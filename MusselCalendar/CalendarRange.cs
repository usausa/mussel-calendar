namespace MusselCalendar;

public readonly struct CalendarRange : IEquatable<CalendarRange>
{
    private const int ScheduleDays = 30;

    public CalendarRange(DateOnly start, DateOnly until)
    {
        Start = start;
        End = until;
    }

    public DateOnly Start { get; }

    public DateOnly End { get; }

    public int Days => End.DayNumber - Start.DayNumber;

    public DateTime StartTime => Start.ToDateTime(TimeOnly.MinValue);

    public DateTime EndTime => End.ToDateTime(TimeOnly.MinValue);

    public static bool operator ==(CalendarRange left, CalendarRange right) => left.Equals(right);

    public static bool operator !=(CalendarRange left, CalendarRange right) => !left.Equals(right);

    public static CalendarRange Of(CalendarViewKind view, DateOnly day, DayOfWeek firstDayOfWeek, int dayCount) =>
        view switch
        {
            CalendarViewKind.Day => new CalendarRange(day, day.AddDays(1)),
            CalendarViewKind.Days => new CalendarRange(day, day.AddDays(Math.Max(dayCount, 1))),
            CalendarViewKind.Week => new CalendarRange(StartOfWeek(day, firstDayOfWeek), StartOfWeek(day, firstDayOfWeek).AddDays(7)),
            CalendarViewKind.Schedule => new CalendarRange(day, day.AddDays(ScheduleDays)),
            CalendarViewKind.Year => new CalendarRange(new DateOnly(day.Year, 1, 1), new DateOnly(day.Year, 1, 1).AddYears(1)),
            _ => OfMonth(day, firstDayOfWeek)
        };

    public static DateOnly Move(CalendarViewKind view, DateOnly day, int direction, int dayCount) =>
        view switch
        {
            CalendarViewKind.Day => day.AddDays(direction),
            CalendarViewKind.Days => day.AddDays(direction * Math.Max(dayCount, 1)),
            CalendarViewKind.Week => day.AddDays(direction * 7),
            CalendarViewKind.Schedule => day.AddDays(direction * ScheduleDays),
            CalendarViewKind.Year => day.AddYears(direction),
            _ => day.AddMonths(direction)
        };

    public static DateOnly StartOfWeek(DateOnly day, DayOfWeek firstDayOfWeek) =>
        day.AddDays(-(((int)day.DayOfWeek - (int)firstDayOfWeek + 7) % 7));

    public bool Contains(DateOnly day) => (day >= Start) && (day < End);

    public bool Equals(CalendarRange other) => (Start == other.Start) && (End == other.End);

    public override bool Equals(object? obj) => (obj is CalendarRange other) && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Start, End);

    private static CalendarRange OfMonth(DateOnly day, DayOfWeek firstDayOfWeek)
    {
        var first = new DateOnly(day.Year, day.Month, 1);
        var start = StartOfWeek(first, firstDayOfWeek);
        var weeks = (first.AddMonths(1).DayNumber - start.DayNumber + 6) / 7;
        return new CalendarRange(start, start.AddDays(weeks * 7));
    }
}
