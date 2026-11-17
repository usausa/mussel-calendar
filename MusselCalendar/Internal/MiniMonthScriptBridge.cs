namespace MusselCalendar.Internal;

using Microsoft.JSInterop;

internal sealed class MiniMonthScriptBridge(MusselMiniMonth month)
{
    [JSInvokable]
    public Task OnFocusDate(string date) => RunAsync(() => month.OnFocusDate(date));

    [JSInvokable]
    public Task OnSelectDate(string date) => RunAsync(() => month.OnSelectDate(date));

    private Task RunAsync(Func<Task> operation) => month.IsDisposed ? Task.CompletedTask : month.ReportAsync(operation());
}
