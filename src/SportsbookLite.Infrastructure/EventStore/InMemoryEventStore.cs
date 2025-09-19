using SportsbookLite.Contracts.Events;
using System.Collections.Concurrent;

namespace SportsbookLite.Infrastructure.EventStore;

public sealed class InMemoryEventStore : IEventStore
{
    private readonly ConcurrentDictionary<string, EventStream> _eventStreams = new();

    public ValueTask SaveEventAsync<TEvent>(string aggregateId, TEvent domainEvent) where TEvent : IDomainEvent
    {
        _eventStreams.AddOrUpdate(
            aggregateId,
            _ => EventStream.Empty(aggregateId).AddEvent(domainEvent),
            (_, existingStream) => existingStream.AddEvent(domainEvent)
        );

        return ValueTask.CompletedTask;
    }

    public ValueTask SaveEventsAsync<TEvent>(string aggregateId, IEnumerable<TEvent> events) where TEvent : IDomainEvent
    {
        _eventStreams.AddOrUpdate(
            aggregateId,
            _ => events.Aggregate(EventStream.Empty(aggregateId), (stream, evt) => stream.AddEvent(evt)),
            (_, existingStream) => existingStream.AddEvents(events.Cast<IDomainEvent>())
        );

        return ValueTask.CompletedTask;
    }

    public ValueTask<IReadOnlyList<IDomainEvent>> GetEventsAsync(string aggregateId)
    {
        var eventStream = _eventStreams.GetValueOrDefault(aggregateId);
        return ValueTask.FromResult(eventStream?.Events ?? new List<IDomainEvent>());
    }

    public ValueTask<IReadOnlyList<IDomainEvent>> GetEventsAsync(string aggregateId, long fromVersion)
    {
        var eventStream = _eventStreams.GetValueOrDefault(aggregateId);
        if (eventStream == null)
            return ValueTask.FromResult<IReadOnlyList<IDomainEvent>>(new List<IDomainEvent>());

        var events = eventStream.Events.Skip((int)fromVersion).ToList();
        return ValueTask.FromResult<IReadOnlyList<IDomainEvent>>(events);
    }

    public ValueTask<EventStream?> GetEventStreamAsync(string aggregateId)
    {
        var eventStream = _eventStreams.GetValueOrDefault(aggregateId);
        return ValueTask.FromResult(eventStream);
    }

    public ValueTask<bool> EventStreamExistsAsync(string aggregateId)
    {
        return ValueTask.FromResult(_eventStreams.ContainsKey(aggregateId));
    }
}