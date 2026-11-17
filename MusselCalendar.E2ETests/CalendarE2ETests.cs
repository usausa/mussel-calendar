namespace MusselCalendar.E2ETests;

using System.Text.RegularExpressions;

using static Microsoft.Playwright.Assertions;

public sealed class CalendarE2ETests(AppFixture app) : IClassFixture<AppFixture>, IAsyncDisposable
{
    private static readonly HttpClient Client = new();

    private SamplePage? opened;

    public static TheoryData<string> Modes => ["server", "wasm"];

    public async ValueTask DisposeAsync()
    {
        if (opened is not null)
        {
            await opened.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task ItemsKeepTheirElementsWhenOthersAreHidden(string mode)
    {
        var sample = await OpenAsync(mode);
        var lunch = sample.Item("ランチ").First;
        await Expect(lunch).ToBeVisibleAsync();
        var id = await lunch.GetAttributeAsync("data-id");
        await lunch.EvaluateAsync("element => { window.kept = element; }");

        await sample.Page.GetByLabel("仕事").UncheckAsync();
        await Expect(sample.Item("研修")).ToHaveCountAsync(0);

        Assert.True(await sample.Page.EvaluateAsync<bool>("id => window.kept.isConnected && (window.kept.dataset.id === id)", id));
    }

    [Theory]
    [InlineData("週", ".mc-timegrid")]
    [InlineData("スケジュール", ".mc-schedule")]
    public async Task ItemsKeepTheirElementsInTheOtherViews(string view, string container)
    {
        var sample = await OpenAsync("server");
        await sample.ShowAsync(view);
        await Expect(sample.Calendar.Locator(container)).ToBeVisibleAsync();
        var lunch = sample.Item("ランチ").First;
        var birthday = sample.Item("誕生日").First;
        await Expect(lunch).ToBeVisibleAsync();
        await Expect(birthday).ToBeVisibleAsync();
        await lunch.EvaluateAsync("element => { window.lunch = element; }");
        await birthday.EvaluateAsync("element => { window.birthday = element; }");

        await sample.Page.GetByLabel("仕事").UncheckAsync();
        await Expect(sample.Item("研修")).ToHaveCountAsync(0);

        Assert.True(await sample.Page.EvaluateAsync<bool>("() => window.lunch.isConnected && window.birthday.isConnected"));
    }

    [Fact]
    public async Task HoldingATouchNearTheEdgeDoesNotScroll()
    {
        var sample = await OpenAsync("server", true);
        await sample.ShowAsync("週");
        await sample.Page.WaitForFunctionAsync("() => (document.querySelector('.mussel-calendar .mc-body')?.scrollTop ?? 0) > 0");
        var meeting = sample.Item("定例会議").First;
        await Expect(meeting).ToBeVisibleAsync();
        var placed = await meeting.EvaluateAsync<int[]>(
            """
            element => {
                const body = element.closest('.mc-body');
                body.scrollTop += element.getBoundingClientRect().top - body.getBoundingClientRect().top - 10;
                return [Math.round(body.scrollTop), Math.round(element.getBoundingClientRect().top - body.getBoundingClientRect().top)];
            }
            """);
        Assert.InRange(placed[1], 0, 20);

        await SamplePage.TouchAsync(meeting, 600, "touchcancel");

        Assert.Equal(placed[0], await sample.Calendar.Locator(".mc-body").EvaluateAsync<int>("body => Math.round(body.scrollTop)"));
    }

    [Fact]
    public async Task ATouchStartsADragAfterTheLongPressTime()
    {
        var sample = await OpenAsync("server", true);
        var lunch = sample.Item("ランチ").First;
        await Expect(lunch).ToBeVisibleAsync();

        var dragging = await SamplePage.DraggingWhileHeldAsync(lunch, [320, 480]);

        Assert.Equal([false, true], dragging);
    }

    [Fact]
    public async Task ALongPressOnAReadOnlyItemOpensIt()
    {
        var sample = await OpenAsync("server", true);
        await sample.Page.GetByLabel("操作").SelectOptionAsync("1");
        var lunch = sample.Item("ランチ").First;
        await Expect(lunch).ToHaveAttributeAsync("data-read-only", "true");

        await SamplePage.TouchAsync(lunch, 600, "touchend");

        await Expect(sample.PopupTitle).ToHaveTextAsync("ランチ");
    }

    [Fact]
    public async Task CallsThatFailLeaveNoUnhandledRejections()
    {
        var sample = await OpenAsync("server");
        await sample.ShowAsync("週");
        var lunch = sample.Item("ランチ").First;
        await Expect(lunch).ToBeVisibleAsync();
        await sample.RejectCallsAsync("OnItemClick", "OnSelect");

        await lunch.ClickAsync();
        await lunch.PressAsync("Enter");
        await sample.Calendar.Locator(".mc-allday-cell").First.ClickAsync();
        await sample.Calendar.Locator(".mc-column").First.ClickAsync();

        Assert.Equal(0, await sample.UnhandledRejectionsAsync(4));
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task DisposingTheScriptRemovesTheMarks(string mode)
    {
        var sample = await OpenAsync(mode);
        await Expect(sample.Page.Locator(".mussel-mini-month[data-attached]")).ToHaveCountAsync(1);

        var marks = await sample.Page.EvaluateAsync<bool[]>(
            """
            async () => {
                const module = await import('./_content/MusselCalendar/MusselCalendar.js');
                const dotNet = { invokeMethodAsync: () => Promise.resolve() };
                const calendar = document.querySelector('.mussel-calendar');
                const month = document.querySelector('.mussel-mini-month');
                module.attach(calendar, dotNet, 8).dispose();
                module.attachMiniMonth(month, dotNet).dispose();
                return [calendar.hasAttribute('data-attached'), month.hasAttribute('data-attached')];
            }
            """);

        Assert.Equal([false, false], marks);
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task TheRenderModeSwitchFollowsThePage(string mode)
    {
        var sample = await OpenAsync(mode);

        await sample.Page.Locator(".page-switch a[href='date']").ClickAsync();

        await Expect(sample.Page.Locator(".mode-switch a").First).ToHaveAttributeAsync("href", new Regex("redirect=%2Fdate$"));
    }

    [Fact]
    public async Task TheErrorPageAndUnknownAddressesAnswer()
    {
        Assert.SkipWhen(string.IsNullOrEmpty(app.BaseAddress), "Neither Edge nor Chrome was found, so the E2E test is skipped.");
        var token = Xunit.TestContext.Current.CancellationToken;

        using var error = await Client.GetAsync(new Uri($"{app.BaseAddress}error"), token);
        using var missing = await Client.GetAsync(new Uri($"{app.BaseAddress}no-such"), token);
        using var outside = await Client.GetAsync(new Uri($"{app.BaseAddress}render-mode/server?redirect=%2F%5Cexample.com"), token);

        Assert.Equal(System.Net.HttpStatusCode.OK, error.StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(new Uri(app.BaseAddress), outside.RequestMessage?.RequestUri);
    }

    private async Task<SamplePage> OpenAsync(string mode, bool touch = false) =>
        opened = await app.OpenAsync(string.Empty, mode, touch).ConfigureAwait(true);
}
