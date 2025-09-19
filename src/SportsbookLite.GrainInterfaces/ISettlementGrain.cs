using SportsbookLite.Contracts.Settlement;

namespace SportsbookLite.GrainInterfaces;

public interface ISettlementGrain : IGrainWithStringKey
{
    ValueTask<SettlementResult> SettleMarketAsync(SettlementRequest request);
    ValueTask<SettlementResult> ReverseSettlementAsync(SettlementReversal reversal);
    ValueTask<SettlementStatus> GetSettlementStatusAsync();
    ValueTask<IReadOnlyList<Guid>> GetAffectedBetsAsync();
    ValueTask<bool> IsSettlementInProgressAsync();
}