namespace GroceryKhata.Api.Tests;

/// <summary>A clock stopped at one moment, for tests whose answer depends on the time of day.</summary>
public sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now.ToUniversalTime();
}
