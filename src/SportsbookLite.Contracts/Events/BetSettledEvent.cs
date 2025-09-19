using SportsbookLite.Contracts.Betting;
using SportsbookLite.Contracts.Wallet;

namespace SportsbookLite.Contracts.Events;

[GenerateSerializer]
public sealed record BetSettledEvent(
    [property: Id(0)] Guid Id,
    [property: Id(1)] DateTimeOffset Timestamp,
    [property: Id(2)] string AggregateId,
    [property: Id(3)] Guid BetId,
    [property: Id(4)] string UserId,
    [property: Id(5)] BetStatus FinalStatus,
    [property: Id(6)] Money? Payout,
    [property: Id(7)] string? SagaId = null,
    [property: Id(8)] Guid? EventId = null,
    [property: Id(9)] string? MarketId = null,
    [property: Id(10)] string? SelectionId = null
) : IDomainEvent;