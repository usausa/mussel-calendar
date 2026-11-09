namespace MusselCalendar;

public sealed class MusselCalendarViewTests : BunitContext
{
    private const string ModulePath = "./_content/MusselCalendar/MusselCalendar.js";

    private static readonly DateOnly Thursday = new(2026, 11, 12);

    public MusselCalendarViewTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupModule(ModulePath);
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

        await cut.InvokeAsync(() => cut.Instance.OnMove("a", "2026-11-13T12:00:00", "2026-11-13T13:00:00", false));

        Assert.NotNull(changed);
        Assert.Same(item, changed.Item);
        Assert.Equal((new DateTime(2026, 11, 13, 12, 0, 0), new DateTime(2026, 11, 13, 13, 0, 0), false), (changed.Start, changed.End, changed.AllDay));
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

        await cut.InvokeAsync(() => cut.Instance.OnShift("a", 1, 30));
        await cut.InvokeAsync(() => cut.Instance.OnExtend("a", 15));
        await cut.InvokeAsync(() => cut.Instance.OnExtend("a", -60));

        Assert.Equal(2, changes.Count);
        Assert.Equal((new DateTime(2026, 11, 13, 9, 30, 0), new DateTime(2026, 11, 13, 10, 30, 0)), (changes[0].Start, changes[0].End));
        Assert.Equal((item.Start, item.End.AddMinutes(15)), (changes[1].Start, changes[1].End));
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

        await cut.InvokeAsync(() => cut.Instance.OnMove("a", "2026-11-13T12:00:00", "2026-11-13T13:00:00", false));
        await cut.InvokeAsync(() => cut.Instance.OnShift("a", 1, 0));

        Assert.False(raised);
    }

    [Fact]
    public async Task SelectRaisesRangeSelected()
    {
        CalendarSelectEventArgs? selected = null;
        var cut = Render<MusselCalendarView>(parameters => parameters
            .Add(static p => p.Date, Thursday)
            .Add(static p => p.RangeSelected, e => selected = e));

        await cut.InvokeAsync(() => cut.Instance.OnSelect("2026-11-12T00:00:00", "2026-11-14T00:00:00", true, 10, 20, 30, 40));

        Assert.NotNull(selected);
        Assert.Equal((new DateTime(2026, 11, 12), new DateTime(2026, 11, 14), true), (selected.Start, selected.End, selected.AllDay));
        Assert.Equal(new CalendarAnchor(10, 20, 30, 40), selected.Anchor);
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

        await cut.InvokeAsync(() => cut.Instance.OnMonthLanes(6));

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

        await cut.InvokeAsync(() => cut.Instance.OnMoreClick("2026-11-12", 10, 20, 30, 40));

        Assert.NotNull(more);
        Assert.Equal(Thursday, more.Day);
        Assert.Equal(new CalendarAnchor(10, 20, 30, 40), more.Anchor);
        Assert.Equal(["empty", "a"], more.Items.Select(static x => x.Id));
    }

    [Fact]
    public void TodayMovesAtMidnight()
    {
        var clock = new ManualTimeProvider { Now = new DateTimeOffset(2026, 11, 12, 23, 59, 0, TimeSpan.Zero) };
        var cut = Render<MusselCalendarView>(parameters => parameters
            .Add(static p => p.Date, Thursday)
            .Add(static p => p.TimeProvider, clock));
        Assert.Equal("2026-11-12", cut.Find(".mc-now").ParentElement?.GetAttribute("data-date"));

        clock.Now = clock.Now.AddMinutes(2);
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

        await cut.InvokeAsync(() => cut.Instance.OnMonthLanes(6));

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
        var marked = cut.FindAll(".mc-year-day.mc-marked");
        Assert.Equal(3, marked.Count);
        Assert.Equal("Thursday, 12 November 2026, 2 items", marked[0].GetAttribute("aria-label"));

        marked[1].Click();

        Assert.Equal(new DateOnly(2026, 11, 13), clicked);
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
    public void NoSecondaryOffsetKeepsOneTimeColumn()
    {
        var cut = Render<MusselCalendarView>(static parameters => parameters
            .Add(static p => p.Date, Thursday)
            .Add(static p => p.TimeZoneText, "JST"));

        Assert.DoesNotContain("mc-dual", cut.Find(".mc-timegrid").ClassList);
        Assert.Empty(cut.FindAll(".mc-zones"));
        Assert.Empty(cut.FindAll(".mc-time.mc-secondary"));
    }

    private static CalendarItem Item(string id, double startHour, double endHour)
    {
        var start = Thursday.ToDateTime(TimeOnly.MinValue);
        return new CalendarItem { Id = id, Title = id, Start = start.AddHours(startHour), End = start.AddHours(endHour) };
    }
}
