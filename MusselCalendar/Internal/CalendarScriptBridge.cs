namespace MusselCalendar.Internal;

using Microsoft.JSInterop;

internal sealed class CalendarScriptBridge(MusselCalendarView view)
{
    [JSInvokable]
    public Task OnSelectAsync(string start, string until, bool allDay, double left, double top, double width, double height) =>
        RunAsync(() => view.OnSelectAsync(start, until, allDay, left, top, width, height));

    [JSInvokable]
    public Task OnItemClickAsync(string id, double left, double top, double width, double height) => RunAsync(() => view.OnItemClickAsync(id, left, top, width, height));

    [JSInvokable]
    public Task OnMoveAsync(string id, string start, string? until, bool allDay) => RunAsync(() => view.OnMoveAsync(id, start, until, allDay));

    [JSInvokable]
    public Task OnResizeAsync(string id, string until) => RunAsync(() => view.OnResizeAsync(id, until));

    [JSInvokable]
    public Task OnShiftAsync(string id, int days, int minutes) => RunAsync(() => view.OnShiftAsync(id, days, minutes));

    [JSInvokable]
    public Task OnExtendAsync(string id, int minutes) => RunAsync(() => view.OnExtendAsync(id, minutes));

    [JSInvokable]
    public void OnMonthLanes(int lanes)
    {
        if (!view.IsDisposed)
        {
            view.OnMonthLanes(lanes);
        }
    }

    [JSInvokable]
    public Task OnDayClickAsync(string date) => RunAsync(() => view.OnDayClickAsync(date));

    [JSInvokable]
    public void OnYearFocus(string date)
    {
        if (!view.IsDisposed)
        {
            view.OnYearFocus(date);
        }
    }

    [JSInvokable]
    public Task OnMoreClickAsync(string date, double left, double top, double width, double height) => RunAsync(() => view.OnMoreClickAsync(date, left, top, width, height));

    private Task RunAsync(Func<Task> operation) => view.IsDisposed ? Task.CompletedTask : view.ReportAsync(operation());
}
