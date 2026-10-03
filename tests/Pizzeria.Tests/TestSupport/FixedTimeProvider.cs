namespace Pizzeria.Tests.TestSupport;

/// <summary>Test clock that always returns the same instant.</summary>
public sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow;
}
