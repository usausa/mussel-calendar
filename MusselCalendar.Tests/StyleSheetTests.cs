namespace MusselCalendar;

using System.Text.RegularExpressions;

public sealed partial class StyleSheetTests
{
    [Fact]
    public void EveryThemeVariableIsDeclaredWithoutSpecificity()
    {
        var css = File.ReadAllText(Path.Combine(TestPaths.Root, "MusselCalendar", "wwwroot", "MusselCalendar.css"));

        var selectors = Rule().Matches(css)
            .Where(static x => Declaration().IsMatch(x.Groups["body"].Value))
            .SelectMany(static x => TopLevelComma().Split(x.Groups["selector"].Value))
            .Select(static x => x.Trim())
            .ToList();

        Assert.NotEmpty(selectors);
        Assert.All(selectors, static x => Assert.Matches(WhereOnly(), x));
    }

    [Fact]
    public void DaysOffHaveAShadeInBothThemes()
    {
        var css = File.ReadAllText(Path.Combine(TestPaths.Root, "MusselCalendar", "wwwroot", "MusselCalendar.css"));

        var selectors = Rule().Matches(css)
            .Where(static x => x.Groups["body"].Value.Contains("--mussel-calendar-off:", StringComparison.Ordinal))
            .Select(static x => x.Groups["selector"].Value.Trim())
            .ToList();

        Assert.Contains(":where(.mussel-calendar)", selectors);
        Assert.Contains(selectors, static x => x.Contains(".mussel-dark", StringComparison.Ordinal));
    }

    [Fact]
    public void TheDarkThemeHasDarkScrollbars()
    {
        var css = File.ReadAllText(Path.Combine(TestPaths.Root, "MusselCalendar", "wwwroot", "MusselCalendar.css"));

        var selectors = Rule().Matches(css)
            .Where(static x => x.Groups["body"].Value.Contains("color-scheme: dark;", StringComparison.Ordinal))
            .Select(static x => x.Groups["selector"].Value.Trim())
            .ToList();

        Assert.Contains(selectors, static x => x.Contains(":where(.mussel-calendar.mussel-dark)", StringComparison.Ordinal));
    }

    [GeneratedRegex(@"(?<selector>[^{}]+)\{(?<body>[^{}]*)\}")]
    private static partial Regex Rule();

    [GeneratedRegex(@"(?:^|;)\s*--mussel-[a-z0-9-]+\s*:")]
    private static partial Regex Declaration();

    [GeneratedRegex(@",(?![^(]*\))")]
    private static partial Regex TopLevelComma();

    [GeneratedRegex(@"^(?::where\([^()]*\)\s*)+$")]
    private static partial Regex WhereOnly();
}
