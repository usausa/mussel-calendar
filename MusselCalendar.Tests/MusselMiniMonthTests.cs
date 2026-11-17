namespace MusselCalendar;

using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

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
    public async Task AFocusAskedForWhileTheScriptAttachesRunsOnceAfterIt()
    {
        var runtime = new LateRuntime { Module = { Hold = "attachMiniMonth" } };
        Services.AddSingleton<IJSRuntime>(runtime);
        var next = new DateOnly(2026, 12, 3);
        var cut = Render<MusselMiniMonth>(static parameters => parameters
            .Add(static p => p.Date, Thursday)
            .Add(static p => p.Culture, CultureInfo.InvariantCulture));
        await cut.InvokeAsync(() => runtime.Import.SetResult(runtime.Module));
        await cut.WaitForAssertionAsync(() => Assert.Contains("attachMiniMonth", runtime.Module.Calls));

        await cut.Find("td[data-date='2026-12-03']").ClickAsync(new MouseEventArgs());
        cut.Render(parameters => parameters.Add(static p => p.Date, next));
        await cut.InvokeAsync(runtime.Module.Resume.SetResult);
        await cut.WaitForAssertionAsync(() => Assert.Contains("focus", runtime.Module.Calls));
        cut.Render(static parameters => parameters.Add(static p => p.Highlight, CalendarRange.Of(CalendarViewKind.Week, new DateOnly(2026, 12, 3), DayOfWeek.Sunday, 1)));

        Assert.Single(runtime.Module.Calls, static x => x == "focus");
    }

    [Fact]
    public void TheFirstMonthDoesNotGoBack()
    {
        DateOnly? shown = null;
        var cut = Render<MusselMiniMonth>(parameters => parameters
            .Add(static p => p.Date, DateOnly.MinValue)
            .Add(static p => p.Culture, CultureInfo.InvariantCulture)
            .Add(static p => p.MonthChanged, month => shown = month));

        cut.FindAll(".mm-nav")[0].Click();

        Assert.Null(shown);
        Assert.Equal("Su", cut.Find("th").TextContent);
        Assert.Equal([null, "0001-01-01"], cut.FindAll("tbody td").Take(2).Select(static x => x.GetAttribute("data-date")));
    }

    [Fact]
    public void TheLastMonthDoesNotGoForward()
    {
        DateOnly? shown = null;
        var cut = Render<MusselMiniMonth>(parameters => parameters
            .Add(static p => p.Date, DateOnly.MaxValue)
            .Add(static p => p.Culture, CultureInfo.InvariantCulture)
            .Add(static p => p.MonthChanged, month => shown = month));

        cut.FindAll(".mm-nav")[1].Click();

        var cells = cut.FindAll("tbody td");
        Assert.Null(shown);
        Assert.Equal("9999-12-31", cells[33].GetAttribute("data-date"));
        Assert.All(cells.Skip(34), static x => Assert.Null(x.GetAttribute("data-date")));
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
    public async Task AnExceptionInAHandlerCalledFromTheScriptReachesTheRenderer()
    {
        var cut = Render<MusselMiniMonth>(parameters => parameters
            .Add(static p => p.Date, Thursday)
            .Add(static p => p.DateChanged, static _ => throw new InvalidOperationException("app")));

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => cut.InvokeAsync(() => cut.Instance.OnSelectDate("2026-11-20")));
        var ended = await Task.WhenAny(Renderer.UnhandledException, Task.Delay(500, Xunit.TestContext.Current.CancellationToken)).ConfigureAwait(true);

        Assert.Same(Renderer.UnhandledException, ended);
        Assert.Same(thrown, await Renderer.UnhandledException.ConfigureAwait(true));
    }

    [Fact]
    public async Task AnExceptionFromMonthChangedWhileFocusingReachesTheRenderer()
    {
        var cut = Render<MusselMiniMonth>(parameters => parameters
            .Add(static p => p.Date, Thursday)
            .Add(static p => p.MonthChanged, static _ => throw new InvalidOperationException("app")));

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => cut.InvokeAsync(() => cut.Instance.OnFocusDate("2026-12-03")));
        var ended = await Task.WhenAny(Renderer.UnhandledException, Task.Delay(500, Xunit.TestContext.Current.CancellationToken)).ConfigureAwait(true);

        Assert.Same(Renderer.UnhandledException, ended);
        Assert.Same(thrown, await Renderer.UnhandledException.ConfigureAwait(true));
    }

    [Fact]
    public void ACultureWithAnotherCalendarIsCopiedOnce()
    {
        var cut = Render<MusselMiniMonth>(parameters => parameters
            .Add(static p => p.Date, Thursday)
            .Add(static p => p.Culture, CultureInfo.GetCultureInfo("th-TH")));
        var first = cut.Instance.FormatCulture;

        cut.Render(static parameters => parameters.Add(static p => p.Date, Thursday.AddDays(1)));

        Assert.Same(first, cut.Instance.FormatCulture);
        Assert.IsType<GregorianCalendar>(first.DateTimeFormat.Calendar);
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
