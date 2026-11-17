namespace MusselCalendar.Internal;

using Microsoft.JSInterop;

internal sealed class CalendarScriptBridge(MusselCalendarView view)
{
    [JSInvokable]
    public Task OnSelect(string start, string until, bool allDay, double left, double top, double width, double height) =>
        RunAsync(() => view.OnSelect(start, until, allDay, left, top, width, height));

    [JSInvokable]
    public Task OnItemClick(string id, double left, double top, double width, double height) => RunAsync(() => view.OnItemClick(id, left, top, width, height));

    [JSInvokable]
    public Task OnMove(string id, string start, string until, bool allDay) => RunAsync(() => view.OnMove(id, start, until, allDay));

    [JSInvokable]
    public Task OnShift(string id, int days, int minutes) => RunAsync(() => view.OnShift(id, days, minutes));

    [JSInvokable]
    public Task OnExtend(string id, int minutes) => RunAsync(() => view.OnExtend(id, minutes));

    [JSInvokable]
    public void OnMonthLanes(int lanes)
    {
        if (!view.IsDisposed)
        {
            view.OnMonthLanes(lanes);
        }
    }

    [JSInvokable]
    public Task OnDayClick(string date) => RunAsync(() => view.OnDayClick(date));

    [JSInvokable]
    public void OnYearFocus(string date)
    {
        if (!view.IsDisposed)
        {
            view.OnYearFocus(date);
        }
    }

    [JSInvokable]
    public Task OnMoreClick(string date, double left, double top, double width, double height) => RunAsync(() => view.OnMoreClick(date, left, top, width, height));

    private Task RunAsync(Func<Task> operation) => view.IsDisposed ? Task.CompletedTask : view.ReportAsync(operation());
}
