namespace SportsbookLite.Contracts.Events;

[GenerateSerializer]
public sealed record SettlementFailedEvent(
    [property: Id(0)] Guid Id,
    [property: Id(1)] DateTimeOffset Timestamp,
    [property: Id(2)] string AggregateId,
    [property: Id(3)] Guid EventId,
    [property: Id(4)] string MarketId,
    [property: Id(5)] string SagaId,
    [property: Id(6)] string ErrorMessage,
    [property: Id(7)] int AttemptNumber,
    [property: Id(8)] TimeSpan ProcessingDuration,
    [property: Id(9)] bool IsRetryable
) : IDomainEvent;