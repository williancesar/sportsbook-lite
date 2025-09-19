using SportsbookLite.Contracts.Odds;

namespace SportsbookLite.Contracts.Events;

[GenerateSerializer]
public readonly record struct OddsVolatilityChangedEvent(
    [property: Id(0)] Guid Id,
    [property: Id(1)] DateTimeOffset Timestamp,
    [property: Id(2)] string AggregateId,
    [property: Id(3)] string MarketId,
    [property: Id(4)] OddsVolatility PreviousLevel,
    [property: Id(5)] OddsVolatility NewLevel,
    [property: Id(6)] decimal VolatilityScore,
    [property: Id(7)] int UpdateCountInWindow,
    [property: Id(8)] TimeSpan CalculationWindow) : IDomainEvent
{
    public static OddsVolatilityChangedEvent Create(
        string marketId,
        OddsVolatility previousLevel,
        OddsVolatility newLevel,
        decimal volatilityScore,
        int updateCountInWindow,
        TimeSpan calculationWindow)
    {
        return new OddsVolatilityChangedEvent(
            Id: Guid.NewGuid(),
            Timestamp: DateTimeOffset.UtcNow,
            AggregateId: marketId,
            MarketId: marketId,
            PreviousLevel: previousLevel,
            NewLevel: newLevel,
            VolatilityScore: volatilityScore,
            UpdateCountInWindow: updateCountInWindow,
            CalculationWindow: calculationWindow);
    }

    public bool IsEscalation => NewLevel > PreviousLevel;
    public bool IsDeescalation => NewLevel < PreviousLevel;
    public bool RequiresAttention => NewLevel >= OddsVolatility.High;
}