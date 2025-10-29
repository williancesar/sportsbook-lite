namespace SportsbookLite.Contracts.Events;

[GenerateSerializer]
public readonly record struct MarketSuspendedEvent(
    [property: Id(0)] Guid Id,
    [property: Id(1)] DateTimeOffset Timestamp,
    [property: Id(2)] string AggregateId,
    [property: Id(3)] Guid EventId,
    [property: Id(4)] Guid MarketId,
    [property: Id(5)] string Reason) : IDomainEvent
{
    public static MarketSuspendedEvent Create(Guid eventId, Guid marketId, string reason)
    {
        return new MarketSuspendedEvent(
            Id: Guid.NewGuid(),
            Timestamp: DateTimeOffset.UtcNow,
            AggregateId: eventId.ToString(),
            EventId: eventId,
            MarketId: marketId,
            Reason: reason);
    }
}