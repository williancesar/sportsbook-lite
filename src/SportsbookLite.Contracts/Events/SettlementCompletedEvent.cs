using SportsbookLite.Contracts.Wallet;

namespace SportsbookLite.Contracts.Events;

[GenerateSerializer]
public sealed record SettlementCompletedEvent(
    [property: Id(0)] Guid Id,
    [property: Id(1)] DateTimeOffset Timestamp,
    [property: Id(2)] string AggregateId,
    [property: Id(3)] Guid EventId,
    [property: Id(4)] string MarketId,
    [property: Id(5)] string SagaId,
    [property: Id(6)] int SuccessfulSettlements,
    [property: Id(7)] int FailedSettlements,
    [property: Id(8)] Money TotalPayouts,
    [property: Id(9)] TimeSpan ProcessingDuration
) : IDomainEvent;