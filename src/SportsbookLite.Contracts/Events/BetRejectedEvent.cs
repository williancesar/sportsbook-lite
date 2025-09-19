namespace SportsbookLite.Contracts.Events;

[GenerateSerializer]
public sealed record BetRejectedEvent(
    [property: Id(0)] Guid Id,
    [property: Id(1)] DateTimeOffset Timestamp,
    [property: Id(2)] string AggregateId,
    [property: Id(3)] Guid BetId,
    [property: Id(4)] string UserId,
    [property: Id(5)] string Reason
) : IDomainEvent;