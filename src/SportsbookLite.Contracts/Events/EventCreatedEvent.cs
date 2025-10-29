namespace SportsbookLite.Contracts.Events;

[GenerateSerializer]
public readonly record struct EventCreatedEvent(
    [property: Id(0)] Guid Id,
    [property: Id(1)] DateTimeOffset Timestamp,
    [property: Id(2)] string AggregateId,
    [property: Id(3)] SportEvent SportEvent) : IDomainEvent
{
    public static EventCreatedEvent Create(SportEvent sportEvent)
    {
        return new EventCreatedEvent(
            Id: Guid.NewGuid(),
            Timestamp: DateTimeOffset.UtcNow,
            AggregateId: sportEvent.Id.ToString(),
            SportEvent: sportEvent);
    }
}