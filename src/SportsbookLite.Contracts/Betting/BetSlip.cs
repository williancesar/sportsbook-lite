using SportsbookLite.Contracts.Wallet;

namespace SportsbookLite.Contracts.Betting;

[GenerateSerializer]
public sealed record BetSlip(
    [property: Id(0)] Guid Id,
    [property: Id(1)] string UserId,
    [property: Id(2)] IReadOnlyList<BetSelection> Selections,
    [property: Id(3)] Money TotalStake,
    [property: Id(4)] BetType Type,
    [property: Id(5)] DateTimeOffset CreatedAt
)
{
    public Money PotentialPayout => Type switch
    {
        BetType.Single => Selections.Count == 1
            ? Money.Create(TotalStake.Amount * Selections[0].Odds, TotalStake.Currency)
            : throw new InvalidOperationException("Single bet must have exactly one selection"),
        BetType.Accumulator => Money.Create(
            TotalStake.Amount * Selections.Aggregate(1m, (acc, sel) => acc * sel.Odds),
            TotalStake.Currency),
        BetType.System => throw new NotImplementedException("System bets not yet implemented"),
        _ => throw new ArgumentOutOfRangeException()
    };
}

[GenerateSerializer]
public sealed record BetSelection(
    [property: Id(0)] Guid EventId,
    [property: Id(1)] string MarketId,
    [property: Id(2)] string SelectionId,
    [property: Id(3)] decimal Odds,
    [property: Id(4)] string SelectionName
);