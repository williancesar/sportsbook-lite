using SportsbookLite.Contracts.Events;

namespace SportsbookLite.Infrastructure.Pulsar;

public interface IEventHandler<in T> where T : IDomainEvent
{
    ValueTask HandleAsync(T eventData, CancellationToken cancellationToken = default);
}

[AttributeUsage(AttributeTargets.Class)]
public sealed class EventHandlerAttribute : Attribute
{
    public string? SubscriptionName { get; }

    public EventHandlerAttribute(string? subscriptionName = null)
    {
        SubscriptionName = subscriptionName;
    }
}