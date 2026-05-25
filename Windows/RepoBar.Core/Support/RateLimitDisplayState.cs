namespace RepoBar.Core.Support;

public sealed record RateLimitSnapshot(
    string Resource,
    int? Limit,
    int? Remaining,
    DateTimeOffset? ResetAt,
    string? LastError = null);

public sealed record RateLimitDisplayState(
    string Title,
    string Detail,
    bool IsBlocked);

public static class RateLimitStatusFormatter
{
    public static RateLimitDisplayState Format(RateLimitSnapshot snapshot, DateTimeOffset now)
    {
        if (!string.IsNullOrWhiteSpace(snapshot.LastError))
        {
            return new RateLimitDisplayState("API limited", snapshot.LastError, IsBlocked: true);
        }

        if (snapshot.Remaining == 0 && snapshot.ResetAt is DateTimeOffset resetAt)
        {
            TimeSpan wait = resetAt - now;
            string detail = wait <= TimeSpan.Zero
                ? "Retry now"
                : $"Resets in {Math.Ceiling(wait.TotalMinutes)} min";
            return new RateLimitDisplayState($"{snapshot.Resource}: exhausted", detail, IsBlocked: true);
        }

        string remaining = snapshot.Remaining?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "?";
        string limit = snapshot.Limit?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "?";
        return new RateLimitDisplayState($"{snapshot.Resource}: {remaining}/{limit}", "Available", IsBlocked: false);
    }
}
