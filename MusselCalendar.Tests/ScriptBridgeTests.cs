namespace MusselCalendar;

using System.Reflection;
using System.Text.RegularExpressions;

using Microsoft.JSInterop;

using MusselCalendar.Internal;

public sealed partial class ScriptBridgeTests
{
    [Fact]
    public void ScriptEntryPointsStayOutOfThePublicApi()
    {
        var entryPoints = typeof(MusselCalendarView).Assembly.GetExportedTypes()
            .SelectMany(static x => x.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Where(static x => x.IsDefined(typeof(JSInvokableAttribute), false))
            .Select(static x => $"{x.DeclaringType!.Name}.{x.Name}");

        Assert.Empty(entryPoints);
    }

    [Fact]
    public void EveryCallFromTheScriptHasAnEntryPointOnItsOwnBridge()
    {
        var script = File.ReadAllText(Path.Combine(TestPaths.Root, "MusselCalendar", "wwwroot", "MusselCalendar.js"));
        var mini = script.IndexOf("class MiniMonth", StringComparison.Ordinal);

        AssertEntryPoints(script[..mini], typeof(CalendarScriptBridge));
        AssertEntryPoints(script[mini..], typeof(MiniMonthScriptBridge));
    }

    private static void AssertEntryPoints(string script, Type bridge)
    {
        var names = Calls().Matches(script).Select(static x => x.Groups["name"].Value).Distinct().ToList();
        var entryPoints = bridge.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(static x => x.IsDefined(typeof(JSInvokableAttribute), false))
            .Select(static x => x.Name)
            .ToList();

        Assert.NotEmpty(names);
        Assert.All(names, name => Assert.Contains(name, entryPoints));
    }

    [GeneratedRegex(@"invokeMethodAsync\(\s*'(?<name>\w+)'")]
    private static partial Regex Calls();
}
