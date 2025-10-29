using SportsbookLite.Contracts.Betting;

namespace SportsbookLite.GrainInterfaces;

public interface IBetManagerGrain : IGrainWithStringKey
{
    ValueTask<IReadOnlyList<Bet>> GetUserBetsAsync(int limit = 50);
    ValueTask<IReadOnlyList<Bet>> GetActiveBetsAsync();
    ValueTask<IReadOnlyList<Bet>> GetBetHistoryAsync(int limit = 100);
    ValueTask AddBetAsync(Guid betId);
    ValueTask<bool> HasBetAsync(Guid betId);
}