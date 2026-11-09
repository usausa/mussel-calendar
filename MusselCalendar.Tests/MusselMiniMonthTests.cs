namespace MusselCalendar;

public sealed class MusselMiniMonthTests : BunitContext
{
    private static readonly DateOnly Thursday = new(2026, 11, 12);

    [Fact]
    public void HighlightMarksRange()
    {
        var cut = Render<MusselMiniMonth>(static parameters => parameters
            .Add(static p => p.Date, Thursday)
            .Add(static p => p.FirstDayOfWeek, DayOfWeek.Monday)
            .Add(static p => p.Highlight, CalendarRange.Of(CalendarViewKind.Week, Thursday, DayOfWeek.Monday, 1))
            .Add(static p => p.Culture, CultureInfo.InvariantCulture));

        Assert.Equal(42, cut.FindAll(".mm-day").Count);
        Assert.Equal(7, cut.FindAll(".mm-in-range").Count);
        Assert.Equal("12", cut.Find(".mm-selected").TextContent);
    }

    [Fact]
    public void TodayMovesAtMidnight()
    {
        var clock = new ManualTimeProvider { Now = new DateTimeOffset(2026, 11, 12, 23, 59, 0, TimeSpan.Zero) };
        var cut = Render<MusselMiniMonth>(parameters => parameters
            .Add(static p => p.Date, Thursday)
            .Add(static p => p.TimeProvider, clock));
        Assert.Equal("12", cut.Find(".mm-today").TextContent);

        clock.Now = clock.Now.AddMinutes(2);
        clock.Fire();

        cut.WaitForAssertion(() => Assert.Equal("13", cut.Find(".mm-today").TextContent));
    }

    [Fact]
    public void RootKeepsOwnClass()
    {
        var cut = Render<MusselMiniMonth>(static parameters => parameters
            .Add(static p => p.Date, Thursday)
            .AddUnmatched("class", "picker"));

        Assert.Equal("mussel-mini-month picker", cut.Find("div").GetAttribute("class"));
    }
}
