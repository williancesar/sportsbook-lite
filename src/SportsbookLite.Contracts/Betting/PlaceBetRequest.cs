using SportsbookLite.Contracts.Wallet;

namespace SportsbookLite.Contracts.Betting;

[GenerateSerializer]
public sealed record PlaceBetRequest(
    [property: Id(0)] Guid BetId,
    [property: Id(1)] string UserId,
    [property: Id(2)] Guid EventId,
    [property: Id(3)] string MarketId,
    [property: Id(4)] string SelectionId,
    [property: Id(5)] Money Amount,
    [property: Id(6)] decimal AcceptableOdds,
    [property: Id(7)] BetType Type = BetType.Single
)
{
    public bool IsValid()
    {
        return BetId != Guid.Empty &&
               !string.IsNullOrWhiteSpace(UserId) &&
               EventId != Guid.Empty &&
               !string.IsNullOrWhiteSpace(MarketId) &&
               !string.IsNullOrWhiteSpace(SelectionId) &&
               Amount.Amount > 0 &&
               AcceptableOdds > 0;
    }
}