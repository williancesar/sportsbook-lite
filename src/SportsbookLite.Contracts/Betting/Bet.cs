using SportsbookLite.Contracts.Wallet;

namespace SportsbookLite.Contracts.Betting;

[GenerateSerializer]
public sealed record Bet(
    [property: Id(0)] Guid Id,
    [property: Id(1)] string UserId,
    [property: Id(2)] Guid EventId,
    [property: Id(3)] string MarketId,
    [property: Id(4)] string SelectionId,
    [property: Id(5)] Money Amount,
    [property: Id(6)] decimal Odds,
    [property: Id(7)] BetStatus Status,
    [property: Id(8)] BetType Type,
    [property: Id(9)] DateTimeOffset PlacedAt,
    [property: Id(10)] DateTimeOffset? SettledAt,
    [property: Id(11)] Money? Payout,
    [property: Id(12)] string? RejectionReason,
    [property: Id(13)] string? VoidReason
)
{
    public Money PotentialPayout => Money.Create(Amount.Amount * Odds, Amount.Currency);
    
    public bool IsSettled => Status is BetStatus.Won or BetStatus.Lost or BetStatus.Void;
    
    public bool CanBeVoided => Status is BetStatus.Accepted or BetStatus.Pending;
    
    public bool CanBeCashedOut => Status is BetStatus.Accepted;
}