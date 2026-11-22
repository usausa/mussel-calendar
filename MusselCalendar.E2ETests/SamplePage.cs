namespace MusselCalendar.E2ETests;

public sealed class SamplePage(IBrowserContext context, IPage page) : IAsyncDisposable
{
    public IPage Page => page;

    public ILocator Calendar => page.Locator(".mussel-calendar");

    public ILocator PopupTitle => page.Locator(".app-popup-title");

    public ILocator Item(string title) => Calendar.Locator("[data-id]").Filter(new() { HasText = title });

    public Task WaitForCalendarAsync() =>
        page.WaitForFunctionAsync(
            "() => { const roots = document.querySelectorAll('.mussel-calendar, .mussel-mini-month'); return (roots.length > 0) && [...roots].every((root) => root.hasAttribute('data-attached')); }",
            null,
            new() { Timeout = 30_000 });

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

    public Task RejectCallsAsync(params string[] names) =>
        page.EvaluateAsync(
            """
            names => {
                const proto = DotNet.DotNetObject.prototype;
                const invoke = proto.invokeMethodAsync;
                window.rejected = [];
                window.unhandled = 0;
                window.addEventListener('unhandledrejection', () => { window.unhandled++; });
                proto.invokeMethodAsync = function (name, ...args) {
                    if (names.includes(name)) {
                        window.rejected.push(name);
                        return Promise.reject(new Error(name));
                    }
                    return invoke.call(this, name, ...args);
                };
            }
            """,
            names);

    public Task HoldCallsAsync(params string[] names) =>
        page.EvaluateAsync(
            """
            names => {
                const proto = DotNet.DotNetObject.prototype;
                const invoke = proto.invokeMethodAsync;
                window.held = [];
                proto.invokeMethodAsync = function (name, ...args) {
                    if (names.includes(name)) {
                        window.held.push(name);
                        return new Promise(() => { });
                    }
                    return invoke.call(this, name, ...args);
                };
            }
            """,
            names);

    public async Task<int> UnhandledRejectionsAsync(int calls)
    {
        await page.WaitForFunctionAsync("calls => window.rejected.length >= calls", calls).ConfigureAwait(true);
        return await page.EvaluateAsync<int>("() => new Promise(resolve => setTimeout(() => resolve(window.unhandled), 300))").ConfigureAwait(true);
    }

    public ValueTask DisposeAsync() => context.DisposeAsync();
}
