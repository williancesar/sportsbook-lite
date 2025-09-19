using SportsbookLite.Contracts.Odds;

namespace SportsbookLite.Contracts.Events;

[GenerateSerializer]
public readonly record struct OddsResumedEvent(
    [property: Id(0)] Guid Id,
    [property: Id(1)] DateTimeOffset Timestamp,
    [property: Id(2)] string AggregateId,
    [property: Id(3)] string MarketId,
    [property: Id(4)] OddsVolatility VolatilityLevel,
    [property: Id(5)] string Reason,
    [property: Id(6)] string? ResumedBy = null,
    [property: Id(7)] TimeSpan SuspensionDuration = default) : IDomainEvent
{
    public static OddsResumedEvent Create(
        string marketId,
        OddsVolatility volatilityLevel,
        string reason,
        string? resumedBy = null,
        DateTimeOffset? suspensionStartTime = null)
    {
        var suspensionDuration = suspensionStartTime.HasValue 
            ? DateTimeOffset.UtcNow - suspensionStartTime.Value 
            : TimeSpan.Zero;

        return new OddsResumedEvent(
            Id: Guid.NewGuid(),
            Timestamp: DateTimeOffset.UtcNow,
            AggregateId: marketId,
            MarketId: marketId,
            VolatilityLevel: volatilityLevel,
            Reason: reason,
            ResumedBy: resumedBy,
            SuspensionDuration: suspensionDuration);
    }
}