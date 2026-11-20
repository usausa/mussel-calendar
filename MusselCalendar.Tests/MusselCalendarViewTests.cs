namespace MusselCalendar;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

using MusselCalendar.Internal;

public sealed class MusselCalendarViewTests : BunitContext
{
    private static readonly DateOnly Thursday = new(2026, 11, 12);

    public MusselCalendarViewTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void WeekViewStartsOnFirstDayOfWeek()
    {
        var cut = Render<MusselCalendarView>(static parameters => parameters
            .Add(static p => p.Date, Thursday)
            .Add(static p => p.FirstDayOfWeek, DayOfWeek.Monday)
            .Add(static p => p.Culture, CultureInfo.InvariantCulture));

        var columns = cut.FindAll(".mc-column");
        Assert.Equal(7, columns.Count);
        Assert.Equal("2026-11-09", columns[0].GetAttribute("data-date"));
    }

    [Fact]
    public async Task AScrollAskedForWhileTheScriptIsAttachingRunsAfterIt()
    {
        var runtime = new LateRuntime { Module = { Hold = "attach" } };
        Services.AddSingleton<IJSRuntime>(runtime);
        var cut = Render<MusselCalendarView>(static parameters => parameters
            .Add(static p => p.View, CalendarViewKind.Month)
            .Add(static p => p.Date, Thursday)
            .Add(static p => p.Culture, CultureInfo.InvariantCulture));
        await cut.InvokeAsync(() => runtime.Import.SetResult(runtime.Module));
        await cut.WaitForAssertionAsync(() => Assert.Contains("attach", runtime.Module.Calls));

        cut.Render(static parameters => parameters.Add(static p => p.View, CalendarViewKind.Week));
        await cut.InvokeAsync(runtime.Module.Resume.SetResult);

        await cut.WaitForAssertionAsync(() => Assert.Contains("scroll", runtime.Module.Calls));
    }

    [Fact]
    public async Task AFailedCallBeforeTheScriptIsAttachedDoesNotStopTheRest()
    {
        var runtime = new LateRuntime { Module = { Hold = "attach", Fail = "scroll" } };
        Services.AddSingleton<IJSRuntime>(runtime);
        var cut = Render<MusselCalendarView>(static parameters => parameters
            .Add(static p => p.View, CalendarViewKind.Month)
            .Add(static p => p.Date, Thursday)
            .Add(static p => p.Culture, CultureInfo.InvariantCulture));
        await cut.InvokeAsync(() => runtime.Import.SetResult(runtime.Module));
        await cut.WaitForAssertionAsync(() => Assert.Contains("attach", runtime.Module.Calls));

        cut.Render(static parameters => parameters.Add(static p => p.View, CalendarViewKind.Week));
        cut.Render(static parameters => parameters.Add(static p => p.View, CalendarViewKind.Month));
        await cut.InvokeAsync(runtime.Module.Resume.SetResult);

        await cut.WaitForAssertionAsync(() => Assert.Contains("ready", runtime.Module.Calls));
        Assert.Equal(["attach", "scroll", "fit", "ready"], runtime.Module.Calls);
        Assert.False(Renderer.UnhandledException.IsCompleted);
    }

    [Fact]
    public async Task AFitAskedForWhileTheScriptIsAttachingRunsAfterIt()
    {
        var runtime = new LateRuntime { Module = { Hold = "attach" } };
        Services.AddSingleton<IJSRuntime>(runtime);
        var cut = Render<MusselCalendarView>(static parameters => parameters
            .Add(static p => p.View, CalendarViewKind.Month)
            .Add(static p => p.Date, Thursday)
            .Add(static p => p.Culture, CultureInfo.InvariantCulture));
        await cut.InvokeAsync(() => runtime.Import.SetResult(runtime.Module));
        await cut.WaitForAssertionAsync(() => Assert.Contains("attach", runtime.Module.Calls));

        cut.Render(static parameters => parameters.Add(static p => p.Date, new DateOnly(2026, 2, 12)));
        await cut.InvokeAsync(runtime.Module.Resume.SetResult);

        await cut.WaitForAssertionAsync(() => Assert.Contains("fit", runtime.Module.Calls));
    }

    [Fact]
    public async Task DisposingTellsTheScriptAndReleasesIt()
    {
        var instance = new LateModule();
        var runtime = new LateRuntime { Module = { Returned = instance } };
        Services.AddSingleton<IJSRuntime>(runtime);
        var cut = Render<MusselCalendarView>(static parameters => parameters.Add(static p => p.Date, Thursday));
        await cut.InvokeAsync(() => runtime.Import.SetResult(runtime.Module));
        await cut.WaitForAssertionAsync(() => Assert.Contains("ready", instance.Calls));

        await cut.InvokeAsync(() => cut.Instance.DisposeAsync().AsTask());

        Assert.Equal(["attach"], runtime.Module.Calls);
        Assert.Contains("dispose", instance.Calls);
        Assert.True(instance.Released);
        Assert.True(runtime.Module.Released);
    }

    [Fact]
    public async Task AFailedDisposeStillReleasesTheScript()
    {
        var instance = new LateModule { Fail = "dispose" };
        var runtime = new LateRuntime { Module = { Returned = instance } };
        Services.AddSingleton<IJSRuntime>(runtime);
        var cut = Render<MusselCalendarView>(static parameters => parameters.Add(static p => p.Date, Thursday));
        await cut.InvokeAsync(() => runtime.Import.SetResult(runtime.Module));
        await cut.WaitForAssertionAsync(() => Assert.Contains("ready", instance.Calls));

        await cut.InvokeAsync(() => cut.Instance.DisposeAsync().AsTask());

        Assert.Contains("dispose", instance.Calls);
        Assert.True(instance.Released);
        Assert.True(runtime.Module.Released);
    }

    [Fact]
    public async Task TheScriptThatArrivesAfterTheViewIsGoneIsReleased()
    {
        var runtime = new LateRuntime();
        Services.AddSingleton<IJSRuntime>(runtime);
        var cut = Render<MusselCalendarView>(static parameters => parameters.Add(static p => p.Date, Thursday));

        await cut.InvokeAsync(() => cut.Instance.DisposeAsync().AsTask());
        await cut.InvokeAsync(() => runtime.Import.SetResult(runtime.Module));

        await cut.WaitForAssertionAsync(() => Assert.True(runtime.Module.Released));
        Assert.DoesNotContain("attach", runtime.Module.Calls);
    }

    [Fact]
    public async Task DisposingWhileTheScriptIsAttachingReleasesWhatAttachReturns()
    {
        var instance = new LateModule();
        var runtime = new LateRuntime { Module = { Hold = "attach", Returned = instance } };
        Services.AddSingleton<IJSRuntime>(runtime);
        var cut = Render<MusselCalendarView>(static parameters => parameters.Add(static p => p.Date, Thursday));
        await cut.InvokeAsync(() => runtime.Import.SetResult(runtime.Module));
        await cut.WaitForAssertionAsync(() => Assert.Equal(["attach"], runtime.Module.Calls));

        await cut.InvokeAsync(() => cut.Instance.DisposeAsync().AsTask());
        await cut.InvokeAsync(runtime.Module.Resume.SetResult);

        await cut.WaitForAssertionAsync(() => Assert.True(instance.Released));
        Assert.False(Renderer.UnhandledException.IsCompleted);
        Assert.Equal(["dispose"], instance.Calls);
        Assert.True(runtime.Module.Released);
    }

    [Fact]
    public async Task DisposingWhileACallThatWaitedRunsStopsTheRest()
    {
        var runtime = new LateRuntime { Module = { Hold = "scroll" } };
        Services.AddSingleton<IJSRuntime>(runtime);
        var cut = Render<MusselCalendarView>(static parameters => parameters
            .Add(static p => p.View, CalendarViewKind.Month)
            .Add(static p => p.Date, Thursday)
            .Add(static p => p.Culture, CultureInfo.InvariantCulture));
        cut.Render(static parameters => parameters.Add(static p => p.View, CalendarViewKind.Week));
        cut.Render(static parameters => parameters.Add(static p => p.View, CalendarViewKind.Month));
        await cut.InvokeAsync(() => runtime.Import.SetResult(runtime.Module));
        await cut.WaitForAssertionAsync(() => Assert.Contains("scroll", runtime.Module.Calls));

        await cut.InvokeAsync(() => cut.Instance.DisposeAsync().AsTask());
        await cut.InvokeAsync(runtime.Module.Resume.SetResult);

        Assert.False(Renderer.UnhandledException.IsCompleted);
    }

    [Fact]
    public void MonthViewRendersWholeWeeks()
    {
        var cut = Render<MusselCalendarView>(static parameters => parameters
            .Add(static p => p.View, CalendarViewKind.Month)
            .Add(static p => p.Date, Thursday)
            .Add(static p => p.Culture, CultureInfo.InvariantCulture));

        Assert.Equal(5, cut.FindAll(".mc-week").Count);
        Assert.Equal(35, cut.FindAll(".mc-month-cell").Count);
    }

    [Fact]
    public void MonthViewShowsMoreForHiddenItems()
    {
        CalendarItem[] items = [Item("a", 9, 10), Item("b", 10, 11), Item("c", 11, 12), Item("d", 12, 13)];

        var cut = Render<MusselCalendarView>(parameters => parameters
            .Add(static p => p.View, CalendarViewKind.Month)
            .Add(static p => p.Date, Thursday)
            .Add(static p => p.Items, items)
            .Add(static p => p.Culture, CultureInfo.InvariantCulture));

        Assert.Equal("+2", cut.Find(".mc-more").TextContent);
        Assert.Equal(2, cut.FindAll(".mc-bar").Count);
    }

    [Fact]
    public void ColorsThatAreNotSafeInStylesAreLeftOut()
    {
        var plain = Item("a", 9, 10);
        plain.Color = "#5794f2";
        var hostile = Item("b", 11, 12);
        hostile.Color = "red; position: fixed; inset: 0; z-index: 9999";

        var cut = Render<MusselCalendarView>(parameters => parameters
            .Add(static p => p.Date, Thursday)
            .Add(static p => p.Items, [plain, hostile]));

        var styles = cut.FindAll(".mc-event").Select(static x => x.GetAttribute("style") ?? string.Empty).ToList();
        Assert.Contains(styles, static x => x.Contains("--mc-color: #5794f2;", StringComparison.Ordinal));
        Assert.DoesNotContain(styles, static x => x.Contains("position", StringComparison.Ordinal));
    }

    [Fact]
    public void ShortItemUsesOneLine()
    {
        CalendarItem[] items = [Item("a", 9, 9.25)];

        var cut = Render<MusselCalendarView>(parameters => parameters
            .Add(static p => p.Date, Thursday)
            .Add(static p => p.Items, items));

        Assert.Single(cut.FindAll(".mc-event.mc-short"));
    }

    [Fact]
    public async Task MoveRaisesItemChanged()
    {
        var item = Item("a", 9, 10);
        CalendarItemChangeEventArgs? changed = null;
        var cut = Render<MusselCalendarView>(parameters => parameters
            .Add(static p => p.Date, Thursday)
            .Add(static p => p.Items, new[] { item })
            .Add(static p => p.ItemChanged, e => changed = e));

        await cut.InvokeAsync(() => new CalendarScriptBridge(cut.Instance).OnMoveAsync("a", "2026-11-13T12:00:00", "2026-11-13T13:00:00", false));

        Assert.NotNull(changed);
        Assert.Same(item, changed.Item);
        Assert.Equal((new DateTime(2026, 11, 13, 12, 0, 0), new DateTime(2026, 11, 13, 13, 0, 0), false), (changed.Start, changed.End, changed.AllDay));
        await cut.InvokeAsync(() => new CalendarScriptBridge(cut.Instance).OnMoveAsync("a", "2026-11-14T00:00:00", "2026-11-15T00:00:00", true));
        Assert.Equal((new DateTime(2026, 11, 14), new DateTime(2026, 11, 15), true), (changed.Start, changed.End, changed.AllDay));
    }

    [Fact]
    public async Task MoveKeepsTheKindOfTheTimes()
    {
        var item = Item("a", 9, 10);
        item.Start = DateTime.SpecifyKind(item.Start, DateTimeKind.Local);
        item.End = DateTime.SpecifyKind(item.End, DateTimeKind.Local);
        CalendarItemChangeEventArgs? changed = null;
        var cut = Render<MusselCalendarView>(parameters => parameters
            .Add(static p => p.Date, Thursday)
            .Add(static p => p.Items, new[] { item })
            .Add(static p => p.ItemChanged, e => changed = e));

        await cut.InvokeAsync(() => new CalendarScriptBridge(cut.Instance).OnMoveAsync("a", "2026-11-13T12:00:00", "2026-11-13T13:00:00", false));

        Assert.Equal((DateTimeKind.Local, DateTimeKind.Local), (changed?.Start.Kind, changed?.End.Kind));
    }

    [Fact]
    public async Task ShiftAndExtendKeepTheOtherEdge()
    {
        var item = Item("a", 9, 10);
        var changes = new List<CalendarItemChangeEventArgs>();
        var cut = Render<MusselCalendarView>(parameters => parameters
            .Add(static p => p.Date, Thursday)
            .Add(static p => p.Items, new[] { item })
            .Add(static p => p.ItemChanged, changes.Add));

        await cut.InvokeAsync(() => new CalendarScriptBridge(cut.Instance).OnShiftAsync("a", 1, 30));
        await cut.InvokeAsync(() => new CalendarScriptBridge(cut.Instance).OnExtendAsync("a", 15));
        await cut.InvokeAsync(() => new CalendarScriptBridge(cut.Instance).OnExtendAsync("a", -60));

        Assert.Equal(2, changes.Count);
        Assert.Equal((new DateTime(2026, 11, 13, 9, 30, 0), new DateTime(2026, 11, 13, 10, 30, 0), false), (changes[0].Start, changes[0].End, changes[0].AllDay));
        Assert.Equal((item.Start, item.End.AddMinutes(15), false), (changes[1].Start, changes[1].End, changes[1].AllDay));
    }

    [Fact]
    public async Task AllDayItemsStayAllDayWhenShiftedAndDoNotExtend()
    {
        var day = Thursday.ToDateTime(TimeOnly.MinValue);
        var item = new CalendarItem { Id = "a", Title = "a", Start = day, End = day.AddDays(1), AllDay = true };
        var changes = new List<CalendarItemChangeEventArgs>();
        var cut = Render<MusselCalendarView>(parameters => parameters
            .Add(static p => p.Date, Thursday)
            .Add(static p => p.Items, new[] { item })
            .Add(static p => p.ItemChanged, changes.Add));

        await cut.InvokeAsync(() => new CalendarScriptBridge(cut.Instance).OnShiftAsync("a", 1, 0));
        await cut.InvokeAsync(() => new CalendarScriptBridge(cut.Instance).OnExtendAsync("a", 15));

        var changed = Assert.Single(changes);
        Assert.Equal((day.AddDays(1), day.AddDays(2), true), (changed.Start, changed.End, changed.AllDay));
    }

    [Fact]
    public async Task ReadOnlyItemIsNotChanged()
    {
        var item = Item("a", 9, 10);
        item.ReadOnly = true;
        var raised = false;
        var cut = Render<MusselCalendarView>(parameters => parameters
            .Add(static p => p.Date, Thursday)
            .Add(static p => p.Items, new[] { item })
            .Add(static p => p.ItemChanged, _ => raised = true));

        await cut.InvokeAsync(() => new CalendarScriptBridge(cut.Instance).OnMoveAsync("a", "2026-11-13T12:00:00", "2026-11-13T13:00:00", false));
        await cut.InvokeAsync(() => new CalendarScriptBridge(cut.Instance).OnShiftAsync("a", 1, 0));

        Assert.False(raised);
    }

    [Fact]
    public async Task SelectRaisesRangeSelected()
    {
        var selections = new List<CalendarSelectEventArgs>();
        var cut = Render<MusselCalendarView>(parameters => parameters
            .Add(static p => p.Date, Thursday)
            .Add(static p => p.RangeSelected, selections.Add));

        await cut.InvokeAsync(() => new CalendarScriptBridge(cut.Instance).OnSelectAsync("2026-11-12T00:00:00", "2026-11-14T00:00:00", true, 10, 20, 30, 40));
        await cut.InvokeAsync(() => new CalendarScriptBridge(cut.Instance).OnSelectAsync("2026-11-12T09:00:00", "2026-11-12T10:30:00", false, 1, 2, 3, 4));

        Assert.Equal(2, selections.Count);
        Assert.Equal((new DateTime(2026, 11, 12), new DateTime(2026, 11, 14), true), (selections[0].Start, selections[0].End, selections[0].AllDay));
        Assert.Equal(new CalendarAnchor(10, 20, 30, 40), selections[0].Anchor);
        Assert.Equal((new DateTime(2026, 11, 12, 9, 0, 0), new DateTime(2026, 11, 12, 10, 30, 0), false), (selections[1].Start, selections[1].End, selections[1].AllDay));
        Assert.Equal(new CalendarAnchor(1, 2, 3, 4), selections[1].Anchor);
    }

    [Fact]
    public void RootKeepsOwnClassAndStyle()
    {
        var cut = Render<MusselCalendarView>(static parameters => parameters
            .Add(static p => p.Date, Thursday)
            .Add(static p => p.HourHeight, 60)
            .AddUnmatched("class", "schedule")
            .AddUnmatched("style", "--mussel-calendar-today: #e8710a"));

        var root = cut.Find("div");
        Assert.Equal("mussel-calendar schedule", root.GetAttribute("class"));
        Assert.Equal("--mussel-calendar-hour-height: 60px; --mussel-calendar-today: #e8710a", root.GetAttribute("style"));
    }

    [Fact]
    public void FormatsChangeLabels()
    {
        CalendarItem[] items = [Item("a", 13.5, 14.5)];

        var week = Render<MusselCalendarView>(parameters => parameters
            .Add(static p => p.Date, Thursday)
            .Add(static p => p.Items, items)
            .Add(static p => p.Culture, CultureInfo.GetCultureInfo("en-US"))
            .Add(static p => p.HourFormat, "h tt")
            .Add(static p => p.TimeFormat, "h:mm tt"));
        var month = Render<MusselCalendarView>(static parameters => parameters
            .Add(static p => p.View, CalendarViewKind.Month)
            .Add(static p => p.Date, Thursday)
            .Add(static p => p.Culture, CultureInfo.GetCultureInfo("en-US"))
            .Add(static p => p.MonthDayFormat, "MMM d"));

        Assert.Equal("1 PM", week.FindAll(".mc-time")[12].TextContent);
        Assert.Equal("1:30 PM – 2:30 PM", week.Find(".mc-event-time").TextContent);
        Assert.Contains(month.FindAll(".mc-month-cell .mc-date"), static x => x.TextContent == "Nov 1");
    }

    [Fact]
    public async Task FittedLanesRebuildMonth()
    {
        CalendarItem[] items = [Item("a", 9, 10), Item("b", 10, 11), Item("c", 11, 12), Item("d", 12, 13), Item("e", 13, 14)];
        var cut = Render<MusselCalendarView>(parameters => parameters
            .Add(static p => p.View, CalendarViewKind.Month)
            .Add(static p => p.Date, Thursday)
            .Add(static p => p.Items, items));

        await cut.InvokeAsync(() => new CalendarScriptBridge(cut.Instance).OnMonthLanes(6));

        Assert.Equal("true", cut.Find("div").GetAttribute("data-fit-lanes"));
        Assert.Contains("--mc-lanes: 6", cut.Find(".mc-week").GetAttribute("style"), StringComparison.Ordinal);
        Assert.Equal(5, cut.FindAll(".mc-bar").Count);
        Assert.Empty(cut.FindAll(".mc-more"));
    }

    [Fact]
    public async Task MoreClickReportsTheItemsOfTheDay()
    {
        var thursday = Thursday.ToDateTime(TimeOnly.MinValue);
        CalendarItem[] items =
        [
            Item("a", 9, 10),
            new() { Id = "midnight", Title = "midnight", Start = thursday.AddHours(-2), End = thursday },
            new() { Id = "empty", Title = "empty", Start = thursday, End = thursday, AllDay = true }
        ];
        CalendarMoreEventArgs? more = null;
        var cut = Render<MusselCalendarView>(parameters => parameters
            .Add(static p => p.View, CalendarViewKind.Month)
            .Add(static p => p.Date, Thursday)
            .Add(static p => p.Items, items)
            .Add(static p => p.MoreClicked, e => more = e));

        await cut.InvokeAsync(() => new CalendarScriptBridge(cut.Instance).OnMoreClickAsync("2026-11-12", 10, 20, 30, 40));

        Assert.NotNull(more);
        Assert.Equal(Thursday, more.Day);
        Assert.Equal(new CalendarAnchor(10, 20, 30, 40), more.Anchor);
        Assert.Equal(["empty", "a"], more.Items.Select(static x => x.Id));
    }

    [Fact]
    public void TodayMovesAtMidnight()
    {
        var clock = new ManualTimeProvider { Now = new DateTimeOffset(2026, 11, 12, 23, 59, 30, TimeSpan.Zero) };
        var cut = Render<MusselCalendarView>(parameters => parameters
            .Add(static p => p.Date, Thursday)
            .Add(static p => p.TimeProvider, clock));
        Assert.Equal("2026-11-12", cut.Find(".mc-now").ParentElement?.GetAttribute("data-date"));
        Assert.Equal((TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(1)), Assert.Single(clock.Timings));

        clock.Now = clock.Now.AddSeconds(30);
        clock.Fire();

        cut.WaitForAssertion(() => Assert.Equal("2026-11-13", cut.Find(".mc-now").ParentElement?.GetAttribute("data-date")));
        Assert.Equal(5, cut.FindAll(".mc-day-head").ToList().FindIndex(static x => x.ClassList.Contains("mc-today")));
    }

    [Fact]
    public async Task FixedLanesIgnoreFitting()
    {
        CalendarItem[] items = [Item("a", 9, 10), Item("b", 10, 11), Item("c", 11, 12), Item("d", 12, 13)];
        var cut = Render<MusselCalendarView>(parameters => parameters
            .Add(static p => p.View, CalendarViewKind.Month)
            .Add(static p => p.Date, Thursday)
            .Add(static p => p.Items, items)
            .Add(static p => p.MonthLanes, 3));

        await cut.InvokeAsync(() => new CalendarScriptBridge(cut.Instance).OnMonthLanes(6));

        Assert.Equal("false", cut.Find("div").GetAttribute("data-fit-lanes"));
        Assert.Equal("+2", cut.Find(".mc-more").TextContent);
    }

    [Fact]
    public void ScheduleListsDaysWithItems()
    {
        var thursday = Thursday.ToDateTime(TimeOnly.MinValue);
        CalendarItem[] items =
        [
            Item("meeting", 10, 11),
            new() { Id = "trip", Title = "trip", Start = thursday, End = thursday.AddDays(2), AllDay = true },
            new() { Id = "night", Title = "night", Start = thursday.AddHours(22), End = thursday.AddHours(26) }
        ];

        var cut = Render<MusselCalendarView>(parameters => parameters
            .Add(static p => p.View, CalendarViewKind.Schedule)
            .Add(static p => p.Date, Thursday)
            .Add(static p => p.Items, items)
            .Add(static p => p.Culture, CultureInfo.InvariantCulture));

        var days = cut.FindAll(".mc-schedule-day");
        Assert.Equal(2, days.Count);
        Assert.Equal(["trip", "meeting", "night"], days[0].QuerySelectorAll(".mc-schedule-title").Select(static x => x.TextContent));
        Assert.Equal(["All day", "10:00 – 11:00", "22:00 –"], days[0].QuerySelectorAll(".mc-schedule-time").Select(static x => x.TextContent));
        Assert.Equal(["trip", "night"], days[1].QuerySelectorAll(".mc-schedule-title").Select(static x => x.TextContent));
        Assert.Equal(["All day", "– 2:00"], days[1].QuerySelectorAll(".mc-schedule-time").Select(static x => x.TextContent));
        Assert.Equal("Nov, Fri", days[1].QuerySelector(".mc-schedule-day-text")?.TextContent);
    }

    [Fact]
    public void ScheduleShowsEmptyText()
    {
        var cut = Render<MusselCalendarView>(static parameters => parameters
            .Add(static p => p.View, CalendarViewKind.Schedule)
            .Add(static p => p.Date, Thursday)
            .Add(static p => p.EmptyText, "Nothing"));

        Assert.Equal("Nothing", cut.Find(".mc-schedule-empty").TextContent);
        Assert.Empty(cut.FindAll(".mc-schedule-day"));
    }

    [Fact]
    public void YearMarksDaysWithItems()
    {
        var thursday = Thursday.ToDateTime(TimeOnly.MinValue);
        CalendarItem[] items =
        [
            Item("a", 9, 10),
            Item("b", 11, 12),
            new() { Id = "trip", Title = "trip", Start = thursday.AddDays(1), End = thursday.AddDays(3), AllDay = true }
        ];
        DateOnly? clicked = null;
        var cut = Render<MusselCalendarView>(parameters => parameters
            .Add(static p => p.View, CalendarViewKind.Year)
            .Add(static p => p.Date, Thursday)
            .Add(static p => p.Items, items)
            .Add(static p => p.ItemCountFormat, "{0} items")
            .Add(static p => p.Culture, CultureInfo.InvariantCulture)
            .Add(static p => p.DayClicked, day => clicked = day));

        Assert.Equal(12, cut.FindAll(".mc-year-month").Count);
        Assert.Equal(365, cut.FindAll(".mc-year-day").Count);
        Assert.Equal(["2026-11-12"], cut.FindAll(".mc-year-day[tabindex='0']").Select(static x => x.GetAttribute("data-date")));
        var grids = cut.FindAll("table.mc-year-grid[role='grid']");
        Assert.Equal(12, grids.Count);
        Assert.Equal(cut.FindAll(".mc-year-title")[10].Id, grids[10].GetAttribute("aria-labelledby"));
        var header = cut.Find("th.mc-year-weekday");
        Assert.Equal(("col", "Sunday", "Su"), (header.GetAttribute("scope"), header.GetAttribute("abbr"), header.TextContent));
        Assert.Equal(5, grids[0].QuerySelectorAll("tbody tr").Length);
        var marked = cut.FindAll(".mc-year-day.mc-marked");
        Assert.Equal(3, marked.Count);
        Assert.Equal("Thursday, 12 November 2026, 2 items", marked[0].GetAttribute("aria-label"));

        marked[1].Click();

        Assert.Equal(new DateOnly(2026, 11, 13), clicked);
    }

    [Fact]
    public async Task YearFocusAndEnterComeFromScript()
    {
        DateOnly? clicked = null;
        var cut = Render<MusselCalendarView>(parameters => parameters
            .Add(static p => p.View, CalendarViewKind.Year)
            .Add(static p => p.Date, Thursday)
            .Add(static p => p.DayClicked, day => clicked = day));

        await cut.InvokeAsync(() => new CalendarScriptBridge(cut.Instance).OnYearFocus("2026-03-05"));
        cut.Render(static _ => { });

        Assert.Equal(["2026-03-05"], cut.FindAll(".mc-year-day[tabindex='0']").Select(static x => x.GetAttribute("data-date")));

        await cut.InvokeAsync(() => new CalendarScriptBridge(cut.Instance).OnDayClickAsync("2026-03-05"));

        Assert.Equal(new DateOnly(2026, 3, 5), clicked);
    }

    [Fact]
    public async Task ReadOnlyViewStopsChanges()
    {
        var raised = new List<string>();
        CalendarItemEventArgs? opened = null;
        var cut = Render<MusselCalendarView>(parameters => parameters
            .Add(static p => p.Date, Thursday)
            .Add(static p => p.ReadOnly, true)
            .Add(static p => p.Items, new[] { Item("a", 9, 10) })
            .Add(static p => p.RangeSelected, _ => raised.Add("select"))
            .Add(static p => p.ItemChanged, _ => raised.Add("change"))
            .Add(static p => p.ItemClicked, e => opened = e));

        await cut.InvokeAsync(() => new CalendarScriptBridge(cut.Instance).OnSelectAsync("2026-11-12T09:00:00", "2026-11-12T10:00:00", false, 0, 0, 0, 0));
        await cut.InvokeAsync(() => new CalendarScriptBridge(cut.Instance).OnMoveAsync("a", "2026-11-13T12:00:00", "2026-11-13T13:00:00", false));
        await cut.InvokeAsync(() => new CalendarScriptBridge(cut.Instance).OnShiftAsync("a", 1, 0));
        await cut.InvokeAsync(() => new CalendarScriptBridge(cut.Instance).OnExtendAsync("a", 15));
        await cut.InvokeAsync(() => new CalendarScriptBridge(cut.Instance).OnItemClickAsync("a", 10, 20, 30, 40));

        Assert.Empty(raised);
        Assert.Equal("a", opened?.Item.Id);
        Assert.Equal(new CalendarAnchor(10, 20, 30, 40), opened?.Anchor);
        Assert.Equal("true", cut.Find(".mussel-calendar").GetAttribute("data-read-only"));
        Assert.Equal("true", cut.Find(".mc-event").GetAttribute("data-read-only"));
        Assert.Empty(cut.FindAll(".mc-resize"));
    }

    [Fact]
    public void TodayDateIsCurrent()
    {
        var clock = new ManualTimeProvider { Now = new DateTimeOffset(2026, 11, 12, 10, 0, 0, TimeSpan.Zero) };
        var cut = Render<MusselCalendarView>(parameters => parameters
            .Add(static p => p.Date, Thursday)
            .Add(static p => p.TimeProvider, clock));

        var current = cut.FindAll("[aria-current='date']");
        Assert.Single(current);
        Assert.Equal("12", current[0].TextContent);
    }

    [Fact]
    public void SecondaryOffsetAddsSecondTimeColumn()
    {
        var cut = Render<MusselCalendarView>(static parameters => parameters
            .Add(static p => p.Date, Thursday)
            .Add(static p => p.Culture, CultureInfo.InvariantCulture)
            .Add(static p => p.SecondaryOffset, TimeSpan.FromHours(-9.5))
            .Add(static p => p.TimeZoneText, "JST")
            .Add(static p => p.SecondaryTimeZoneText, "UTC"));

        Assert.Contains("mc-dual", cut.Find(".mc-timegrid").ClassList);
        Assert.Equal(["UTC", "JST"], cut.FindAll(".mc-zones span").Select(static x => x.TextContent));
        var secondary = cut.FindAll(".mc-time.mc-secondary");
        Assert.Equal(23, secondary.Count);
        Assert.Equal("15:30", secondary[0].TextContent);
        Assert.Equal("0:30", secondary[9].TextContent);
    }

    [Fact]
    public void SecondaryOffsetWithMinutesUsesTimeFormat()
    {
        var cut = Render<MusselCalendarView>(static parameters => parameters
            .Add(static p => p.Date, Thursday)
            .Add(static p => p.Culture, CultureInfo.GetCultureInfo("en-US"))
            .Add(static p => p.HourFormat, "h tt")
            .Add(static p => p.TimeFormat, "h:mm tt")
            .Add(static p => p.SecondaryOffset, new TimeSpan(5, 30, 0)));

        Assert.Equal("1 AM", cut.FindAll(".mc-time:not(.mc-secondary)")[0].TextContent);
        Assert.Equal("6:30 AM", cut.FindAll(".mc-time.mc-secondary")[0].TextContent);
    }

    [Fact]
    public void YearUsesGregorianMonthNames()
    {
        var cut = Render<MusselCalendarView>(static parameters => parameters
            .Add(static p => p.View, CalendarViewKind.Year)
            .Add(static p => p.Date, Thursday)
            .Add(static p => p.Culture, CultureInfo.GetCultureInfo("fa-IR")));

        Assert.Equal("ژانویه", cut.FindAll(".mc-year-title")[0].TextContent);
    }

    [Fact]
    public async Task YearRedrawsOnlyWhenTheDayChanges()
    {
        var clock = new ManualTimeProvider { Now = new DateTimeOffset(2026, 11, 12, 10, 0, 0, TimeSpan.Zero) };
        var cut = Render<MusselCalendarView>(parameters => parameters
            .Add(static p => p.View, CalendarViewKind.Year)
            .Add(static p => p.Date, Thursday)
            .Add(static p => p.TimeProvider, clock));
        var renders = cut.RenderCount;

        clock.Now = clock.Now.AddMinutes(1);
        clock.Fire();
        await cut.InvokeAsync(static () => { });
        Assert.Equal(renders, cut.RenderCount);

        clock.Now = clock.Now.AddDays(1);
        clock.Fire();

        await cut.WaitForAssertionAsync(() => Assert.Equal("2026-11-13", cut.Find(".mc-year-day.mc-today").GetAttribute("data-date")));
    }

    [Fact]
    public void NoSecondaryOffsetKeepsOneTimeColumn()
    {
        var cut = Render<MusselCalendarView>(static parameters => parameters
            .Add(static p => p.Date, Thursday)
            .Add(static p => p.TimeZoneText, "JST"));

        Assert.DoesNotContain("mc-dual", cut.Find(".mc-timegrid").ClassList);
        Assert.Empty(cut.FindAll(".mc-zones"));
        Assert.Empty(cut.FindAll(".mc-time.mc-secondary"));
    }

    [Theory]
    [InlineData("en-US", "12/1")]
    [InlineData("ja-JP", "12/1")]
    [InlineData("en-GB", "1/12")]
    [InlineData("de-DE", "1.12")]
    public void TheFirstDayOfAMonthFollowsTheOrderOfTheCulture(string culture, string expected)
    {
        var cut = Render<MusselCalendarView>(parameters => parameters
            .Add(static p => p.View, CalendarViewKind.Month)
            .Add(static p => p.Date, new DateOnly(2026, 12, 15))
            .Add(static p => p.Culture, CultureInfo.GetCultureInfo(culture)));

        Assert.Contains(expected, cut.FindAll(".mc-date").Select(static x => x.TextContent));
    }

    [Fact]
    public async Task AnExceptionInAHandlerCalledFromTheScriptReachesTheRenderer()
    {
        var cut = Render<MusselCalendarView>(parameters => parameters
            .Add(static p => p.Date, Thursday)
            .Add(static p => p.Items, new[] { Item("a", 9, 10) })
            .Add(static p => p.ItemClicked, static _ => throw new InvalidOperationException("app")));

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => cut.InvokeAsync(() => new CalendarScriptBridge(cut.Instance).OnItemClickAsync("a", 0, 0, 0, 0)));

        Assert.True(Renderer.UnhandledException.IsCompleted);
        Assert.Same(thrown, await Renderer.UnhandledException.ConfigureAwait(true));
    }

    [Fact]
    public async Task CanceledHandlersAndLostConnectionsDoNotReachTheRenderer()
    {
        var cut = Render<MusselCalendarView>(parameters => parameters
            .Add(static p => p.Date, Thursday)
            .Add(static p => p.Items, new[] { Item("a", 9, 10) })
            .Add(static p => p.ItemClicked, static _ => throw new OperationCanceledException())
            .Add(static p => p.DayClicked, static _ => throw new JSDisconnectedException("gone")));

        var bridge = new CalendarScriptBridge(cut.Instance);
        await Assert.ThrowsAsync<OperationCanceledException>(() => cut.InvokeAsync(() => bridge.OnItemClickAsync("a", 0, 0, 0, 0)));
        await Assert.ThrowsAsync<JSDisconnectedException>(() => cut.InvokeAsync(() => bridge.OnDayClickAsync("2026-11-12")));

        Assert.False(Renderer.UnhandledException.IsCompleted);
    }

    [Fact]
    public async Task CallsFromTheScriptAfterTheViewIsGoneDoNothing()
    {
        var clicked = 0;
        var cut = Render<MusselCalendarView>(parameters => parameters
            .Add(static p => p.Date, Thursday)
            .Add(static p => p.Items, new[] { Item("a", 9, 10) })
            .Add(static p => p.ItemClicked, _ => clicked++));
        var bridge = new CalendarScriptBridge(cut.Instance);
        await cut.InvokeAsync(() => cut.Instance.DisposeAsync().AsTask());

        await cut.InvokeAsync(() => bridge.OnItemClickAsync("a", 0, 0, 0, 0));

        Assert.Equal(0, clicked);
    }

    [Fact]
    public async Task AFailureAfterTheViewIsGoneDoesNotReachTheRenderer()
    {
        var pending = new TaskCompletionSource();
        var cut = Render<MusselCalendarView>(parameters => parameters
            .Add(static p => p.Date, Thursday)
            .Add(static p => p.Items, new[] { Item("a", 9, 10) })
            .Add(static p => p.ItemClicked, async _ =>
            {
                await pending.Task.ConfigureAwait(true);
                throw new InvalidOperationException("late");
            }));

        var clicked = cut.InvokeAsync(() => new CalendarScriptBridge(cut.Instance).OnItemClickAsync("a", 0, 0, 0, 0));
        await cut.InvokeAsync(() => cut.Instance.DisposeAsync().AsTask());
        pending.SetResult();

        await Assert.ThrowsAsync<InvalidOperationException>(() => clicked);

        Assert.False(Renderer.UnhandledException.IsCompleted);
    }

    [Theory]
    [InlineData(CalendarViewKind.Month)]
    [InlineData(CalendarViewKind.Week)]
    [InlineData(CalendarViewKind.Schedule)]
    public void ItemsThatShareAnIdAreStillDrawn(CalendarViewKind view)
    {
        var day = Thursday.ToDateTime(TimeOnly.MinValue);
        var timed = Item("c", 13, 14);
        var allDay = new CalendarItem { Id = "d", Title = "d", Start = day.AddDays(1), End = day.AddDays(2), AllDay = true };
        var cut = Render<MusselCalendarView>(parameters => parameters
            .Add(static p => p.Date, Thursday)
            .Add(static p => p.View, view)
            .Add(static p => p.MonthLanes, 6)
            .Add(static p => p.Items, new[] { timed, allDay }));

        cut.Render(parameters => parameters.Add(static p => p.Items, new[]
        {
            Item("a", 9, 10),
            Item("a", 11, 12),
            new CalendarItem { Id = "b", Title = "b", Start = day, End = day.AddDays(1), AllDay = true },
            new CalendarItem { Id = "b", Title = "b", Start = day, End = day.AddDays(1), AllDay = true },
            timed,
            allDay
        }));

        Assert.NotEmpty(cut.FindAll("[data-id='a']"));
        Assert.NotEmpty(cut.FindAll("[data-id='b']"));
    }

    [Fact]
    public void ColorsWithAnAlphaAreKeptAndUrlsAreLeftOut()
    {
        string[] colors = ["rgb(87 148 242 / 80%)", "var(--brand)", "url(//example.com/x.png)", new('a', 65), new('a', 64), string.Empty];
        var items = new CalendarItem[colors.Length];
        for (var i = 0; i < colors.Length; i++)
        {
            items[i] = Item($"c{i}", 9 + i, 10 + i);
            items[i].Color = colors[i];
        }

        var cut = Render<MusselCalendarView>(parameters => parameters
            .Add(static p => p.Date, Thursday)
            .Add(static p => p.Items, items));

        Assert.Contains("--mc-color: rgb(87 148 242 / 80%);", StyleOf(0), StringComparison.Ordinal);
        Assert.Contains("--mc-color: var(--brand);", StyleOf(1), StringComparison.Ordinal);
        Assert.DoesNotContain("--mc-color", StyleOf(2), StringComparison.Ordinal);
        Assert.DoesNotContain("--mc-color", StyleOf(3), StringComparison.Ordinal);
        Assert.Contains($"--mc-color: {colors[4]};", StyleOf(4), StringComparison.Ordinal);
        Assert.DoesNotContain("--mc-color", StyleOf(5), StringComparison.Ordinal);

        string StyleOf(int index) => cut.Find($".mc-event[data-id='c{index}']").GetAttribute("style") ?? string.Empty;
    }

    [Fact]
    public void ACultureWithAnotherCalendarIsCopiedOnce()
    {
        var cut = Render<MusselCalendarView>(parameters => parameters
            .Add(static p => p.Date, Thursday)
            .Add(static p => p.Culture, CultureInfo.GetCultureInfo("th-TH")));
        var first = cut.Instance.FormatCulture;

        cut.Render(static parameters => parameters.Add(static p => p.Date, Thursday.AddDays(1)));

        Assert.Same(first, cut.Instance.FormatCulture);
        Assert.IsType<GregorianCalendar>(first.DateTimeFormat.Calendar);
    }

    [Fact]
    public async Task TimesThatCannotBeReadAreIgnored()
    {
        var raised = new List<string>();
        var cut = Render<MusselCalendarView>(parameters => parameters
            .Add(static p => p.Date, Thursday)
            .Add(static p => p.Items, new[] { Item("a", 9, 10) })
            .Add(static p => p.RangeSelected, _ => raised.Add("select"))
            .Add(static p => p.ItemChanged, _ => raised.Add("change")));

        await cut.InvokeAsync(() => new CalendarScriptBridge(cut.Instance).OnSelectAsync("1-01-01T10:00:00", "1-01-01T11:00:00", false, 0, 0, 0, 0));
        await cut.InvokeAsync(() => new CalendarScriptBridge(cut.Instance).OnMoveAsync("a", "2026-11-13T12:00:00", "2026-11-13T25:00:00", false));

        Assert.Empty(raised);
        Assert.False(Renderer.UnhandledException.IsCompleted);
    }

    [Theory]
    [InlineData(CalendarViewKind.Day)]
    [InlineData(CalendarViewKind.Days)]
    [InlineData(CalendarViewKind.Week)]
    [InlineData(CalendarViewKind.Month)]
    [InlineData(CalendarViewKind.Schedule)]
    [InlineData(CalendarViewKind.Year)]
    public void EveryViewDrawsAtTheEndOfTheCalendar(CalendarViewKind view)
    {
        var cut = Render<MusselCalendarView>(parameters => parameters
            .Add(static p => p.View, view)
            .Add(static p => p.Date, DateOnly.MaxValue));

        Assert.Single(cut.FindAll(".mussel-calendar"));
    }

    [Fact]
    public void TheLastMonthOfTheMonthViewIsNovember()
    {
        var cut = Render<MusselCalendarView>(parameters => parameters
            .Add(static p => p.View, CalendarViewKind.Month)
            .Add(static p => p.Date, DateOnly.MaxValue));

        Assert.False(cut.Find(".mc-month-cell[data-date='9999-11-30']").ClassList.Contains("mc-outside"));
        Assert.True(cut.Find(".mc-month-cell[data-date='9999-12-01']").ClassList.Contains("mc-outside"));
    }

    [Fact]
    public void TheFirstYearKeepsTheColumnsOfTheWeek()
    {
        var cut = Render<MusselCalendarView>(parameters => parameters
            .Add(static p => p.View, CalendarViewKind.Year)
            .Add(static p => p.Date, DateOnly.MinValue)
            .Add(static p => p.Culture, CultureInfo.InvariantCulture));

        var january = cut.FindAll(".mc-year-grid")[0];
        Assert.Equal("Su", january.QuerySelector("th")?.TextContent);
        Assert.Equal([null, "0001-01-01"], january.QuerySelectorAll("tbody td").Take(2).Select(static x => x.GetAttribute("data-date")));
    }

    [Fact]
    public void ItemsThatNeverEndAreShown()
    {
        var item = new CalendarItem { Id = "a", Title = "a", Start = Thursday.ToDateTime(new TimeOnly(9, 0)), End = DateTime.MaxValue };
        var cut = Render<MusselCalendarView>(parameters => parameters
            .Add(static p => p.View, CalendarViewKind.Month)
            .Add(static p => p.Date, Thursday)
            .Add(static p => p.Items, new[] { item }));

        Assert.NotEmpty(cut.FindAll("[data-id='a']"));
    }

    [Fact]
    public void ItemsThatStartAtTheEndOfTimeDoNotStopTheWeek()
    {
        CalendarItem[] items =
        [
            new() { Id = "timed", Title = "timed", Start = DateTime.MaxValue, End = DateTime.MaxValue },
            new() { Id = "allDay", Title = "allDay", Start = DateTime.MaxValue, End = DateTime.MaxValue, AllDay = true },
            Item("b", 9, 10)
        ];
        var cut = Render<MusselCalendarView>(parameters => parameters
            .Add(static p => p.View, CalendarViewKind.Week)
            .Add(static p => p.Date, Thursday)
            .Add(static p => p.Items, items));

        Assert.NotEmpty(cut.FindAll("[data-id='b']"));
    }

    [Fact]
    public async Task ShiftsAndExtensionsPastTheEndsOfTimeDoNothing()
    {
        var late = new CalendarItem { Id = "late", Title = "late", Start = Thursday.ToDateTime(new TimeOnly(9, 0)), End = DateTime.MaxValue };
        var early = new CalendarItem { Id = "early", Title = "early", Start = DateTime.MinValue, End = DateTime.MinValue.AddHours(1) };
        var changes = new List<CalendarItemChangeEventArgs>();
        var cut = Render<MusselCalendarView>(parameters => parameters
            .Add(static p => p.Date, Thursday)
            .Add(static p => p.Items, new[] { late, early })
            .Add(static p => p.ItemChanged, changes.Add));

        await cut.InvokeAsync(() => new CalendarScriptBridge(cut.Instance).OnShiftAsync("late", 1, 0));
        await cut.InvokeAsync(() => new CalendarScriptBridge(cut.Instance).OnExtendAsync("late", 15));
        await cut.InvokeAsync(() => new CalendarScriptBridge(cut.Instance).OnShiftAsync("early", 0, -30));
        await cut.InvokeAsync(() => new CalendarScriptBridge(cut.Instance).OnShiftAsync("early", -1, 0));
        await cut.InvokeAsync(() => new CalendarScriptBridge(cut.Instance).OnShiftAsync("early", Int32.MaxValue, 0));

        Assert.Empty(changes);
        Assert.False(Renderer.UnhandledException.IsCompleted);
    }

    private static CalendarItem Item(string id, double startHour, double endHour)
    {
        var start = Thursday.ToDateTime(TimeOnly.MinValue);
        return new CalendarItem { Id = id, Title = id, Start = start.AddHours(startHour), End = start.AddHours(endHour) };
    }
}
