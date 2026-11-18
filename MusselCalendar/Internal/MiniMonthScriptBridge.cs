namespace MusselCalendar.Internal;

using Microsoft.JSInterop;

internal sealed class MiniMonthScriptBridge(MusselMiniMonth view)
{
    [JSInvokable]
    public Task OnFocusDateAsync(string date) => RunAsync(() => view.OnFocusDateAsync(date));

    [JSInvokable]
    public Task OnSelectDateAsync(string date) => RunAsync(() => view.OnSelectDateAsync(date));

    private Task RunAsync(Func<Task> operation) => view.IsDisposed ? Task.CompletedTask : view.ReportAsync(operation());
}
