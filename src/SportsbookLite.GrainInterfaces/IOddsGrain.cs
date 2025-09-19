using SportsbookLite.Contracts.Odds;

namespace SportsbookLite.GrainInterfaces;

public interface IOddsGrain : IGrainWithStringKey
{
    ValueTask<OddsSnapshot> GetCurrentOddsAsync();
    
    ValueTask<OddsSnapshot> UpdateOddsAsync(OddsUpdateRequest request);
    
    ValueTask<OddsHistory> GetOddsHistoryAsync(string selection);
    
    ValueTask<IReadOnlyDictionary<string, OddsHistory>> GetAllOddsHistoryAsync();
    
    ValueTask<OddsSnapshot> SuspendOddsAsync(string reason, string? suspendedBy = null);
    
    ValueTask<OddsSnapshot> ResumeOddsAsync(string reason, string? resumedBy = null);
    
    ValueTask<OddsVolatility> CalculateVolatilityAsync(TimeSpan window);
    
    ValueTask<OddsVolatility> GetCurrentVolatilityAsync();
    
    ValueTask<OddsSnapshot> LockOddsForBetAsync(string betId, string selection);
    
    ValueTask<OddsSnapshot> UnlockOddsAsync(string betId);
    
    ValueTask<bool> IsSelectionLockedAsync(string selection);
    
    ValueTask<IReadOnlyDictionary<string, HashSet<string>>> GetLockedSelectionsAsync();
    
    ValueTask<OddsSnapshot> InitializeMarketAsync(Dictionary<string, decimal> initialOdds, OddsSource source = OddsSource.Manual);
    
    ValueTask<bool> IsMarketSuspendedAsync();
    
    ValueTask<decimal> GetVolatilityScoreAsync(TimeSpan window);
}