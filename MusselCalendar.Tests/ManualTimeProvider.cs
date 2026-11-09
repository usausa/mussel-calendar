namespace MusselCalendar;

internal sealed class ManualTimeProvider : TimeProvider
{
    private readonly List<TimerCallback> callbacks = [];

    public DateTimeOffset Now { get; set; }

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

    public override DateTimeOffset GetUtcNow() => Now;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        callbacks.Add(callback);
        return new ManualTimer();
    }

    public void Fire()
    {
        foreach (var callback in callbacks)
        {
            callback(null);
        }
    }

    private sealed class ManualTimer : ITimer
    {
        public bool Change(TimeSpan dueTime, TimeSpan period) => true;

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
