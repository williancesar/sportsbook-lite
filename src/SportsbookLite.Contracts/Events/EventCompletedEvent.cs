namespace SportsbookLite.Contracts.Events;

[GenerateSerializer]
public readonly record struct EventCompletedEvent(
    [property: Id(0)] Guid Id,
    [property: Id(1)] DateTimeOffset Timestamp,
    [property: Id(2)] string AggregateId,
    [property: Id(3)] Guid EventId,
    [property: Id(4)] DateTimeOffset EndTime,
    [property: Id(5)] EventResult? Result = null) : IDomainEvent
{
    public static EventCompletedEvent Create(Guid eventId, DateTimeOffset endTime, EventResult? result = null)
    {
        return new EventCompletedEvent(
            Id: Guid.NewGuid(),
            Timestamp: DateTimeOffset.UtcNow,
            AggregateId: eventId.ToString(),
            EventId: eventId,
            EndTime: endTime,
            Result: result);
    }
}