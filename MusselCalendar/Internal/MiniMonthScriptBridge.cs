namespace MusselCalendar.Internal;

using Microsoft.JSInterop;

internal sealed class MiniMonthScriptBridge(MusselMiniMonth month)
{
    [JSInvokable]
    public Task OnFocusDate(string date) => month.OnFocusDate(date);

    [JSInvokable]
    public Task OnSelectDate(string date) => month.OnSelectDate(date);
}
