namespace Example.Client.Components.Pages;

public sealed class SampleCalendar
{
    public SampleCalendar(string name, string color, bool readOnly)
    {
        Name = name;
        Color = color;
        ReadOnly = readOnly;
    }

    public string Name { get; }

    public string Color { get; }

    public bool ReadOnly { get; }

    public bool Visible { get; set; } = true;
}

public static class SampleData
{
    public static IReadOnlyList<SampleCalendar> CreateCalendars() =>
    [
        new("仕事", "#3274D9", false),
        new("個人", "#56A64B", false),
        new("祝日", "#E02F44", true)
    ];

    public static IEnumerable<(CalendarItem Item, SampleCalendar Calendar)> CreateItems(IReadOnlyList<SampleCalendar> calendars, DateOnly today)
    {
        var work = calendars[0];
        var home = calendars[1];
        var holiday = calendars[2];
        var week = CalendarRange.StartOfWeek(today, DayOfWeek.Sunday);
        var builder = new Builder(week);

        yield return builder.Timed(work, "定例会議", 1, 10, 11);
        yield return builder.Timed(work, "設計レビュー", 1, 10.5, 12);
        yield return builder.Timed(work, "1on1", 1, 11, 11.5);
        yield return builder.Timed(work, "朝会", 1, 9, 9.25);
        yield return builder.Timed(home, "ランチ", 2, 12, 13);
        yield return builder.Timed(work, "研修", 2, 9, 33);
        yield return builder.AllDay(work, "出張", 3, 3, false);
        yield return builder.Timed(work, "夜間作業", 4, 22, 26);
        yield return builder.Timed(home, "ジム", 4, 19, 20.5);
        yield return builder.AllDay(home, "誕生日", 6, 1, false);
        yield return builder.AllDay(holiday, "祝日(読み取り専用)", 8, 1, true);
        for (var i = 0; i < 5; i++)
        {
            yield return builder.Timed(i % 2 == 0 ? work : home, $"予定 {i + 1}", 10, 9 + (i * 2), 10 + (i * 2));
        }

        yield return builder.AllDay(home, "旅行", 12, 4, false);
        yield return builder.Timed(work, "締め切り", -3, 17, 18);
    }

    private sealed class Builder
    {
        private readonly DateOnly week;

        private int number;

        public Builder(DateOnly week)
        {
            this.week = week;
        }

        public (CalendarItem Item, SampleCalendar Calendar) Timed(SampleCalendar calendar, string title, int day, double startHour, double endHour)
        {
            var start = week.AddDays(day).ToDateTime(TimeOnly.MinValue);
            return (new CalendarItem
            {
                Id = NextId(),
                Title = title,
                Start = start.AddHours(startHour),
                End = start.AddHours(endHour),
                Color = calendar.Color,
                ReadOnly = calendar.ReadOnly
            }, calendar);
        }

        public (CalendarItem Item, SampleCalendar Calendar) AllDay(SampleCalendar calendar, string title, int day, int days, bool readOnly)
        {
            var start = week.AddDays(day).ToDateTime(TimeOnly.MinValue);
            return (new CalendarItem
            {
                Id = NextId(),
                Title = title,
                Start = start,
                End = start.AddDays(days),
                AllDay = true,
                Color = calendar.Color,
                ReadOnly = readOnly || calendar.ReadOnly
            }, calendar);
        }

        private string NextId() => (++number).ToString(CultureInfo.InvariantCulture);
    }
}
