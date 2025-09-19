using SportsbookLite.Contracts.Events;

namespace SportsbookLite.Infrastructure.Pulsar;

public interface IPulsarService
{
    ValueTask PublishAsync<T>(T eventData, string? topic = null) where T : IDomainEvent;
    
    ValueTask<IAsyncDisposable> SubscribeAsync<T>(
        string subscription, 
        Func<T, ValueTask> handler, 
        string? topic = null) where T : IDomainEvent;
    
    ValueTask<IAsyncDisposable> SubscribeAsync<T>(
        string subscription,
        IEventHandler<T> handler,
        string? topic = null) where T : IDomainEvent;
        
    ValueTask StartAsync(CancellationToken cancellationToken = default);
    
    ValueTask StopAsync(CancellationToken cancellationToken = default);
    
    ValueTask<bool> IsConnectedAsync();
    
    string GetTopicName<T>() where T : IDomainEvent;
    
    string GetTopicName(string eventType);
}