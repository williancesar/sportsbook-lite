using SportsbookLite.Contracts.Events;
using SportsbookLite.GrainInterfaces;

namespace SportsbookLite.Grains.Events;

public sealed class SportEventGrain : Grain, ISportEventGrain
{
    private EventState _state = new();

    public ValueTask<SportEvent> CreateAsync(SportEvent sportEvent)
    {
        if (_state.IsInitialized)
        {
            throw new InvalidOperationException("Event already exists");
        }

        _state.Event = sportEvent;
        var createdEvent = EventCreatedEvent.Create(sportEvent);
        _state.AddEvent(createdEvent);

        return ValueTask.FromResult(sportEvent);
    }
    
    public ValueTask<SportEvent?> GetEventAsync()
    {
        return ValueTask.FromResult(_state.Event);
    }

    public ValueTask<SportEvent> CreateEventAsync(
        string name,
        SportType sportType,
        string competition,
        DateTimeOffset startTime,
        Dictionary<string, string> participants)
    {
        if (_state.IsInitialized)
        {
            throw new InvalidOperationException("Event already exists");
        }

        var sportEvent = SportEvent.Create(name, sportType, competition, startTime, participants);
        _state.Event = sportEvent;

        var createdEvent = EventCreatedEvent.Create(sportEvent);
        _state.AddEvent(createdEvent);

        return ValueTask.FromResult(sportEvent);
    }

    public ValueTask<SportEvent> UpdateEventAsync(
        string? name = null,
        DateTimeOffset? startTime = null,
        Dictionary<string, string>? participants = null)
    {
        if (!_state.IsInitialized)
        {
            throw new InvalidOperationException("Event does not exist");
        }

        var currentEvent = _state.Event!.Value;

        if (currentEvent.Status != EventStatus.Scheduled)
        {
            throw new InvalidOperationException($"Cannot update event in status {currentEvent.Status}");
        }

        var updatedEvent = currentEvent;

        if (name != null)
        {
            updatedEvent = updatedEvent with { Name = name, LastModified = DateTimeOffset.UtcNow };
        }

        if (startTime.HasValue)
        {
            updatedEvent = updatedEvent.WithUpdatedTime(startTime.Value);
        }

        if (participants != null)
        {
            updatedEvent = updatedEvent with { Participants = participants, LastModified = DateTimeOffset.UtcNow };
        }

        _state.Event = updatedEvent;
        return ValueTask.FromResult(updatedEvent);
    }

    public ValueTask<SportEvent> StartEventAsync()
    {
        if (!_state.IsInitialized)
        {
            throw new InvalidOperationException("Event does not exist");
        }

        var currentEvent = _state.Event!.Value;

        if (!currentEvent.CanTransitionTo(EventStatus.Live))
        {
            throw new InvalidOperationException($"Cannot start event from status {currentEvent.Status}");
        }

        var startedEvent = currentEvent.WithStatus(EventStatus.Live);
        _state.Event = startedEvent;

        var startedDomainEvent = EventStartedEvent.Create(startedEvent.Id, DateTimeOffset.UtcNow);
        _state.AddEvent(startedDomainEvent);

        return ValueTask.FromResult(startedEvent);
    }

    public ValueTask<SportEvent> CompleteEventAsync(EventResult? result = null)
    {
        if (!_state.IsInitialized)
        {
            throw new InvalidOperationException("Event does not exist");
        }

        var currentEvent = _state.Event!.Value;

        if (!currentEvent.CanTransitionTo(EventStatus.Completed))
        {
            throw new InvalidOperationException($"Cannot complete event from status {currentEvent.Status}");
        }

        var endTime = DateTimeOffset.UtcNow;
        var completedEvent = currentEvent.WithStatus(EventStatus.Completed, endTime);
        _state.Event = completedEvent;

        if (result.HasValue)
        {
            _state.Result = result.Value;
        }

        _state.SuspendAllMarkets("Event completed");

        var completedDomainEvent = EventCompletedEvent.Create(completedEvent.Id, endTime, result);
        _state.AddEvent(completedDomainEvent);

        return ValueTask.FromResult(completedEvent);
    }

    public ValueTask<SportEvent> CancelEventAsync(string reason)
    {
        if (!_state.IsInitialized)
        {
            throw new InvalidOperationException("Event does not exist");
        }

        var currentEvent = _state.Event!.Value;

        if (!currentEvent.CanTransitionTo(EventStatus.Cancelled))
        {
            throw new InvalidOperationException($"Cannot cancel event from status {currentEvent.Status}");
        }

        var cancelledEvent = currentEvent.WithStatus(EventStatus.Cancelled);
        _state.Event = cancelledEvent;

        _state.SuspendAllMarkets("Event cancelled");

        var cancelledDomainEvent = EventCancelledEvent.Create(cancelledEvent.Id, reason);
        _state.AddEvent(cancelledDomainEvent);

        return ValueTask.FromResult(cancelledEvent);
    }

    public ValueTask<SportEvent> GetEventDetailsAsync()
    {
        if (!_state.IsInitialized)
        {
            throw new InvalidOperationException("Event does not exist");
        }

        return ValueTask.FromResult(_state.Event!.Value);
    }

    public ValueTask<Market> AddMarketAsync(
        string name,
        string description,
        Dictionary<string, decimal> outcomes)
    {
        if (!_state.IsInitialized)
        {
            throw new InvalidOperationException("Event does not exist");
        }

        var currentEvent = _state.Event!.Value;
        var market = Market.Create(currentEvent.Id, name, description, outcomes);

        _state.AddMarket(market);
        return ValueTask.FromResult(market);
    }

    public ValueTask<Market> UpdateMarketStatusAsync(Guid marketId, MarketStatus status)
    {
        if (!_state.IsInitialized)
        {
            throw new InvalidOperationException("Event does not exist");
        }

        if (!_state.TryGetMarket(marketId, out var market))
        {
            throw new InvalidOperationException("Market does not exist");
        }

        if (!market.CanTransitionTo(status))
        {
            throw new InvalidOperationException($"Cannot transition market from {market.Status} to {status}");
        }

        var updatedMarket = market.WithStatus(status);
        _state.UpdateMarket(updatedMarket);

        if (status == MarketStatus.Suspended)
        {
            var suspendedEvent = MarketSuspendedEvent.Create(
                _state.Event!.Value.Id, 
                marketId, 
                "Manual suspension");
            _state.AddEvent(suspendedEvent);
        }

        return ValueTask.FromResult(updatedMarket);
    }

    public ValueTask<IReadOnlyList<Market>> GetMarketsAsync()
    {
        if (!_state.IsInitialized)
        {
            throw new InvalidOperationException("Event does not exist");
        }

        var markets = _state.Markets.Values.ToList();
        return ValueTask.FromResult<IReadOnlyList<Market>>(markets);
    }

    public ValueTask<Market> SetMarketResultAsync(Guid marketId, string winningOutcome)
    {
        if (!_state.IsInitialized)
        {
            throw new InvalidOperationException("Event does not exist");
        }

        if (!_state.TryGetMarket(marketId, out var market))
        {
            throw new InvalidOperationException("Market does not exist");
        }

        if (market.Status != MarketStatus.Closed)
        {
            throw new InvalidOperationException($"Market must be closed before settling. Current status: {market.Status}");
        }

        if (!market.Outcomes.ContainsKey(winningOutcome))
        {
            throw new InvalidOperationException($"Invalid winning outcome: {winningOutcome}");
        }

        var settledMarket = market.WithWinner(winningOutcome);
        _state.UpdateMarket(settledMarket);

        var settledEvent = MarketSettledEvent.Create(
            _state.Event!.Value.Id,
            marketId,
            winningOutcome);
        _state.AddEvent(settledEvent);

        return ValueTask.FromResult(settledMarket);
    }

    public ValueTask<EventResult> SetResultAsync(Dictionary<string, object> results, bool isOfficial = true)
    {
        if (!_state.IsInitialized)
        {
            throw new InvalidOperationException("Event does not exist");
        }

        var currentEvent = _state.Event!.Value;
        var eventResult = EventResult.Create(currentEvent.Id, results, isOfficial);

        _state.Result = eventResult;
        return ValueTask.FromResult(eventResult);
    }

    public ValueTask<EventResult?> GetResultAsync()
    {
        return ValueTask.FromResult(_state.Result);
    }
}