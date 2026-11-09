namespace MusselCalendar;

using MusselCalendar.Internal;

public sealed class CalendarLayoutTests
{
    private static readonly DateOnly Monday = new(2026, 11, 9);

    [Fact]
    public void TimedItemEndingAtMidnightStaysOnOneDay()
    {
        var item = Timed("a", Monday, 22, 24);

        Assert.Equal(1, CalendarLayout.DayCount(item));
        Assert.False(CalendarLayout.InAllDayLane(item));
    }

    [Fact]
    public void ZeroLengthAndReversedItemsStayOnTheirStartDay()
    {
        var start = Monday.ToDateTime(TimeOnly.MinValue);
        CalendarItem[] items =
        [
            new() { Id = "zero", Title = "zero", Start = start, End = start },
            new() { Id = "reversed", Title = "reversed", Start = start.AddHours(10), End = start.AddHours(-2) }
        ];

        var segments = CalendarLayout.BuildTimed(items, Monday.AddDays(-1), 2, 15, CalendarOverlapMode.Columns);

        Assert.Equal((1, 0, 15), segments.Where(static x => x.Item.Id == "zero").Select(static x => (x.Day, x.StartMinute, x.EndMinute)).Single());
        Assert.Equal((1, 600, 615), segments.Where(static x => x.Item.Id == "reversed").Select(static x => (x.Day, x.StartMinute, x.EndMinute)).Single());
    }

    [Fact]
    public void LongTimedItemGoesToAllDayLane()
    {
        Assert.True(CalendarLayout.InAllDayLane(Timed("a", Monday, 9, 33)));
        Assert.True(CalendarLayout.InAllDayLane(AllDay("b", Monday, 1)));
    }

    [Fact]
    public void ItemsStartingTogetherShareTheWidth()
    {
        var segments = CalendarLayout.BuildTimed([Timed("a", Monday, 10, 11), Timed("b", Monday, 10, 12)], Monday, 1, 15, CalendarOverlapMode.Columns);

        var a = segments.Single(x => x.Item.Id == "a");
        var b = segments.Single(x => x.Item.Id == "b");
        Assert.Equal((0.5, 0.5), (a.Left, a.Width));
        Assert.Equal((0, 0.5), (b.Left, b.Width));
    }

    [Fact]
    public void ItemExpandsToTheRightUntilACollision()
    {
        var segments = CalendarLayout.BuildTimed(
            [Timed("a", Monday, 10, 11), Timed("b", Monday, 10.5, 11.5), Timed("c", Monday, 11, 12), Timed("d", Monday, 14, 15)],
            Monday,
            1,
            15,
            CalendarOverlapMode.Columns);

        Assert.Equal((0, 0.5), Position(segments, "a"));
        Assert.Equal((0.5, 0.5), Position(segments, "b"));
        Assert.Equal((0, 0.5), Position(segments, "c"));
        Assert.Equal((0, 1), Position(segments, "d"));
    }

    [Fact]
    public void StackModeWidensItemsUnderLaterOnes()
    {
        var segments = CalendarLayout.BuildTimed([Timed("a", Monday, 10, 12), Timed("b", Monday, 10.5, 11)], Monday, 1, 15, CalendarOverlapMode.Stack);

        var a = segments.Single(x => x.Item.Id == "a");
        var b = segments.Single(x => x.Item.Id == "b");
        Assert.Equal(0.85, a.Width, 3);
        Assert.True(b.Layer > a.Layer);
    }

    [Fact]
    public void ItemCrossingMidnightIsSplitByDay()
    {
        var segments = CalendarLayout.BuildTimed([Timed("a", Monday, 22, 26)], Monday, 2, 15, CalendarOverlapMode.Columns);

        Assert.Collection(
            segments,
            static x => Assert.Equal((0, 1320, 1440, true, false), (x.Day, x.StartMinute, x.EndMinute, x.IsStart, x.IsEnd)),
            static x => Assert.Equal((1, 0, 120, false, true), (x.Day, x.StartMinute, x.EndMinute, x.IsStart, x.IsEnd)));
    }

    [Fact]
    public void BarsAreStackedInLanesAndClippedToTheRange()
    {
        var bars = CalendarLayout.BuildBars([AllDay("a", Monday.AddDays(-2), 4), AllDay("b", Monday.AddDays(1), 2), AllDay("c", Monday.AddDays(5), 4)], Monday, 7, out var lanes);

        Assert.Equal(2, lanes);
        Assert.Equal((0, 2, 0, false, true), Bar(bars, "a"));
        Assert.Equal((1, 2, 1, true, true), Bar(bars, "b"));
        Assert.Equal((5, 2, 0, true, false), Bar(bars, "c"));
    }

    [Fact]
    public void MonthWeekCollapsesTheLastLaneIntoMore()
    {
        var wednesday = Monday.AddDays(2);
        var week = CalendarLayout.BuildMonthWeek(
            [Timed("a", wednesday, 9, 10), Timed("b", wednesday, 10, 11), Timed("c", wednesday, 11, 12), Timed("d", wednesday, 12, 13), AllDay("e", Monday.AddDays(4), 1)],
            Monday.AddDays(-1),
            3);

        Assert.Equal(2, week.Bars.Count(x => x.Column == 3));
        Assert.Equal(2, week.More[3]);
        Assert.Single(week.Bars, x => x.Item.Id == "e");
        Assert.Equal(0, week.More[5]);
    }

    [Fact]
    public void BarAcrossAnOverflowingDayIsHiddenOnAllItsDays()
    {
        var week = CalendarLayout.BuildMonthWeek(
            [AllDay("a1", Monday, 2), AllDay("a2", Monday, 2), AllDay("a3", Monday, 2), AllDay("long", Monday.AddDays(1), 2)],
            Monday,
            3);

        Assert.Equal(["a1", "a2"], week.Bars.Select(static x => x.Item.Id).Order(StringComparer.Ordinal));
        Assert.Equal(1, week.More[0]);
        Assert.Equal(2, week.More[1]);
        Assert.Equal(1, week.More[2]);
    }

    [Fact]
    public void CountDaysStaysInRange()
    {
        var range = new CalendarRange(new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 1));
        CalendarItem[] items =
        [
            AllDay("trip", new DateOnly(2025, 12, 30), 4),
            Timed("meeting", new DateOnly(2026, 1, 2), 9, 10),
            AllDay("late", new DateOnly(2026, 12, 31), 3)
        ];

        var counts = CalendarLayout.CountDays(items, range);

        Assert.Equal(
            [(new DateOnly(2026, 1, 1), 1), (new DateOnly(2026, 1, 2), 2), (new DateOnly(2026, 12, 31), 1)],
            counts.OrderBy(static x => x.Key).Select(static x => (x.Key, x.Value)));
    }

    private static (double Left, double Width) Position(IEnumerable<TimedSegment> segments, string id)
    {
        var segment = segments.Single(x => x.Item.Id == id);
        return (segment.Left, segment.Width);
    }

    private static (int Column, int Span, int Lane, bool IsStart, bool IsEnd) Bar(IEnumerable<BarSegment> bars, string id)
    {
        var bar = bars.Single(x => x.Item.Id == id);
        return (bar.Column, bar.Span, bar.Lane, bar.IsStart, bar.IsEnd);
    }

    private static CalendarItem Timed(string id, DateOnly day, double startHour, double endHour)
    {
        var start = day.ToDateTime(TimeOnly.MinValue);
        return new CalendarItem { Id = id, Title = id, Start = start.AddHours(startHour), End = start.AddHours(endHour) };
    }

    private static CalendarItem AllDay(string id, DateOnly day, int days)
    {
        var start = day.ToDateTime(TimeOnly.MinValue);
        return new CalendarItem { Id = id, Title = id, Start = start, End = start.AddDays(days), AllDay = true };
    }
}
