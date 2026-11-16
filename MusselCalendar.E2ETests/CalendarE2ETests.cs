namespace MusselCalendar.E2ETests;

using static Microsoft.Playwright.Assertions;

public sealed class CalendarE2ETests(AppFixture app) : IClassFixture<AppFixture>, IAsyncDisposable
{
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

    private async Task<SamplePage> OpenAsync(string mode, bool touch = false) =>
        opened = await app.OpenAsync(mode, touch).ConfigureAwait(true);
}
