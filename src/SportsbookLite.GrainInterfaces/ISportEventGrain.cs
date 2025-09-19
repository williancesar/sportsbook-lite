using SportsbookLite.Contracts.Events;

namespace SportsbookLite.GrainInterfaces;

public interface ISportEventGrain : IGrainWithGuidKey
{
    ValueTask<SportEvent> CreateAsync(SportEvent sportEvent);
    
    ValueTask<SportEvent?> GetEventAsync();
    
    ValueTask<SportEvent> CreateEventAsync(
        string name,
        SportType sportType,
        string competition,
        DateTimeOffset startTime,
        Dictionary<string, string> participants);
    
    ValueTask<SportEvent> UpdateEventAsync(
        string? name = null,
        DateTimeOffset? startTime = null,
        Dictionary<string, string>? participants = null);
    
    ValueTask<SportEvent> StartEventAsync();
    
    ValueTask<SportEvent> CompleteEventAsync(EventResult? result = null);
    
    ValueTask<SportEvent> CancelEventAsync(string reason);
    
    ValueTask<SportEvent> GetEventDetailsAsync();
    
    ValueTask<Market> AddMarketAsync(
        string name,
        string description,
        Dictionary<string, decimal> outcomes);
    
    ValueTask<Market> UpdateMarketStatusAsync(Guid marketId, MarketStatus status);
    
    ValueTask<IReadOnlyList<Market>> GetMarketsAsync();
    
    ValueTask<Market> SetMarketResultAsync(Guid marketId, string winningOutcome);
    
    ValueTask<EventResult> SetResultAsync(Dictionary<string, object> results, bool isOfficial = true);
    
    ValueTask<EventResult?> GetResultAsync();
}