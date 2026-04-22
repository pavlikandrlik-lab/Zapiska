namespace PmTracker.Web.Services.Sync;

/// <summary>
/// Pure static calculator: zarovnat další tick na anchor + celočíselný násobek periody.
/// </summary>
public static class SyncScheduleCalculator
{
    public static DateTimeOffset ComputeNext(DateTimeOffset now, DateTimeOffset anchorAt, TimeSpan period)
    {
        if (period <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(period), period, "period must be > TimeSpan.Zero");
        }

        if (now <= anchorAt)
        {
            return anchorAt;
        }

        var elapsed = now - anchorAt;
        var ticks = (long)Math.Ceiling(elapsed.Ticks / (double)period.Ticks);
        return anchorAt + TimeSpan.FromTicks(period.Ticks * ticks);
    }
}
