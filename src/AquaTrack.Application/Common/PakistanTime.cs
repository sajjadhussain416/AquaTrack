namespace AquaTrack.Application.Common;

/// <summary>
/// SRS NFR-15: store UTC, show Pakistan Standard Time (UTC+5, no daylight saving).
/// </summary>
public static class PakistanTime
{
    private static readonly TimeSpan Offset = TimeSpan.FromHours(5);

    public static DateTime Now => DateTime.UtcNow + Offset;

    /// <summary>Business date in Pakistan, used for deliveries and billing.</summary>
    public static DateOnly Today => DateOnly.FromDateTime(Now);

    public static DateTime ToLocal(DateTime utc) => DateTime.SpecifyKind(utc, DateTimeKind.Utc) + Offset;
}
