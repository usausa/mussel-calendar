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

    private static CalendarItem Item(string id, double startHour, double endHour)
    {
        var start = Thursday.ToDateTime(TimeOnly.MinValue);
        return new CalendarItem { Id = id, Title = id, Start = start.AddHours(startHour), End = start.AddHours(endHour) };
    }
}
