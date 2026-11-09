namespace MusselCalendar;

public sealed class CalendarRangeTests
{
    [Theory]
    [InlineData(DayOfWeek.Sunday, "2026-11-08")]
    [InlineData(DayOfWeek.Monday, "2026-11-09")]
    public void WeekStartsOnFirstDayOfWeek(DayOfWeek firstDayOfWeek, string expected)
    {
        var range = CalendarRange.Of(CalendarViewKind.Week, new DateOnly(2026, 11, 12), firstDayOfWeek, 4);

        Assert.Equal(DateOnly.Parse(expected, CultureInfo.InvariantCulture), range.Start);
        Assert.Equal(7, range.Days);
    }

    [Theory]
    [InlineData(DayOfWeek.Sunday, "2026-11-01", 35)]
    [InlineData(DayOfWeek.Monday, "2026-10-26", 42)]
    public void MonthCoversWholeWeeks(DayOfWeek firstDayOfWeek, string start, int days)
    {
        var range = CalendarRange.Of(CalendarViewKind.Month, new DateOnly(2026, 11, 20), firstDayOfWeek, 4);

        Assert.Equal(DateOnly.Parse(start, CultureInfo.InvariantCulture), range.Start);
        Assert.Equal(days, range.Days);
    }

    [Fact]
    public void DaysViewStartsOnDate()
    {
        var range = CalendarRange.Of(CalendarViewKind.Days, new DateOnly(2026, 11, 12), DayOfWeek.Sunday, 3);

        Assert.Equal(new DateOnly(2026, 11, 12), range.Start);
        Assert.Equal(new DateOnly(2026, 11, 15), range.End);
        Assert.True(range.Contains(new DateOnly(2026, 11, 14)));
        Assert.False(range.Contains(new DateOnly(2026, 11, 15)));
    }

    [Theory]
    [InlineData(CalendarViewKind.Day, 1, "2026-11-13")]
    [InlineData(CalendarViewKind.Days, -1, "2026-11-08")]
    [InlineData(CalendarViewKind.Week, 1, "2026-11-19")]
    [InlineData(CalendarViewKind.Month, 1, "2026-12-12")]
    public void MoveStepsByView(CalendarViewKind view, int direction, string expected)
    {
        Assert.Equal(DateOnly.Parse(expected, CultureInfo.InvariantCulture), CalendarRange.Move(view, new DateOnly(2026, 11, 12), direction, 4));
    }
}
