using SportsbookLite.Contracts.Wallet;

namespace SportsbookLite.Contracts.Events;

[GenerateSerializer]
public sealed record PayoutProcessedEvent(
    [property: Id(0)] Guid Id,
    [property: Id(1)] DateTimeOffset Timestamp,
    [property: Id(2)] string AggregateId,
    [property: Id(3)] Guid BetId,
    [property: Id(4)] string UserId,
    [property: Id(5)] Money PayoutAmount,
    [property: Id(6)] string TransactionId,
    [property: Id(7)] string SagaId
) : IDomainEvent;