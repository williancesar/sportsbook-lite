using SportsbookLite.Contracts.Betting;
using SportsbookLite.Contracts.Settlement;
using SportsbookLite.Contracts.Wallet;

namespace SportsbookLite.GrainInterfaces;

public interface IBetGrain : IGrainWithGuidKey
{
    ValueTask<BetResult> PlaceBetAsync(PlaceBetRequest request);
    ValueTask<Bet?> GetBetDetailsAsync();
    ValueTask<BetResult> VoidBetAsync(string reason);
    ValueTask<BetResult> CashOutAsync();
    ValueTask<IReadOnlyList<Bet>> GetBetHistoryAsync();
    ValueTask<BetResult> SettleBetAsync(BetStatus finalStatus, Money? payout, string sagaId);
    ValueTask<bool> CanBeSettledAsync();
}