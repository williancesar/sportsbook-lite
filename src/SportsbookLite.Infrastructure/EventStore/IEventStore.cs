using SportsbookLite.Contracts.Events;

namespace SportsbookLite.Infrastructure.EventStore;

public interface IEventStore
{
    ValueTask SaveEventAsync<TEvent>(string aggregateId, TEvent domainEvent) where TEvent : IDomainEvent;
    ValueTask SaveEventsAsync<TEvent>(string aggregateId, IEnumerable<TEvent> events) where TEvent : IDomainEvent;
    ValueTask<IReadOnlyList<IDomainEvent>> GetEventsAsync(string aggregateId);
    ValueTask<IReadOnlyList<IDomainEvent>> GetEventsAsync(string aggregateId, long fromVersion);
    ValueTask<EventStream?> GetEventStreamAsync(string aggregateId);
    ValueTask<bool> EventStreamExistsAsync(string aggregateId);
}