namespace SportsbookLite.Grains.Betting;

public sealed class BetManagerGrain : Grain, IBetManagerGrain
{
    private readonly ILogger<BetManagerGrain> _logger;
    private BetManagerState _state = new();

    public BetManagerGrain(ILogger<BetManagerGrain> logger)
    {
        _logger = logger;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        var userId = this.GetPrimaryKeyString();
        if (_state.UserId == string.Empty)
        {
            _state.UserId = userId;
        }
        
        await base.OnActivateAsync(cancellationToken);
    }

    public async ValueTask<IReadOnlyList<Bet>> GetUserBetsAsync(int limit = 50)
    {
        var betTasks = _state.BetIds
            .Take(limit)
            .Select(async betId =>
            {
                var betGrain = GrainFactory.GetGrain<IBetGrain>(betId);
                return await betGrain.GetBetDetailsAsync();
            });

        var bets = await Task.WhenAll(betTasks);
        return bets.Where(bet => bet != null).Cast<Bet>().ToList();
    }

    public async ValueTask<IReadOnlyList<Bet>> GetActiveBetsAsync()
    {
        var betTasks = _state.BetIds
            .Select(async betId =>
            {
                var betGrain = GrainFactory.GetGrain<IBetGrain>(betId);
                return await betGrain.GetBetDetailsAsync();
            });

        var bets = await Task.WhenAll(betTasks);
        return bets
            .Where(bet => bet != null && !bet.IsSettled)
            .Cast<Bet>()
            .ToList();
    }

    public async ValueTask<IReadOnlyList<Bet>> GetBetHistoryAsync(int limit = 100)
    {
        var betTasks = _state.BetIds
            .Take(limit)
            .Select(async betId =>
            {
                var betGrain = GrainFactory.GetGrain<IBetGrain>(betId);
                var history = await betGrain.GetBetHistoryAsync();
                return history.LastOrDefault();
            });

        var bets = await Task.WhenAll(betTasks);
        return bets.Where(bet => bet != null).Cast<Bet>().OrderByDescending(b => b.PlacedAt).ToList();
    }

    public ValueTask AddBetAsync(Guid betId)
    {
        if (!_state.BetIds.Contains(betId))
        {
            _state.BetIds.Add(betId);
            _state.LastModified = DateTimeOffset.UtcNow;
            
            _logger.LogDebug("Added bet {BetId} to user {UserId}", betId, _state.UserId);
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask<bool> HasBetAsync(Guid betId)
    {
        return ValueTask.FromResult(_state.BetIds.Contains(betId));
    }
}

[GenerateSerializer]
public sealed class BetManagerState
{
    [Id(0)]
    public string UserId { get; set; } = string.Empty;

    [Id(1)]
    public List<Guid> BetIds { get; set; } = new();

    [Id(2)]
    public DateTimeOffset LastModified { get; set; } = DateTimeOffset.UtcNow;
}