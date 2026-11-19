namespace MusselCalendar.E2ETests;

using System.Text.RegularExpressions;

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

    [Theory]
    [InlineData("週", ".mc-timegrid", "仕事", "誕生日", "研修")]
    [InlineData("週", ".mc-timegrid", "個人", "夜間作業", "ジム")]
    [InlineData("スケジュール", ".mc-schedule", "個人", "予定 3", "予定 2")]
    public async Task ItemsKeepTheirElementsInTheOtherViews(string view, string container, string calendar, string name, string gone)
    {
        var sample = await OpenAsync("server");
        await sample.ShowAsync(view);
        await Expect(sample.Calendar.Locator(container)).ToBeVisibleAsync();
        var kept = sample.Item(name).First;
        await Expect(kept).ToBeVisibleAsync();
        var id = await kept.GetAttributeAsync("data-id");
        await kept.EvaluateAsync("element => { window.kept = element; }");

        await sample.Page.GetByLabel(calendar).UncheckAsync();
        await Expect(sample.Item(gone)).ToHaveCountAsync(0);

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
    public async Task TheDragPreviewTakesTheFontOfTheCalendar()
    {
        var sample = await OpenAsync("server");
        var page = sample.Page;
        var lunch = sample.Item("ランチ").First;
        await Expect(lunch).ToBeVisibleAsync();
        await sample.Calendar.EvaluateAsync("calendar => calendar.style.setProperty('--mussel-font-family', 'monospace')");
        var box = await lunch.BoundingBoxAsync() ?? throw new InvalidOperationException("The item is not shown.");
        var (x, y) = (box.X + (box.Width / 2), box.Y + (box.Height / 2));

        await page.Mouse.MoveAsync(x, y);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(x + 40, y + 40, new() { Steps = 5 });
        var font = await page.Locator(".mc-preview").First.EvaluateAsync<string>("preview => getComputedStyle(preview).fontFamily");
        await page.Keyboard.PressAsync("Escape");
        await page.Mouse.UpAsync();

        Assert.Equal("monospace", font);
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
        await sample.RejectCallsAsync("OnItemClickAsync", "OnSelectAsync");

        await lunch.ClickAsync();
        await lunch.PressAsync("Enter");
        await sample.Calendar.Locator(".mc-allday-cell").First.ClickAsync();
        await sample.Calendar.Locator(".mc-column").First.ClickAsync();

        Assert.Equal(0, await sample.UnhandledRejectionsAsync(4));
    }

    [Fact]
    public async Task KeysWhoseCallsFailLeaveNoUnhandledRejections()
    {
        var sample = await OpenAsync("server");
        await sample.RejectCallsAsync("OnDayClickAsync", "OnYearFocus", "OnSelectDateAsync", "OnFocusDateAsync");
        await sample.ShowAsync("年");
        var day = sample.Calendar.Locator(".mc-year-day[data-date$='-06-15']");
        await Expect(day).ToBeVisibleAsync();

        await day.FocusAsync();
        await sample.Page.Keyboard.PressAsync("ArrowRight");
        await sample.Page.Keyboard.PressAsync("Enter");
        await sample.Page.Locator(".mussel-mini-month td[tabindex='0']").First.FocusAsync();
        await sample.Page.Keyboard.PressAsync("ArrowRight");
        await sample.Page.Keyboard.PressAsync("Enter");

        Assert.Equal(0, await sample.UnhandledRejectionsAsync(4));
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task TheRootsLoseTheirMarkWhenTheirScriptsAreDisposed(string mode)
    {
        var sample = await OpenAsync(mode);
        var count = await sample.Page.EvaluateAsync<int>(
            """
            () => {
                window.lostMarks = 0;
                const roots = document.querySelectorAll('.mussel-calendar, .mussel-mini-month');
                for (const root of roots) {
                    new MutationObserver(() => {
                        if (!root.hasAttribute('data-attached')) {
                            window.lostMarks++;
                        }
                    }).observe(root, { attributes: true, attributeFilter: ['data-attached'] });
                }
                return roots.length;
            }
            """);

        await sample.Page.Locator(".page-switch a[href='date']").ClickAsync();

        Assert.Equal(2, count);
        await sample.Page.WaitForFunctionAsync("count => window.lostMarks === count", count);
    }

    [Fact]
    public async Task RenderModeSwitchKeepsThePage()
    {
        var sample = opened = await app.OpenAsync("date", "server");
        var active = sample.Page.Locator(".mode-switch a.active");
        await Expect(active).ToHaveTextAsync("Server");

        await sample.Page.Locator(".mode-switch").GetByText("WebAssembly").ClickAsync();
        await Expect(active).ToHaveTextAsync("WebAssembly");
        await Expect(sample.Page.Locator(".page-switch a.active")).ToHaveTextAsync("日付の選択");
        await sample.WaitForCalendarAsync();
        Assert.EndsWith("/date", sample.Page.Url, StringComparison.Ordinal);

        var day = sample.Page.Locator(".mussel-mini-month").First.Locator("td.mm-day:not(.mm-outside):not([aria-selected])").First;
        var date = await day.GetAttributeAsync("data-date");
        await day.ClickAsync();
        await Expect(sample.Page.Locator($".mussel-mini-month td[data-date='{date}']").First).ToHaveAttributeAsync("aria-selected", "true");

        await sample.Page.Locator(".page-switch").GetByText("カレンダー").ClickAsync();
        await Expect(sample.Page.Locator(".mode-switch a").First).ToHaveAttributeAsync("href", new Regex("redirect=%2F$"));
        await Expect(active).ToHaveTextAsync("WebAssembly");
        await sample.Page.Locator(".mode-switch").GetByText("Server").ClickAsync();
        await Expect(active).ToHaveTextAsync("Server");
        Assert.Equal(app.BaseAddress, sample.Page.Url);
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task PagesArePrerendered(string mode)
    {
        var sample = opened = await app.OpenBlankAsync(javaScriptEnabled: false);

        await sample.Page.GotoAsync($"{app.BaseAddress}render-mode/{mode}?redirect=%2F");
        await Expect(sample.Page.Locator(".page-switch a.active")).ToHaveTextAsync("カレンダー");
        await Expect(sample.Item("ランチ").First).ToBeVisibleAsync();
        await Expect(sample.Page.Locator(".mussel-mini-month td[aria-current='date']").First).ToBeVisibleAsync();
        await Expect(sample.Page.Locator("[data-attached]")).ToHaveCountAsync(0);
        await Expect(sample.Page.Locator(".mode-switch a.active")).ToHaveTextAsync(mode == "wasm" ? "WebAssembly" : "Server");
    }

    [Fact]
    public async Task UnknownAddressShowsTheNotFoundPage()
    {
        var sample = opened = await app.OpenBlankAsync();

        var response = await sample.Page.GotoAsync($"{app.BaseAddress}no-such-page");
        Assert.Equal(404, response?.Status);
        await Expect(sample.Page.Locator("h1")).ToHaveTextAsync("見つかりません");
        await Expect(sample.Page.Locator(".page-switch")).ToHaveCountAsync(0);

        await sample.Page.GetByRole(AriaRole.Link, new() { Name = "ホーム" }).ClickAsync();
        await sample.WaitForCalendarAsync();
    }

    [Fact]
    public async Task TheErrorPageOpens()
    {
        var sample = opened = await app.OpenBlankAsync();

        var response = await sample.Page.GotoAsync($"{app.BaseAddress}error");
        Assert.Equal(200, response?.Status);
        await Expect(sample.Page.Locator("h1")).ToHaveTextAsync("エラー");
    }

    [Fact]
    public async Task RedirectsOutsideTheAppGoHome()
    {
        var sample = opened = await app.OpenBlankAsync();

        var response = await sample.Page.GotoAsync($"{app.BaseAddress}render-mode/server?redirect=%2F%5Cexample.com");
        Assert.Equal(200, response?.Status);
        Assert.Equal(app.BaseAddress, sample.Page.Url);
    }

    private async Task<SamplePage> OpenAsync(string mode, bool touch = false) =>
        opened = await app.OpenAsync(string.Empty, mode, touch).ConfigureAwait(true);
}
