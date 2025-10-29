using SportsbookLite.Contracts.Betting;
using SportsbookLite.Contracts.Wallet;

namespace SportsbookLite.Contracts.Events;

[GenerateSerializer]
public sealed record BetPlacedEvent(
    [property: Id(0)] Guid Id,
    [property: Id(1)] DateTimeOffset Timestamp,
    [property: Id(2)] string AggregateId,
    [property: Id(3)] Guid BetId,
    [property: Id(4)] string UserId,
    [property: Id(5)] Guid EventId,
    [property: Id(6)] string MarketId,
    [property: Id(7)] string SelectionId,
    [property: Id(8)] Money Amount,
    [property: Id(9)] decimal AcceptableOdds,
    [property: Id(10)] BetType Type
) : IDomainEvent;