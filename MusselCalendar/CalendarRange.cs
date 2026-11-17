namespace MusselCalendar;

public readonly struct CalendarRange : IEquatable<CalendarRange>
{
    private const int ScheduleDays = 30;

    private static readonly DateOnly LastShownMonth = new(DateOnly.MaxValue.Year, 11, 1);

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
            CalendarViewKind.Day => OfDays(day, 1),
            CalendarViewKind.Days => OfDays(day, dayCount),
            CalendarViewKind.Week => OfWeek(day, firstDayOfWeek),
            CalendarViewKind.Schedule => OfDays(day, ScheduleDays),
            CalendarViewKind.Year => OfYear(day),
            _ => OfMonth(day, firstDayOfWeek)
        };

    public static DateOnly Move(CalendarViewKind view, DateOnly day, int direction, int dayCount) =>
        view switch
        {
            CalendarViewKind.Day => MoveDays(day, direction, 1),
            CalendarViewKind.Days => MoveDays(day, direction, Math.Clamp(dayCount, 1, DateOnly.MaxValue.DayNumber)),
            CalendarViewKind.Week => MoveDays(day, direction, 7),
            CalendarViewKind.Schedule => MoveDays(day, direction, ScheduleDays),
            CalendarViewKind.Year => MoveYears(day, direction),
            _ => MoveMonths(day, direction)
        };

    public static DateOnly StartOfWeek(DateOnly day, DayOfWeek firstDayOfWeek)
    {
        var offset = DaysIntoWeek(day, firstDayOfWeek);
        return day.DayNumber >= offset ? day.AddDays(-offset) : DateOnly.MinValue;
    }

    public bool Contains(DateOnly day) => (day >= Start) && (day < End);

    public bool Equals(CalendarRange other) => (Start == other.Start) && (End == other.End);

    public override bool Equals(object? obj) => (obj is CalendarRange other) && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Start, End);

    internal static int DaysIntoWeek(DateOnly day, DayOfWeek firstDayOfWeek) => ((int)day.DayOfWeek - (int)firstDayOfWeek + 7) % 7;

    internal static DateOnly MonthOf(DateOnly day) => day < LastShownMonth ? new DateOnly(day.Year, day.Month, 1) : LastShownMonth;

    private static CalendarRange OfDays(DateOnly day, int count)
    {
        var days = Math.Clamp(count, 1, DateOnly.MaxValue.DayNumber);
        var start = DateOnly.FromDayNumber(Math.Min(day.DayNumber, DateOnly.MaxValue.DayNumber - days));
        return new CalendarRange(start, start.AddDays(days));
    }

    private static CalendarRange OfWeek(DateOnly day, DayOfWeek firstDayOfWeek)
    {
        var start = StartOfWeek(DateOnly.FromDayNumber(Math.Min(day.DayNumber, DateOnly.MaxValue.DayNumber - 7)), firstDayOfWeek);
        return new CalendarRange(start, start.AddDays(7));
    }

    private static CalendarRange OfYear(DateOnly day)
    {
        var start = new DateOnly(Math.Min(day.Year, DateOnly.MaxValue.Year - 1), 1, 1);
        return new CalendarRange(start, start.AddYears(1));
    }

    private static CalendarRange OfMonth(DateOnly day, DayOfWeek firstDayOfWeek)
    {
        var first = MonthOf(day);
        var start = StartOfWeek(first, firstDayOfWeek);
        var weeks = (first.AddMonths(1).DayNumber - start.DayNumber + 6) / 7;
        return new CalendarRange(start, start.AddDays(weeks * 7));
    }

    private static DateOnly MoveDays(DateOnly day, int direction, int pageDays)
    {
        var last = DateOnly.MaxValue.DayNumber - pageDays;
        return DateOnly.FromDayNumber((int)Math.Clamp(Math.Min(day.DayNumber, last) + ((long)direction * pageDays), 0, last));
    }

    private static DateOnly MoveMonths(DateOnly day, int direction)
    {
        var last = ((LastShownMonth.Year - 1) * 12L) + LastShownMonth.Month - 1;
        var index = Math.Clamp(Math.Min(((day.Year - 1) * 12L) + day.Month - 1, last) + direction, 0, last);
        var year = (int)(index / 12) + 1;
        var month = (int)(index % 12) + 1;
        return new DateOnly(year, month, Math.Min(day.Day, DateTime.DaysInMonth(year, month)));
    }

    private static DateOnly MoveYears(DateOnly day, int direction)
    {
        var last = DateOnly.MaxValue.Year - 1;
        var year = (int)Math.Clamp((long)Math.Min(day.Year, last) + direction, DateOnly.MinValue.Year, last);
        return new DateOnly(year, day.Month, Math.Min(day.Day, DateTime.DaysInMonth(year, day.Month)));
    }
}
