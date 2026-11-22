namespace MusselCalendar.E2ETests;

using System.Text.RegularExpressions;

using static Microsoft.Playwright.Assertions;

public sealed class CalendarE2ETests(AppFixture app) : IClassFixture<AppFixture>, IAsyncDisposable
{
    private static readonly Regex TimeRange = new(@"^\d{1,2}月\d{1,2}日 \(.+\) \d{1,2}:\d{2} – \d{1,2}:\d{2}$");

    private static readonly Regex OneDay = new(@"^\d{1,2}月\d{1,2}日 \(.+\)$");

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
    public async Task TheTimeGridSelectsTimesAndAnAllDayCellSelectsADay()
    {
        var sample = await OpenAsync("server");
        var page = sample.Page;
        await sample.ShowAsync("週");
        var column = sample.Calendar.Locator(".mc-column").First;
        await Expect(column).ToBeVisibleAsync();
        var text = page.Locator(".app-popup-text");
        var cancel = page.GetByRole(AriaRole.Button, new() { Name = "キャンセル" });

        await column.ClickAsync();
        await Expect(text).ToHaveTextAsync(TimeRange);
        await cancel.ClickAsync();
        await sample.Calendar.Locator(".mc-allday-cell").First.ClickAsync();
        await Expect(text).ToHaveTextAsync(OneDay);
        await cancel.ClickAsync();
        await Expect(page.Locator(".app-backdrop")).ToHaveCountAsync(0);
        var body = await sample.Calendar.Locator(".mc-body").BoundingBoxAsync() ?? throw new InvalidOperationException("The time grid is not shown.");
        var box = await column.BoundingBoxAsync() ?? throw new InvalidOperationException("The column is not shown.");
        var (x, y) = (box.X + (box.Width / 2), body.Y + (body.Height / 3));
        await page.Mouse.MoveAsync(x, y);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(x, y + (box.Height / 16), new() { Steps = 5 });
        await page.Mouse.UpAsync();

        await Expect(text).ToHaveTextAsync(TimeRange);
    }

    [Fact]
    public async Task AnItemMovedInTheTimeGridKeepsItsLength()
    {
        var sample = await OpenAsync("server");
        var page = sample.Page;
        await sample.ShowAsync("週");
        var lunch = sample.Item("ランチ").First;
        await Expect(lunch).ToBeVisibleAsync();
        await lunch.EvaluateAsync("element => element.scrollIntoView({ block: 'center' })");
        var start = DateTime.ParseExact(await lunch.GetAttributeAsync("data-start") ?? string.Empty, "yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);
        var hour = await sample.Calendar.Locator(".mc-column").First.EvaluateAsync<float>("column => column.getBoundingClientRect().height / 24");
        var box = await lunch.BoundingBoxAsync() ?? throw new InvalidOperationException("The item is not shown.");
        var (x, y) = (box.X + (box.Width / 2), box.Y + (box.Height / 2));

        await page.Mouse.MoveAsync(x, y);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(x, y + hour, new() { Steps = 5 });
        await page.Mouse.UpAsync();

        await Expect(lunch).ToHaveAttributeAsync("data-start", start.AddHours(1).ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture));
        await Expect(lunch).ToHaveAttributeAsync("data-end", start.AddHours(2).ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task AnItemResizedInTheTimeGridKeepsItsStart()
    {
        var sample = await OpenAsync("server");
        var page = sample.Page;
        await sample.ShowAsync("週");
        var lunch = sample.Calendar.Locator(".mc-event", new() { HasText = "ランチ" }).First;
        await Expect(lunch).ToBeVisibleAsync();
        await lunch.EvaluateAsync("element => element.scrollIntoView({ block: 'center' })");
        var start = await lunch.GetAttributeAsync("data-start") ?? string.Empty;
        var end = DateTime.ParseExact(await lunch.GetAttributeAsync("data-end") ?? string.Empty, "yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);
        var hour = await sample.Calendar.Locator(".mc-column").First.EvaluateAsync<float>("column => column.getBoundingClientRect().height / 24");
        var handle = await lunch.Locator(".mc-resize").BoundingBoxAsync() ?? throw new InvalidOperationException("The handle is not shown.");
        var (x, y) = (handle.X + (handle.Width / 2), handle.Y + (handle.Height / 2));

        await page.Mouse.MoveAsync(x, y);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(x, y + hour, new() { Steps = 5 });
        await page.Mouse.UpAsync();

        await Expect(lunch).ToHaveAttributeAsync("data-end", end.AddHours(1).ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture));
        await Expect(lunch).ToHaveAttributeAsync("data-start", start);
    }

    [Fact]
    public async Task ATimedItemDroppedFromTheAllDayLaneOnTheTimeGridKeepsItsLength()
    {
        var sample = await OpenAsync("server");
        var page = sample.Page;
        await sample.ShowAsync("週");
        var training = sample.Calendar.Locator(".mc-allday .mc-bar", new() { HasText = "研修" }).First;
        await Expect(training).ToBeVisibleAsync();
        var thursday = sample.Calendar.Locator(".mc-column").Nth(4);
        var date = DateOnly.ParseExact(await thursday.GetAttributeAsync("data-date") ?? string.Empty, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        var hour = await thursday.EvaluateAsync<float>("column => column.getBoundingClientRect().height / 24");
        await thursday.EvaluateAsync("(column, hour) => { column.closest('.mc-body').scrollTop = 8 * hour; }", hour);
        var bar = await training.BoundingBoxAsync() ?? throw new InvalidOperationException("The item is not shown.");
        var column = await thursday.BoundingBoxAsync() ?? throw new InvalidOperationException("The column is not shown.");
        var (x, y) = (column.X + (column.Width / 2), column.Y + (10.1f * hour));

        await page.Mouse.MoveAsync(bar.X + 10, bar.Y + (bar.Height / 2));
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(x, y, new() { Steps = 10 });
        await page.Mouse.UpAsync();

        await Expect(training).ToHaveAttributeAsync("data-start", date.ToString("yyyy-MM-dd'T10:00:00'", CultureInfo.InvariantCulture));
        await Expect(training).ToHaveAttributeAsync("data-end", date.AddDays(1).ToString("yyyy-MM-dd'T10:00:00'", CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task AnItemMovedToTheNextDayInTheMonthStartsADayLater()
    {
        var sample = await OpenAsync("server");
        var page = sample.Page;
        var lunch = sample.Item("ランチ").First;
        await Expect(lunch).ToBeVisibleAsync();
        var next = DateOnly.ParseExact(await lunch.GetAttributeAsync("data-first") ?? string.Empty, "yyyy-MM-dd", CultureInfo.InvariantCulture).AddDays(1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var item = await lunch.BoundingBoxAsync() ?? throw new InvalidOperationException("The item is not shown.");
        var cell = await sample.Calendar.Locator($".mc-month-cell[data-date='{next}']").BoundingBoxAsync() ?? throw new InvalidOperationException("The next day is not shown.");
        var (x, y) = (item.X + (item.Width / 2), item.Y + (item.Height / 2));

        await page.Mouse.MoveAsync(x, y);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(cell.X + (cell.Width / 2), y, new() { Steps = 5 });
        await page.Mouse.UpAsync();

        await Expect(lunch).ToHaveAttributeAsync("data-first", next);
    }

    [Fact]
    public async Task DaysOffAreShadedAndAHolidayHasItsColor()
    {
        var sample = await OpenAsync("server");
        await sample.ShowAsync("週");
        await sample.Page.GetByRole(AriaRole.Button, new() { Name = "次へ" }).ClickAsync();
        var holiday = sample.Item("祝日(読み取り専用)").First;
        await Expect(holiday).ToBeVisibleAsync();
        var column = sample.Calendar.Locator($".mc-column[data-date='{await holiday.GetAttributeAsync("data-first")}']");
        var columns = sample.Calendar.Locator(".mc-column");

        await Expect(sample.Calendar.Locator(".mc-head .mc-date[title='祝日']")).ToHaveCSSAsync("color", "rgb(217, 48, 37)");
        await Expect(column).ToHaveCSSAsync("background-color", "rgb(248, 249, 250)");
        await Expect(column).ToHaveCSSAsync("background-image", new Regex("linear-gradient"));
        await Expect(columns.First).ToHaveCSSAsync("background-color", "rgb(248, 249, 250)");
        await Expect(columns.Nth(2)).ToHaveCSSAsync("background-color", "rgba(0, 0, 0, 0)");
    }

    [Fact]
    public async Task ItemsOnLightColorsHaveDarkText()
    {
        var sample = await OpenAsync("server");

        var colors = await sample.Page.EvaluateAsync<string[]>(
            """
            () => {
                const host = document.createElement('div');
                host.innerHTML = '<div class="mussel-calendar"><div class="mc-event" style="--mc-color: #f6bf26"></div><div class="mc-event"></div><div class="mc-bar" style="--mc-color: #fff59d"></div></div>'
                    + '<div class="mussel-calendar" style="--mussel-calendar-event-text: #123456"><div class="mc-event" style="--mc-color: #f6bf26"></div></div>'
                    + '<span style="color: oklch(0.38 0 0)"></span><span style="color: oklch(1 0 0)"></span>';
                document.body.appendChild(host);
                return [...host.querySelectorAll('.mc-event, .mc-bar, span')].map((x) => getComputedStyle(x).color);
            }
            """);

        Assert.Equal([colors[4], colors[5], colors[4], "rgb(18, 52, 86)"], colors[..4]);
    }

    [Fact]
    public async Task ItemsJustBelowTheLightnessOfDarkTextHaveWhiteText()
    {
        var sample = await OpenAsync("server");

        var colors = await sample.Page.EvaluateAsync<string[]>(
            """
            () => {
                const host = document.createElement('div');
                host.innerHTML = '<div class="mussel-calendar"><div class="mc-event" style="--mc-color: #00c0ed"></div><div class="mc-bar" style="--mc-color: #00c6c9"></div></div>'
                    + '<span style="color: oklch(1 0 0)"></span>';
                document.body.appendChild(host);
                return [...host.querySelectorAll('.mc-event, .mc-bar, span')].map((x) => getComputedStyle(x).color);
            }
            """);

        Assert.Equal([colors[2], colors[2]], colors[..2]);
    }

    [Fact]
    public async Task ATextColorOfItemsAboveTheViewDoesNotReachIt()
    {
        var sample = await OpenAsync("server");

        var colors = await sample.Page.EvaluateAsync<string[]>(
            """
            () => {
                const host = document.createElement('div');
                host.innerHTML = '<div style="--mussel-calendar-event-text: #123456"><div class="mussel-calendar"><div class="mc-event" style="--mc-color: #f6bf26"></div><div class="mc-event"></div></div></div>'
                    + '<span style="color: oklch(0.38 0 0)"></span><span style="color: oklch(1 0 0)"></span>';
                document.body.appendChild(host);
                return [...host.querySelectorAll('.mc-event, span')].map((x) => getComputedStyle(x).color);
            }
            """);

        Assert.Equal([colors[2], colors[3]], colors[..2]);
    }

    [Fact]
    public async Task TheDragPreviewHasTheTextColorOfTheItem()
    {
        var sample = await OpenAsync("server");
        var page = sample.Page;
        await sample.ShowAsync("週");
        var lunch = sample.Calendar.Locator(".mc-event", new() { HasText = "ランチ" }).First;
        await Expect(lunch).ToBeVisibleAsync();
        await lunch.EvaluateAsync("element => { element.scrollIntoView({ block: 'center' }); element.style.setProperty('--mc-color', '#f6bf26'); }");
        var box = await lunch.BoundingBoxAsync() ?? throw new InvalidOperationException("The item is not shown.");
        var (x, y) = (box.X + (box.Width / 2), box.Y + (box.Height / 2));

        await page.Mouse.MoveAsync(x, y);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(x, y + 30, new() { Steps = 5 });
        await Expect(page.Locator(".mc-preview").First).ToBeVisibleAsync();
        var colors = await lunch.EvaluateAsync<string[]>(
            """
            element => {
                const dark = document.createElement('span');
                dark.style.color = 'oklch(0.38 0 0)';
                document.body.appendChild(dark);
                return [getComputedStyle(document.querySelector('.mc-preview')).color, getComputedStyle(element).color, getComputedStyle(dark).color];
            }
            """);
        await page.Keyboard.PressAsync("Escape");
        await page.Mouse.UpAsync();

        Assert.Equal([colors[2], colors[2]], colors[..2]);
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

    [Fact]
    public async Task KeysOnAnItemWhoseMoveIsUnansweredDoNotReachTheBrowser()
    {
        var sample = await OpenAsync("server");
        var page = sample.Page;
        await sample.ShowAsync("月");
        var lunch = sample.Item("ランチ").First;
        await Expect(lunch).ToBeVisibleAsync();
        await sample.HoldCallsAsync("OnShiftAsync", "OnItemClickAsync");
        await page.EvaluateAsync("() => { window.keys = []; window.addEventListener('keydown', (e) => { if (e.key !== 'Alt') { window.keys.push(e.defaultPrevented); } }); }");

        await lunch.PressAsync("Alt+ArrowLeft");
        await lunch.PressAsync("Alt+ArrowLeft");
        await lunch.PressAsync("Space");
        var keys = await page.EvaluateAsync<bool[]>("() => window.keys");
        var held = await page.EvaluateAsync<string[]>("() => window.held");

        Assert.Equal([true, true, true], keys);
        Assert.Equal(["OnShiftAsync"], held);
    }

    [Fact]
    public async Task AMoveWithoutAnAnswerStopsWaitingAfterTheLimit()
    {
        var sample = await OpenAsync("wasm", clock: true);
        var page = sample.Page;
        await sample.ShowAsync("月");
        var lunch = sample.Item("ランチ").First;
        await Expect(lunch).ToBeVisibleAsync();
        await sample.HoldCallsAsync("OnShiftAsync");

        await lunch.PressAsync("Alt+ArrowLeft");
        await lunch.PressAsync("Alt+ArrowLeft");
        var waiting = await page.EvaluateAsync<int>("() => window.held.length");
        await page.Clock.FastForwardAsync(60_000);
        await lunch.PressAsync("Alt+ArrowLeft");
        var after = await page.EvaluateAsync<int>("() => window.held.length");

        Assert.Equal(1, waiting);
        Assert.Equal(2, after);
    }

    [Fact]
    public async Task MonthLanesWithoutAnAnswerAreSentAgainAfterTheLimit()
    {
        var sample = await OpenAsync("wasm", clock: true);
        var page = sample.Page;
        await sample.ShowAsync("月");
        await Expect(sample.Item("ランチ").First).ToBeVisibleAsync();
        await sample.HoldCallsAsync("OnMonthLanes");

        await page.SetViewportSizeAsync(1280, 600);
        await page.WaitForFunctionAsync("() => window.held.length > 0");
        var sent = await page.EvaluateAsync<int>("() => window.held.length");
        await page.Clock.FastForwardAsync(60_000);
        var again = await page.EvaluateAsync<int>("() => window.held.length");

        Assert.True(again > sent);
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

    private async Task<SamplePage> OpenAsync(string mode, bool touch = false, bool clock = false) =>
        opened = await app.OpenAsync(string.Empty, mode, touch, clock).ConfigureAwait(true);
}
