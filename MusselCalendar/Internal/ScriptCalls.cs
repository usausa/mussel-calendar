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

    public static async Task ReleaseAsync(IJSObjectReference? attached, IJSObjectReference? loaded)
    {
        try
        {
            if (attached is not null)
            {
                try
                {
                    await attached.InvokeVoidAsync("dispose").ConfigureAwait(true);
                }
                catch (JSException)
                {
                }

                await attached.DisposeAsync().ConfigureAwait(true);
            }

            if (loaded is not null)
            {
                await loaded.DisposeAsync().ConfigureAwait(true);
            }
        }
        catch (JSDisconnectedException)
        {
        }
        catch (JSException)
        {
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }
}
