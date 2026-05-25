namespace RepoBar.Core.Support;

public sealed class RefreshScheduler(TimeSpan interval, DateTimeOffset now)
{
    public TimeSpan Interval { get; private set; } = Normalize(interval);

    public DateTimeOffset NextDueAt { get; private set; } = now + Normalize(interval);

    public bool IsManualRefreshPending { get; private set; }

    public bool IsDue(DateTimeOffset now) => IsManualRefreshPending || now >= NextDueAt;

    public void Configure(TimeSpan interval, DateTimeOffset now)
    {
        Interval = Normalize(interval);
        NextDueAt = now + Interval;
        IsManualRefreshPending = false;
    }

    public void RequestManualRefresh() => IsManualRefreshPending = true;

    public void RecordSuccess(DateTimeOffset now)
    {
        IsManualRefreshPending = false;
        NextDueAt = now + Interval;
    }

    public void RecordBackoff(DateTimeOffset now, TimeSpan? backoff)
    {
        IsManualRefreshPending = false;
        TimeSpan wait = backoff is null || backoff.Value < TimeSpan.Zero ? Interval : Max(Interval, backoff.Value);
        NextDueAt = now + wait;
    }

    private static TimeSpan Normalize(TimeSpan interval) =>
        interval < TimeSpan.FromMinutes(1) ? TimeSpan.FromMinutes(1) : interval;

    private static TimeSpan Max(TimeSpan lhs, TimeSpan rhs) => lhs >= rhs ? lhs : rhs;
}
