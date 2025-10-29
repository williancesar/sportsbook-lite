namespace SportsbookLite.Contracts.Events;

[GenerateSerializer]
public readonly record struct EventStartedEvent(
    [property: Id(0)] Guid Id,
    [property: Id(1)] DateTimeOffset Timestamp,
    [property: Id(2)] string AggregateId,
    [property: Id(3)] Guid EventId,
    [property: Id(4)] DateTimeOffset StartTime) : IDomainEvent
{
    public static EventStartedEvent Create(Guid eventId, DateTimeOffset startTime)
    {
        return new EventStartedEvent(
            Id: Guid.NewGuid(),
            Timestamp: DateTimeOffset.UtcNow,
            AggregateId: eventId.ToString(),
            EventId: eventId,
            StartTime: startTime);
    }
}