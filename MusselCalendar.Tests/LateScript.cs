namespace MusselCalendar;

using Microsoft.JSInterop;

internal sealed class LateRuntime : IJSRuntime
{
    public TaskCompletionSource<IJSObjectReference> Import { get; } = new();

    public LateModule Module { get; } = new();

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
        identifier == "import" ? new ValueTask<TValue>((Task<TValue>)(object)Import.Task) : default;

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
        identifier == "import" ? new ValueTask<TValue>((Task<TValue>)(object)Import.Task) : default;
}

internal sealed class LateModule : IJSObjectReference
{
    public List<string> Calls { get; } = [];

    public string? Hold { get; set; }

    public TaskCompletionSource Resume { get; } = new();

    public bool Released { get; private set; }

    public ValueTask DisposeAsync()
    {
        Released = true;
        return ValueTask.CompletedTask;
    }

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
    {
        ObjectDisposedException.ThrowIf(Released, this);
        Calls.Add(identifier);
        var result = typeof(TValue) == typeof(IJSObjectReference) ? (TValue)(object)this : default!;
        return identifier == Hold
            ? new ValueTask<TValue>(Resume.Task.ContinueWith(_ => result, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default))
            : new ValueTask<TValue>(result);
    }

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
    {
        ObjectDisposedException.ThrowIf(Released, this);
        Calls.Add(identifier);
        var result = typeof(TValue) == typeof(IJSObjectReference) ? (TValue)(object)this : default!;
        return identifier == Hold
            ? new ValueTask<TValue>(Resume.Task.ContinueWith(_ => result, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default))
            : new ValueTask<TValue>(result);
    }
}
