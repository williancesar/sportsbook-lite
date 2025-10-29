namespace SportsbookLite.Contracts.Events;

[GenerateSerializer]
public readonly record struct MarketSettledEvent(
    [property: Id(0)] Guid Id,
    [property: Id(1)] DateTimeOffset Timestamp,
    [property: Id(2)] string AggregateId,
    [property: Id(3)] Guid EventId,
    [property: Id(4)] Guid MarketId,
    [property: Id(5)] string WinningOutcome) : IDomainEvent
{
    public static MarketSettledEvent Create(Guid eventId, Guid marketId, string winningOutcome)
    {
        return new MarketSettledEvent(
            Id: Guid.NewGuid(),
            Timestamp: DateTimeOffset.UtcNow,
            AggregateId: eventId.ToString(),
            EventId: eventId,
            MarketId: marketId,
            WinningOutcome: winningOutcome);
    }
}