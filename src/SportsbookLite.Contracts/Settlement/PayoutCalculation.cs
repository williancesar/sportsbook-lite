using SportsbookLite.Contracts.Wallet;

namespace SportsbookLite.Contracts.Settlement;

[GenerateSerializer]
public sealed record PayoutCalculation(
    [property: Id(0)] Guid BetId,
    [property: Id(1)] Money OriginalStake,
    [property: Id(2)] decimal Odds,
    [property: Id(3)] Money PayoutAmount,
    [property: Id(4)] Money Profit,
    [property: Id(5)] bool IsWinning
)
{
    public static PayoutCalculation CreateWinning(Guid betId, Money stake, decimal odds)
    {
        var payoutAmount = Money.Create(stake.Amount * odds, stake.Currency);
        var profit = Money.Create(payoutAmount.Amount - stake.Amount, stake.Currency);
        return new PayoutCalculation(betId, stake, odds, payoutAmount, profit, true);
    }

    public static PayoutCalculation CreateLosing(Guid betId, Money stake, decimal odds) =>
        new(betId, stake, odds, Money.Zero(stake.Currency), Money.Create(-stake.Amount, stake.Currency), false);

    public static PayoutCalculation CreateVoid(Guid betId, Money stake, decimal odds) =>
        new(betId, stake, odds, stake, Money.Zero(stake.Currency), false);
}