using SportsbookLite.Contracts.Odds;

namespace SportsbookLite.Contracts.Events;

[GenerateSerializer]
public readonly record struct OddsSuspendedEvent(
    [property: Id(0)] Guid Id,
    [property: Id(1)] DateTimeOffset Timestamp,
    [property: Id(2)] string AggregateId,
    [property: Id(3)] string MarketId,
    [property: Id(4)] OddsVolatility VolatilityLevel,
    [property: Id(5)] string Reason,
    [property: Id(6)] bool IsAutomatic,
    [property: Id(7)] string? SuspendedBy = null) : IDomainEvent
{
    public static OddsSuspendedEvent CreateAutomatic(
        string marketId,
        OddsVolatility volatilityLevel,
        string reason)
    {
        return new OddsSuspendedEvent(
            Id: Guid.NewGuid(),
            Timestamp: DateTimeOffset.UtcNow,
            AggregateId: marketId,
            MarketId: marketId,
            VolatilityLevel: volatilityLevel,
            Reason: reason,
            IsAutomatic: true);
    }

    public static OddsSuspendedEvent CreateManual(
        string marketId,
        string reason,
        string suspendedBy,
        OddsVolatility volatilityLevel = OddsVolatility.Low)
    {
        return new OddsSuspendedEvent(
            Id: Guid.NewGuid(),
            Timestamp: DateTimeOffset.UtcNow,
            AggregateId: marketId,
            MarketId: marketId,
            VolatilityLevel: volatilityLevel,
            Reason: reason,
            IsAutomatic: false,
            SuspendedBy: suspendedBy);
    }
}