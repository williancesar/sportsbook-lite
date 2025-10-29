namespace SportsbookLite.Grains.Betting;

public sealed class BetGrain : Grain, IBetGrain
{
    private readonly IEventStore _eventStore;
    private readonly ILogger<BetGrain> _logger;
    private BetAggregate? _aggregate;

    public BetGrain(IEventStore eventStore, ILogger<BetGrain> logger)
    {
        _eventStore = eventStore;
        _logger = logger;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        var betId = this.GetPrimaryKey();
        
        var eventStream = await _eventStore.GetEventStreamAsync(betId.ToString());
        if (eventStream != null && eventStream.Events.Any())
        {
            _aggregate = BetAggregate.Create(betId);
            foreach (var domainEvent in eventStream.Events)
            {
                _aggregate.Apply(domainEvent);
            }
        }
        // Don't create aggregate if no events exist - let PlaceBetAsync create it

        await base.OnActivateAsync(cancellationToken);
    }

    public async ValueTask<BetResult> PlaceBetAsync(PlaceBetRequest request)
    {
        try
        {
            if (!request.IsValid())
                return BetResult.Failed("Invalid bet request");

            // Create aggregate if it doesn't exist (new bet)
            if (_aggregate == null)
            {
                _aggregate = BetAggregate.Create(this.GetPrimaryKey());
            }
            else if (_aggregate.GetState().Status != BetStatus.Pending)
            {
                return BetResult.Failed("Bet has already been processed");
            }

            var userWalletGrain = GrainFactory.GetGrain<IUserWalletGrain>(request.UserId);
            var oddsGrain = GrainFactory.GetGrain<IOddsGrain>(request.MarketId);

            var availableBalance = await userWalletGrain.GetAvailableBalanceAsync();
            var currentOdds = await oddsGrain.GetCurrentOddsAsync();

            if (!availableBalance.IsGreaterThanOrEqualTo(request.Amount))
                return BetResult.Failed("Insufficient balance");

            if (!currentOdds.Selections.TryGetValue(request.SelectionId, out var currentSelectionOdds))
                return BetResult.Failed("Selection not found");

            if (currentSelectionOdds.Decimal < request.AcceptableOdds)
                return BetResult.Failed("Odds have changed and are no longer acceptable");

            var reservationResult = await userWalletGrain.ReserveAsync(request.Amount, request.BetId.ToString());
            if (!reservationResult.IsSuccess)
                return BetResult.Failed($"Failed to reserve funds: {reservationResult.ErrorMessage}");

            try
            {
                await oddsGrain.LockOddsForBetAsync(request.BetId.ToString(), request.SelectionId);

                _aggregate.PlaceBet(request);
                _aggregate.AcceptBet(currentSelectionOdds.Decimal);

                await PersistEventsAsync();

                await userWalletGrain.CommitReservationAsync(request.BetId.ToString());

                var betManagerGrain = GrainFactory.GetGrain<IBetManagerGrain>(request.UserId);
                await betManagerGrain.AddBetAsync(request.BetId);

                _logger.LogInformation("Bet {BetId} placed successfully for user {UserId}", request.BetId, request.UserId);

                return BetResult.Success(_aggregate.GetState().ToBet());
            }
            catch (Exception)
            {
                await userWalletGrain.ReleaseReservationAsync(request.BetId.ToString());
                await oddsGrain.UnlockOddsAsync(request.BetId.ToString());
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error placing bet {BetId}", request.BetId);
            return BetResult.Failed($"Failed to place bet: {ex.Message}");
        }
    }

    public ValueTask<Bet?> GetBetDetailsAsync()
    {
        var bet = _aggregate?.GetState().ToBet();
        return ValueTask.FromResult(bet);
    }

    public async ValueTask<BetResult> VoidBetAsync(string reason)
    {
        try
        {
            if (_aggregate == null)
                return BetResult.Failed("Bet not found");

            var currentBet = _aggregate.GetState().ToBet();
            if (!currentBet.CanBeVoided)
                return BetResult.Failed("Bet cannot be voided in current status");

            _aggregate.VoidBet(reason);
            await PersistEventsAsync();

            if (currentBet.Status == BetStatus.Accepted)
            {
                var userWalletGrain = GrainFactory.GetGrain<IUserWalletGrain>(currentBet.UserId);
                await userWalletGrain.ReleaseReservationAsync(currentBet.Id.ToString());
            }

            _logger.LogInformation("Bet {BetId} voided: {Reason}", currentBet.Id, reason);

            return BetResult.Success(_aggregate.GetState().ToBet());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error voiding bet {BetId}", this.GetPrimaryKey());
            return BetResult.Failed($"Failed to void bet: {ex.Message}");
        }
    }

    public async ValueTask<BetResult> CashOutAsync()
    {
        try
        {
            if (_aggregate == null)
                return BetResult.Failed("Bet not found");

            var currentBet = _aggregate.GetState().ToBet();
            if (!currentBet.CanBeCashedOut)
                return BetResult.Failed("Bet cannot be cashed out");

            var cashOutAmount = currentBet.Amount.Subtract(Money.Create(currentBet.Amount.Amount * 0.05m, currentBet.Amount.Currency));

            var userWalletGrain = GrainFactory.GetGrain<IUserWalletGrain>(currentBet.UserId);
            var depositResult = await userWalletGrain.DepositAsync(cashOutAmount, $"cashout-{currentBet.Id}");

            if (!depositResult.IsSuccess)
                return BetResult.Failed("Failed to process cash out");

            _aggregate.GetState().Status = BetStatus.CashOut;
            _aggregate.GetState().SettledAt = DateTimeOffset.UtcNow;
            _aggregate.GetState().PayoutAmount = cashOutAmount.Amount;

            await PersistEventsAsync();

            _logger.LogInformation("Bet {BetId} cashed out for {Amount}", currentBet.Id, cashOutAmount);

            return BetResult.Success(_aggregate.GetState().ToBet());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cashing out bet {BetId}", this.GetPrimaryKey());
            return BetResult.Failed($"Failed to cash out bet: {ex.Message}");
        }
    }

    public async ValueTask<IReadOnlyList<Bet>> GetBetHistoryAsync()
    {
        var betId = this.GetPrimaryKey();
        var events = await _eventStore.GetEventsAsync(betId.ToString());

        var bets = new List<Bet>();
        var aggregate = BetAggregate.Create(betId);

        foreach (var domainEvent in events)
        {
            aggregate.Apply(domainEvent);
            bets.Add(aggregate.GetState().ToBet());
        }

        return bets;
    }

    public async ValueTask<BetResult> SettleBetAsync(BetStatus finalStatus, Money? payout, string sagaId)
    {
        try
        {
            if (_aggregate == null)
                return BetResult.Failed("Bet not found");

            var currentBet = _aggregate.GetState().ToBet();
            if (currentBet.IsSettled)
                return BetResult.Failed("Bet has already been settled");

            if (currentBet.Status != BetStatus.Accepted)
                return BetResult.Failed("Bet must be in Accepted status to settle");

            _aggregate.SettleBet(finalStatus, payout);
            await PersistEventsAsync();

            _logger.LogInformation("Bet {BetId} settled with status {Status} by saga {SagaId}", 
                currentBet.Id, finalStatus, sagaId);

            return BetResult.Success(_aggregate.GetState().ToBet());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error settling bet {BetId}", this.GetPrimaryKey());
            return BetResult.Failed($"Failed to settle bet: {ex.Message}");
        }
    }

    public ValueTask<bool> CanBeSettledAsync()
    {
        var currentBet = _aggregate?.GetState().ToBet();
        var canSettle = currentBet != null && 
                       currentBet.Status == BetStatus.Accepted && 
                       !currentBet.IsSettled;
        return ValueTask.FromResult(canSettle);
    }

    private async Task PersistEventsAsync()
    {
        var state = _aggregate!.GetState();
        if (state.UncommittedEvents.Count > 0)
        {
            await _eventStore.SaveEventsAsync(state.BetId.ToString(), state.UncommittedEvents);
            state.MarkEventsAsCommitted();
        }
    }
}