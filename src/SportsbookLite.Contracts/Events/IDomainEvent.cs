namespace SportsbookLite.Contracts.Events;

public interface IDomainEvent
{
    Guid Id { get; }
    DateTimeOffset Timestamp { get; }
    string AggregateId { get; }
}