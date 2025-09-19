namespace SportsbookLite.Contracts.Events;

[GenerateSerializer]
public readonly record struct EventCancelledEvent(
    [property: Id(0)] Guid Id,
    [property: Id(1)] DateTimeOffset Timestamp,
    [property: Id(2)] string AggregateId,
    [property: Id(3)] Guid EventId,
    [property: Id(4)] string Reason) : IDomainEvent
{
    public static EventCancelledEvent Create(Guid eventId, string reason)
    {
        return new EventCancelledEvent(
            Id: Guid.NewGuid(),
            Timestamp: DateTimeOffset.UtcNow,
            AggregateId: eventId.ToString(),
            EventId: eventId,
            Reason: reason);
    }
}