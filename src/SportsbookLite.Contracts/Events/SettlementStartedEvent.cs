namespace SportsbookLite.Contracts.Events;

[GenerateSerializer]
public sealed record SettlementStartedEvent(
    [property: Id(0)] Guid Id,
    [property: Id(1)] DateTimeOffset Timestamp,
    [property: Id(2)] string AggregateId,
    [property: Id(3)] Guid EventId,
    [property: Id(4)] string MarketId,
    [property: Id(5)] string WinningSelectionId,
    [property: Id(6)] int TotalBetsToSettle,
    [property: Id(7)] string SagaId
) : IDomainEvent;