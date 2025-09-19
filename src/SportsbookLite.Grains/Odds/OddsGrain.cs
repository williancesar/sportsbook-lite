using Orleans.Runtime;
using SportsbookLite.Contracts.Events;
using SportsbookLite.Contracts.Odds;
using SportsbookLite.GrainInterfaces;
using OddsValue = SportsbookLite.Contracts.Odds.Odds;

namespace SportsbookLite.Grains.Odds;

public sealed class OddsGrain : Grain, IOddsGrain
{
    private readonly OddsState _state = new();
    private readonly ILogger<OddsGrain> _logger;

    public OddsGrain(ILogger<OddsGrain> logger)
    {
        _logger = logger;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.MarketId))
        {
            _state.MarketId = this.GetPrimaryKeyString();
        }
        
        await base.OnActivateAsync(cancellationToken);
    }

    public ValueTask<OddsSnapshot> GetCurrentOddsAsync()
    {
        return ValueTask.FromResult(_state.CreateSnapshot());
    }

    public async ValueTask<OddsSnapshot> UpdateOddsAsync(OddsUpdateRequest request)
    {
        if (!request.IsValidUpdate())
        {
            throw new ArgumentException("Invalid odds update request");
        }

        if (_state.IsSuspended)
        {
            _logger.LogWarning("Attempted to update suspended odds for market {MarketId}", request.MarketId);
            throw new InvalidOperationException($"Cannot update odds for suspended market {request.MarketId}");
        }

        var previousOdds = new Dictionary<string, OddsValue>(_state.CurrentOdds);
        var previousVolatility = _state.CurrentVolatility;

        _state.UpdateOdds(request);
        
        var newVolatility = _state.CalculateVolatility(_state.VolatilityWindow);
        
        if (newVolatility >= OddsVolatility.Extreme && !_state.IsSuspended)
        {
            _state.Suspend("Automatic suspension due to extreme volatility", null);
            
            var suspensionEvent = OddsSuspendedEvent.CreateAutomatic(
                _state.MarketId,
                newVolatility,
                "Automatic suspension due to extreme volatility");
                
            await PublishEventAsync(suspensionEvent);
        }
        
        if (newVolatility != previousVolatility)
        {
            var volatilityEvent = OddsVolatilityChangedEvent.Create(
                _state.MarketId,
                previousVolatility,
                newVolatility,
                _state.GetVolatilityScore(_state.VolatilityWindow),
                _state.OddsHistories.Values.Sum(h => h.GetUpdatesInTimeWindow(_state.VolatilityWindow).Count()),
                _state.VolatilityWindow);
                
            await PublishEventAsync(volatilityEvent);
        }

        var oddsUpdatedEvent = OddsUpdatedEvent.Create(
            _state.MarketId,
            previousOdds,
            _state.CurrentOdds,
            request.Source,
            request.Reason,
            request.UpdatedBy);
            
        await PublishEventAsync(oddsUpdatedEvent);

        _logger.LogInformation("Updated odds for market {MarketId} with {SelectionCount} selections", 
            request.MarketId, request.SelectionOdds.Count);

        return _state.CreateSnapshot();
    }

    public ValueTask<OddsHistory> GetOddsHistoryAsync(string selection)
    {
        if (_state.OddsHistories.TryGetValue(selection, out var history))
        {
            return ValueTask.FromResult(history);
        }

        throw new ArgumentException($"No history found for selection '{selection}' in market '{_state.MarketId}'");
    }

    public ValueTask<IReadOnlyDictionary<string, OddsHistory>> GetAllOddsHistoryAsync()
    {
        return ValueTask.FromResult<IReadOnlyDictionary<string, OddsHistory>>(_state.OddsHistories);
    }

    public async ValueTask<OddsSnapshot> SuspendOddsAsync(string reason, string? suspendedBy = null)
    {
        if (_state.IsSuspended)
        {
            _logger.LogWarning("Market {MarketId} is already suspended", _state.MarketId);
            return _state.CreateSnapshot();
        }

        _state.Suspend(reason, suspendedBy);

        var suspensionEvent = string.IsNullOrEmpty(suspendedBy)
            ? OddsSuspendedEvent.CreateAutomatic(_state.MarketId, _state.CurrentVolatility, reason)
            : OddsSuspendedEvent.CreateManual(_state.MarketId, reason, suspendedBy, _state.CurrentVolatility);

        await PublishEventAsync(suspensionEvent);

        _logger.LogInformation("Suspended odds for market {MarketId}: {Reason}", _state.MarketId, reason);

        return _state.CreateSnapshot();
    }

    public async ValueTask<OddsSnapshot> ResumeOddsAsync(string reason, string? resumedBy = null)
    {
        if (!_state.IsSuspended)
        {
            _logger.LogWarning("Market {MarketId} is not currently suspended", _state.MarketId);
            return _state.CreateSnapshot();
        }

        var suspensionStartTime = _state.SuspensionTime;
        _state.Resume(reason);

        var resumeEvent = OddsResumedEvent.Create(
            _state.MarketId,
            _state.CurrentVolatility,
            reason,
            resumedBy,
            suspensionStartTime);

        await PublishEventAsync(resumeEvent);

        _logger.LogInformation("Resumed odds for market {MarketId}: {Reason}", _state.MarketId, reason);

        return _state.CreateSnapshot();
    }

    public ValueTask<OddsVolatility> CalculateVolatilityAsync(TimeSpan window)
    {
        var volatility = _state.CalculateVolatility(window);
        return ValueTask.FromResult(volatility);
    }

    public ValueTask<OddsVolatility> GetCurrentVolatilityAsync()
    {
        return ValueTask.FromResult(_state.CurrentVolatility);
    }

    public async ValueTask<OddsSnapshot> LockOddsForBetAsync(string betId, string selection)
    {
        if (_state.IsSuspended)
        {
            throw new InvalidOperationException($"Cannot lock odds for suspended market {_state.MarketId}");
        }

        if (!_state.CurrentOdds.ContainsKey(selection))
        {
            throw new ArgumentException($"Selection '{selection}' not found in market '{_state.MarketId}'");
        }

        _state.LockSelection(selection, betId);

        _logger.LogDebug("Locked selection {Selection} for bet {BetId} in market {MarketId}", 
            selection, betId, _state.MarketId);

        return _state.CreateSnapshot();
    }

    public async ValueTask<OddsSnapshot> UnlockOddsAsync(string betId)
    {
        _state.UnlockSelection(betId);

        _logger.LogDebug("Unlocked odds for bet {BetId} in market {MarketId}", betId, _state.MarketId);

        return _state.CreateSnapshot();
    }

    public ValueTask<bool> IsSelectionLockedAsync(string selection)
    {
        return ValueTask.FromResult(_state.IsSelectionLocked(selection));
    }

    public ValueTask<IReadOnlyDictionary<string, HashSet<string>>> GetLockedSelectionsAsync()
    {
        return ValueTask.FromResult<IReadOnlyDictionary<string, HashSet<string>>>(_state.LockedSelections);
    }

    public async ValueTask<OddsSnapshot> InitializeMarketAsync(Dictionary<string, decimal> initialOdds, OddsSource source = OddsSource.Manual)
    {
        if (_state.CurrentOdds.Any())
        {
            throw new InvalidOperationException($"Market {_state.MarketId} is already initialized");
        }

        _state.Initialize(this.GetPrimaryKeyString(), initialOdds, source);

        _logger.LogInformation("Initialized market {MarketId} with {SelectionCount} selections", 
            _state.MarketId, initialOdds.Count);

        return _state.CreateSnapshot();
    }

    public ValueTask<bool> IsMarketSuspendedAsync()
    {
        return ValueTask.FromResult(_state.IsSuspended);
    }

    public ValueTask<decimal> GetVolatilityScoreAsync(TimeSpan window)
    {
        return ValueTask.FromResult(_state.GetVolatilityScore(window));
    }

    private async ValueTask PublishEventAsync<T>(T domainEvent) where T : IDomainEvent
    {
        try
        {
            _logger.LogDebug("Publishing event {EventType} for aggregate {AggregateId}", 
                typeof(T).Name, domainEvent.AggregateId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish event {EventType} for aggregate {AggregateId}", 
                typeof(T).Name, domainEvent.AggregateId);
        }
    }
}