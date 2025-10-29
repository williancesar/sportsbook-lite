using SportsbookLite.Contracts.Betting;
using SportsbookLite.Contracts.Events;
using SportsbookLite.Contracts.Settlement;

namespace SportsbookLite.Grains.Settlement;

public sealed class SettlementSagaGrain : Grain, ISettlementSagaGrain
{
    private readonly ILogger<SettlementSagaGrain> _logger;
    private SettlementSagaState _state = new();

    public SettlementSagaGrain(ILogger<SettlementSagaGrain> logger)
    {
        _logger = logger;
    }

    public async ValueTask<SettlementResult> StartSettlementAsync(SettlementRequest request)
    {
        if (_state.Status != SettlementStatus.Pending)
        {
            return SettlementResult.Failed("Saga has already been started");
        }

        try
        {
            _state.Request = request;
            _state.Status = SettlementStatus.InProgress;
            _state.StartedAt = DateTimeOffset.UtcNow;
            _state.CurrentStep = SagaStep.Started;

            _logger.LogInformation("Starting settlement saga for market {MarketId}", request.MarketId);

            return await ExecuteSagaStepsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start settlement saga for market {MarketId}", request.MarketId);
            await MarkAsFailed(ex.Message);
            return SettlementResult.Failed(ex.Message);
        }
    }

    private async ValueTask<SettlementResult> ExecuteSagaStepsAsync()
    {
        try
        {
            await ValidateRequestAsync();
            await CollectBetsAsync();
            await CalculatePayoutsAsync();
            await ProcessPayoutsAsync();
            await UpdateBetsAsync();
            await MarkAsCompleted();

            return SettlementResult.Success(
                _state.UpdatedBets.AsReadOnly(),
                _state.TotalPayouts,
                _state.UpdatedBets.Count
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Saga step failed, initiating compensation");
            _state.ErrorMessage = ex.Message;
            _state.IsCompensationRequired = true;
            
            var compensationResult = await CompensateAsync(ex.Message);
            return compensationResult.IsSuccess ? 
                SettlementResult.Failed(ex.Message) : 
                SettlementResult.Failed($"Primary failure: {ex.Message}, Compensation failure: {compensationResult.ErrorMessage}");
        }
    }

    private async Task ValidateRequestAsync()
    {
        _state.CurrentStep = SagaStep.ValidatingRequest;
        _state.ExecutedSteps.Add("ValidateRequest");
        
        if (_state.Request == null)
            throw new InvalidOperationException("Settlement request is null");

        var eventGrain = GrainFactory.GetGrain<ISportEventGrain>(_state.Request.EventId);
        var eventDetails = await eventGrain.GetEventDetailsAsync();
        
        if (eventDetails == null)
            throw new InvalidOperationException($"Event {_state.Request.EventId} not found");

        if (eventDetails.Status != EventStatus.Completed)
            throw new InvalidOperationException($"Event {_state.Request.EventId} is not completed");
    }

    private async Task CollectBetsAsync()
    {
        _state.CurrentStep = SagaStep.CollectingBets;
        _state.ExecutedSteps.Add("CollectBets");

        _logger.LogInformation("Collecting bets for market {MarketId}", _state.Request.MarketId);
        
        _state.CollectedBetIds = new List<Guid>();

        if (!_state.CollectedBetIds.Any())
        {
            _logger.LogInformation("No bets found to settle for market {MarketId}", _state.Request.MarketId);
        }
    }

    private async Task CalculatePayoutsAsync()
    {
        _state.CurrentStep = SagaStep.CalculatingPayouts;
        _state.ExecutedSteps.Add("CalculatePayouts");

        var payoutCalculations = new Dictionary<Guid, PayoutCalculation>();
        var totalPayouts = Money.Zero();

        foreach (var betId in _state.CollectedBetIds)
        {
            var betGrain = GrainFactory.GetGrain<IBetGrain>(betId);
            var bet = await betGrain.GetBetDetailsAsync();

            if (bet != null)
            {
                PayoutCalculation calculation;
                
                if (bet.SelectionId == _state.Request!.WinningSelectionId)
                {
                    calculation = PayoutCalculation.CreateWinning(betId, bet.Amount, bet.Odds);
                    totalPayouts = Money.Create(totalPayouts.Amount + calculation.PayoutAmount.Amount, totalPayouts.Currency);
                }
                else
                {
                    calculation = PayoutCalculation.CreateLosing(betId, bet.Amount, bet.Odds);
                }

                payoutCalculations[betId] = calculation;
            }
        }

        _state.PayoutCalculations = payoutCalculations;
        _state.TotalPayouts = totalPayouts;
    }

    private async Task ProcessPayoutsAsync()
    {
        _state.CurrentStep = SagaStep.ProcessingPayouts;
        _state.ExecutedSteps.Add("ProcessPayouts");
        _state.CompensationSteps.Add("ReversePayouts");

        var processedPayouts = new List<Guid>();

        foreach (var (betId, calculation) in _state.PayoutCalculations)
        {
            if (calculation.IsWinning && calculation.PayoutAmount.Amount > 0)
            {
                var betGrain = GrainFactory.GetGrain<IBetGrain>(betId);
                var bet = await betGrain.GetBetDetailsAsync();

                if (bet != null)
                {
                    var walletGrain = GrainFactory.GetGrain<IUserWalletGrain>(bet.UserId);
                    var payoutResult = await walletGrain.ProcessPayoutAsync(
                        calculation.PayoutAmount, 
                        betId.ToString(), 
                        this.GetPrimaryKey().ToString()
                    );

                    if (payoutResult.IsSuccess)
                    {
                        processedPayouts.Add(betId);
                        _logger.LogDebug("Processed payout for bet {BetId}: {Amount}", betId, calculation.PayoutAmount.Amount);
                    }
                    else
                    {
                        _logger.LogError("Failed to process payout for bet {BetId}: {Error}", betId, payoutResult.ErrorMessage);
                        throw new InvalidOperationException($"Failed to process payout for bet {betId}: {payoutResult.ErrorMessage}");
                    }
                }
            }
            else
            {
                processedPayouts.Add(betId);
            }
        }

        _state.ProcessedPayouts = processedPayouts;
    }

    private async Task UpdateBetsAsync()
    {
        _state.CurrentStep = SagaStep.UpdatingBets;
        _state.ExecutedSteps.Add("UpdateBets");
        _state.CompensationSteps.Add("RevertBetStatuses");

        var updatedBets = new List<Guid>();

        foreach (var (betId, calculation) in _state.PayoutCalculations)
        {
            var betGrain = GrainFactory.GetGrain<IBetGrain>(betId);
            var finalStatus = calculation.IsWinning ? BetStatus.Won : BetStatus.Lost;
            var payout = calculation.IsWinning ? (Money?)calculation.PayoutAmount : null;

            var updateResult = await betGrain.SettleBetAsync(finalStatus, payout, this.GetPrimaryKey().ToString());

            if (updateResult.IsSuccess)
            {
                updatedBets.Add(betId);
                _logger.LogDebug("Updated bet {BetId} to status {Status}", betId, finalStatus);
            }
            else
            {
                _logger.LogError("Failed to update bet {BetId}: {Error}", betId, updateResult.Error);
                throw new InvalidOperationException($"Failed to update bet {betId}: {updateResult.Error}");
            }
        }

        _state.UpdatedBets = updatedBets;
    }

    private async Task MarkAsCompleted()
    {
        _state.CurrentStep = SagaStep.Completed;
        _state.Status = SettlementStatus.Completed;
        _state.CompletedAt = DateTimeOffset.UtcNow;
        
        _logger.LogInformation("Settlement saga completed for market {MarketId}: {BetsSettled} bets settled, {TotalPayout} total payouts",
            _state.Request?.MarketId, _state.UpdatedBets.Count, _state.TotalPayouts.Amount);
    }

    private async Task MarkAsFailed(string errorMessage)
    {
        _state.CurrentStep = SagaStep.Failed;
        _state.Status = SettlementStatus.Failed;
        _state.ErrorMessage = errorMessage;
        _state.CompletedAt = DateTimeOffset.UtcNow;
    }

    public async ValueTask<SettlementResult> CompensateAsync(string reason)
    {
        try
        {
            _state.CurrentStep = SagaStep.Compensating;
            
            _logger.LogWarning("Starting compensation for saga {SagaId}, reason: {Reason}", this.GetPrimaryKey(), reason);
            
            if (_state.CompensationSteps.Contains("RevertBetStatuses"))
            {
                await RevertBetStatusesAsync();
            }

            if (_state.CompensationSteps.Contains("ReversePayouts"))
            {
                await ReversePayoutsAsync(reason);
            }

            _state.Status = SettlementStatus.Pending;
            _state.ErrorMessage = null;
            _state.IsCompensationRequired = false;

            _logger.LogInformation("Compensation completed for saga {SagaId}", this.GetPrimaryKey());

            return SettlementResult.Success(
                _state.UpdatedBets.AsReadOnly(),
                Money.Zero(),
                0
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Compensation failed: {Error}", ex.Message);
            return SettlementResult.Failed($"Compensation failed: {ex.Message}");
        }
    }

    private async Task RevertBetStatusesAsync()
    {
        foreach (var betId in _state.UpdatedBets)
        {
            var betGrain = GrainFactory.GetGrain<IBetGrain>(betId);
            await betGrain.SettleBetAsync(BetStatus.Accepted, null, this.GetPrimaryKey().ToString());
        }
        
        _state.UpdatedBets.Clear();
    }

    private async Task ReversePayoutsAsync(string reason)
    {
        foreach (var betId in _state.ProcessedPayouts)
        {
            if (_state.PayoutCalculations.TryGetValue(betId, out var calculation) && calculation.IsWinning)
            {
                var betGrain = GrainFactory.GetGrain<IBetGrain>(betId);
                var bet = await betGrain.GetBetDetailsAsync();

                if (bet != null)
                {
                    var walletGrain = GrainFactory.GetGrain<IUserWalletGrain>(bet.UserId);
                    await walletGrain.ReversePayoutAsync(
                        calculation.PayoutAmount,
                        betId.ToString(),
                        this.GetPrimaryKey().ToString(),
                        reason
                    );
                }
            }
        }
        
        _state.ProcessedPayouts.Clear();
        _state.TotalPayouts = Money.Zero();
    }

    public ValueTask<SettlementStatus> GetSagaStatusAsync() => 
        ValueTask.FromResult(_state.Status);

    public ValueTask<bool> IsCompletedAsync() => 
        ValueTask.FromResult(_state.Status == SettlementStatus.Completed);

    public ValueTask<bool> IsFailedAsync() => 
        ValueTask.FromResult(_state.Status == SettlementStatus.Failed);

    public ValueTask<IReadOnlyList<string>> GetExecutedStepsAsync() => 
        ValueTask.FromResult<IReadOnlyList<string>>(_state.ExecutedSteps.AsReadOnly());

    public async ValueTask CancelAsync()
    {
        if (_state.Status == SettlementStatus.InProgress && _state.IsCompensationRequired)
        {
            await CompensateAsync("Saga cancelled");
        }
        
        _state.Status = SettlementStatus.Failed;
        _state.ErrorMessage = "Cancelled";
    }
}