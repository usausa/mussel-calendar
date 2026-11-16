namespace MusselCalendar.E2ETests;

public sealed class SamplePage(IBrowserContext context, IPage page) : IAsyncDisposable
{
    public IPage Page => page;

    public ILocator Calendar => page.Locator(".mussel-calendar");

    public ILocator PopupTitle => page.Locator(".app-popup-title");

    public ILocator Item(string title) => Calendar.Locator("[data-id]").Filter(new() { HasText = title });

    public Task WaitForCalendarAsync() =>
        page.Locator(".mussel-calendar[data-attached]").WaitForAsync(new() { State = WaitForSelectorState.Attached, Timeout = 30_000 });

    public Task ShowAsync(string view) => page.GetByRole(AriaRole.Checkbox, new() { Name = view, Exact = true }).ClickAsync();

    public static Task TouchAsync(ILocator target, int milliseconds, string end) =>
        target.EvaluateAsync(
            """
            async (element, [hold, end]) => {
                const box = element.getBoundingClientRect();
                const touch = new Touch({ identifier: 1, target: element, clientX: box.left + (box.width / 2), clientY: box.top + Math.min(10, box.height / 2) });
                element.dispatchEvent(new TouchEvent('touchstart', { touches: [touch], targetTouches: [touch], changedTouches: [touch], bubbles: true, cancelable: true }));
                await new Promise(resolve => setTimeout(resolve, hold));
                element.dispatchEvent(new TouchEvent(end, { touches: [], targetTouches: [], changedTouches: [touch], bubbles: true, cancelable: true }));
            }
            """,
            new object[] { milliseconds, end });

    public static Task<bool[]> DraggingWhileHeldAsync(ILocator target, int[] times) =>
        target.EvaluateAsync<bool[]>(
            """
            async (element, times) => {
                const box = element.getBoundingClientRect();
                const touch = new Touch({ identifier: 1, target: element, clientX: box.left + (box.width / 2), clientY: box.top + Math.min(10, box.height / 2) });
                element.dispatchEvent(new TouchEvent('touchstart', { touches: [touch], targetTouches: [touch], changedTouches: [touch], bubbles: true, cancelable: true }));
                const dragging = [];
                let elapsed = 0;
                for (const time of times) {
                    await new Promise(resolve => setTimeout(resolve, time - elapsed));
                    elapsed = time;
                    dragging.push(element.classList.contains('mc-dragging'));
                }
                element.dispatchEvent(new TouchEvent('touchcancel', { touches: [], targetTouches: [], changedTouches: [touch], bubbles: true, cancelable: true }));
                return dragging;
            }
            """,
            times);

    public ValueTask DisposeAsync() => context.DisposeAsync();
}
