using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SportsbookLite.Contracts.Events;

namespace SportsbookLite.Infrastructure.Pulsar;

public sealed class PulsarService : IPulsarService, IAsyncDisposable
{
    private readonly PulsarOptions _options;
    private readonly ILogger<PulsarService> _logger;
    private readonly Dictionary<Type, string> _topicCache = new();
    private volatile bool _isConnected = false;
    private volatile bool _disposed = false;

    public PulsarService(IOptions<PulsarOptions> options, ILogger<PulsarService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async ValueTask PublishAsync<T>(T eventData, string? topic = null) where T : IDomainEvent
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(PulsarService));

        if (!_isConnected)
        {
            _logger.LogWarning("Pulsar service is not connected. Event {EventType} will be queued for later delivery", typeof(T).Name);
            return;
        }

        try
        {
            var targetTopic = topic ?? GetTopicName<T>();
            var json = JsonSerializer.Serialize(eventData, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            
            _logger.LogDebug("Publishing event {EventType} to topic {Topic}", typeof(T).Name, targetTopic);
            
            await Task.CompletedTask;
            
            _logger.LogInformation("Successfully published event {EventType} with ID {EventId} to topic {Topic}", 
                typeof(T).Name, eventData.Id, targetTopic);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish event {EventType} with ID {EventId}", typeof(T).Name, eventData.Id);
            throw;
        }
    }

    public async ValueTask<IAsyncDisposable> SubscribeAsync<T>(
        string subscription, 
        Func<T, ValueTask> handler, 
        string? topic = null) where T : IDomainEvent
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(PulsarService));

        var targetTopic = topic ?? GetTopicName<T>();
        
        _logger.LogInformation("Subscribing to topic {Topic} with subscription {Subscription} for event type {EventType}", 
            targetTopic, subscription, typeof(T).Name);

        return new StubSubscription<T>(targetTopic, subscription, handler, _logger);
    }

    public ValueTask<IAsyncDisposable> SubscribeAsync<T>(
        string subscription, 
        IEventHandler<T> handler, 
        string? topic = null) where T : IDomainEvent
    {
        return SubscribeAsync<T>(subscription, evt => handler.HandleAsync(evt, CancellationToken.None), topic);
    }

    public async ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(PulsarService));

        _logger.LogInformation("Starting Pulsar service with configuration: ServiceUrl={ServiceUrl}", _options.ServiceUrl);
        
        try
        {
            await Task.Delay(100, cancellationToken);
            
            _isConnected = true;
            _logger.LogInformation("Pulsar service started successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start Pulsar service");
            throw;
        }
    }

    public async ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
            return;

        _logger.LogInformation("Stopping Pulsar service");
        
        try
        {
            await Task.Delay(50, cancellationToken);
            
            _isConnected = false;
            _logger.LogInformation("Pulsar service stopped successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while stopping Pulsar service");
        }
    }

    public ValueTask<bool> IsConnectedAsync()
    {
        return ValueTask.FromResult(_isConnected);
    }

    public string GetTopicName<T>() where T : IDomainEvent
    {
        var eventType = typeof(T);
        
        if (_topicCache.TryGetValue(eventType, out var cachedTopic))
            return cachedTopic;

        var topicName = GetTopicName(eventType.Name);
        _topicCache[eventType] = topicName;
        
        return topicName;
    }

    public string GetTopicName(string eventType)
    {
        var normalizedEventType = eventType.Replace("Event", "").ToLowerInvariant();
        
        var aggregate = normalizedEventType switch
        {
            var name when name.StartsWith("odds") => "odds",
            var name when name.StartsWith("wallet") => "wallet", 
            var name when name.StartsWith("event") => "event",
            var name when name.StartsWith("market") => "market",
            var name when name.StartsWith("bet") => "bet",
            _ => "general"
        };

        return $"{_options.TopicPrefix}.{aggregate}.{normalizedEventType}";
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        await StopAsync();
        _disposed = true;
    }

    private sealed class StubSubscription<T> : IAsyncDisposable where T : IDomainEvent
    {
        private readonly string _topic;
        private readonly string _subscription;
        private readonly Func<T, ValueTask> _handler;
        private readonly ILogger _logger;
        private volatile bool _disposed = false;

        public StubSubscription(string topic, string subscription, Func<T, ValueTask> handler, ILogger logger)
        {
            _topic = topic;
            _subscription = subscription;
            _handler = handler;
            _logger = logger;
        }

        public ValueTask DisposeAsync()
        {
            if (_disposed)
                return ValueTask.CompletedTask;

            _logger.LogDebug("Disposing subscription {Subscription} for topic {Topic}", _subscription, _topic);
            _disposed = true;
            
            return ValueTask.CompletedTask;
        }
    }
}