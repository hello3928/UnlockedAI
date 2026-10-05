namespace UnlockedAI.Tests.Support;

/// <summary>A clock that always shows the same moment, in a fixed time zone offset.</summary>
internal sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    /// <summary>Monday 9 March 2026, 14:30, at UTC+10.</summary>
    public static FixedClock Default { get; } = new(new DateTimeOffset(2026, 3, 9, 14, 30, 0, TimeSpan.FromHours(10)));

    public override TimeZoneInfo LocalTimeZone { get; } =
        TimeZoneInfo.CreateCustomTimeZone("Test", now.Offset, "Test", "Test");

    public override DateTimeOffset GetUtcNow() => now.ToUniversalTime();
}
