namespace MusselCalendar;

public sealed class MusselMiniMonthTests : BunitContext
{
    private const string ModulePath = "./_content/MusselCalendar/MusselCalendar.js";

    private static readonly DateOnly Thursday = new(2026, 11, 12);

    public MusselMiniMonthTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupModule(ModulePath);
    }

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
    public void GridFollowsDatePickerPattern()
    {
        var clock = new ManualTimeProvider { Now = new DateTimeOffset(2026, 11, 10, 9, 0, 0, TimeSpan.Zero) };
        var cut = Render<MusselMiniMonth>(parameters => parameters
            .Add(static p => p.Date, Thursday)
            .Add(static p => p.Culture, CultureInfo.InvariantCulture)
            .Add(static p => p.TimeProvider, clock));

        var title = cut.Find(".mm-title");
        Assert.Equal("polite", title.GetAttribute("aria-live"));
        Assert.Equal(title.Id, cut.Find("table[role='grid']").GetAttribute("aria-labelledby"));
        var header = cut.Find("th");
        Assert.Equal(("col", "Sunday", "Su"), (header.GetAttribute("scope"), header.GetAttribute("abbr"), header.TextContent));
        Assert.Equal(["2026-11-12"], cut.FindAll("td[tabindex='0']").Select(static x => x.GetAttribute("data-date")));
        Assert.Equal(["2026-11-12"], cut.FindAll("td[aria-selected]").Select(static x => x.GetAttribute("data-date")));
        Assert.Equal(["2026-11-10"], cut.FindAll("td[aria-current='date']").Select(static x => x.GetAttribute("data-date")));
        Assert.Equal("Thursday, 12 November 2026", cut.Find("td[aria-selected='true']").GetAttribute("aria-label"));
    }

    [Fact]
    public void ClickSelectsDay()
    {
        DateOnly? selected = null;
        var cut = Render<MusselMiniMonth>(parameters => parameters
            .Add(static p => p.Date, Thursday)
            .Add(static p => p.DateChanged, day => selected = day));

        cut.Find("td[data-date='2026-11-25']").Click();

        Assert.Equal(new DateOnly(2026, 11, 25), selected);
    }

    [Fact]
    public void DateMovesMonthWhenMonthIsNotGiven()
    {
        var months = new List<DateOnly>();
        var cut = Render<MusselMiniMonth>(parameters => parameters
            .Add(static p => p.Date, Thursday)
            .Add(static p => p.Culture, CultureInfo.InvariantCulture)
            .Add(static p => p.MonthChanged, months.Add));

        cut.Render(static parameters => parameters.Add(static p => p.Date, new DateOnly(2026, 12, 3)));

        Assert.Equal([new DateOnly(2026, 12, 1)], months);
        Assert.Equal("2026 December", cut.Find(".mm-title").TextContent);
    }

    [Fact]
    public void GivenMonthStaysWhenDateChanges()
    {
        var months = new List<DateOnly>();
        var november = new DateOnly(2026, 11, 1);
        var cut = Render<MusselMiniMonth>(parameters => parameters
            .Add(static p => p.Date, Thursday)
            .Add(static p => p.Month, november)
            .Add(static p => p.Culture, CultureInfo.InvariantCulture)
            .Add(static p => p.MonthChanged, months.Add));

        cut.Render(parameters => parameters
            .Add(static p => p.Date, new DateOnly(2026, 12, 3))
            .Add(static p => p.Month, november));

        Assert.Empty(months);
        Assert.Equal("2026 November", cut.Find(".mm-title").TextContent);
        Assert.Contains("mm-outside", cut.Find("td[aria-selected='true']").ClassList);
    }

    [Fact]
    public void NextButtonRaisesMonthChanged()
    {
        var months = new List<DateOnly>();
        var cut = Render<MusselMiniMonth>(parameters => parameters
            .Add(static p => p.Date, Thursday)
            .Add(static p => p.Culture, CultureInfo.InvariantCulture)
            .Add(static p => p.MonthChanged, months.Add));

        cut.FindAll(".mm-nav")[1].Click();

        Assert.Equal([new DateOnly(2026, 12, 1)], months);
        Assert.Equal("2026 December", cut.Find(".mm-title").TextContent);
        Assert.Equal(["2026-12-01"], cut.FindAll("td[tabindex='0']").Select(static x => x.GetAttribute("data-date")));
    }

    [Fact]
    public async Task ScriptMovesFocusAndSelects()
    {
        var months = new List<DateOnly>();
        DateOnly? selected = null;
        var cut = Render<MusselMiniMonth>(parameters => parameters
            .Add(static p => p.Date, Thursday)
            .Add(static p => p.MonthChanged, months.Add)
            .Add(static p => p.DateChanged, day => selected = day));

        await cut.InvokeAsync(() => cut.Instance.OnFocusDate("2026-11-20"));
        Assert.Empty(months);

        await cut.InvokeAsync(() => cut.Instance.OnFocusDate("2026-12-03"));
        Assert.Equal([new DateOnly(2026, 12, 1)], months);
        Assert.Equal(["2026-12-03"], cut.FindAll("td[tabindex='0']").Select(static x => x.GetAttribute("data-date")));

        await cut.InvokeAsync(() => cut.Instance.OnSelectDate("2026-12-04"));
        Assert.Equal(new DateOnly(2026, 12, 4), selected);
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
