namespace MusselCalendar.Internal;

using Microsoft.JSInterop;

internal static class ScriptCalls
{
    public static async Task SendAsync(IJSObjectReference target, string identifier, params object?[] args)
    {
        try
        {
            await target.InvokeVoidAsync(identifier, args).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is JSException or OperationCanceledException)
        {
        }
    }
}
