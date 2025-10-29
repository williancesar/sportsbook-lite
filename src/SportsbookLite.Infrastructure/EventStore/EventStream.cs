using SportsbookLite.Contracts.Events;

namespace SportsbookLite.Infrastructure.EventStore;

[GenerateSerializer]
public sealed record EventStream(
    [property: Id(0)] string AggregateId,
    [property: Id(1)] IReadOnlyList<IDomainEvent> Events,
    [property: Id(2)] long Version,
    [property: Id(3)] DateTimeOffset CreatedAt,
    [property: Id(4)] DateTimeOffset UpdatedAt
)
{
    public static EventStream Empty(string aggregateId) => new(
        aggregateId,
        new List<IDomainEvent>(),
        0,
        DateTimeOffset.UtcNow,
        DateTimeOffset.UtcNow
    );

    public EventStream AddEvent(IDomainEvent domainEvent) => this with
    {
        Events = Events.Concat(new[] { domainEvent }).ToList(),
        Version = Version + 1,
        UpdatedAt = DateTimeOffset.UtcNow
    };

    public EventStream AddEvents(IEnumerable<IDomainEvent> events) => this with
    {
        Events = Events.Concat(events).ToList(),
        Version = Version + events.Count(),
        UpdatedAt = DateTimeOffset.UtcNow
    };
}