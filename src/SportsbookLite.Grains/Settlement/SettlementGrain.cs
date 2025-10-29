using SportsbookLite.Contracts.Events;
using SportsbookLite.Contracts.Settlement;

namespace SportsbookLite.Grains.Settlement;

public sealed class SettlementGrain : Grain, ISettlementGrain
{
    private readonly ILogger<SettlementGrain> _logger;
    private SettlementState _state = new();

    public SettlementGrain(ILogger<SettlementGrain> logger)
    {
        _logger = logger;
    }

    public async ValueTask<SettlementResult> SettleMarketAsync(SettlementRequest request)
    {
        try
        {
            if (_state.Status == SettlementStatus.InProgress)
            {
                return SettlementResult.Failed("Settlement already in progress for this market");
            }

            if (_state.Status == SettlementStatus.Completed)
            {
                return SettlementResult.Failed("Market has already been settled");
            }

            _state.Status = SettlementStatus.InProgress;
            _state.EventId = request.EventId;
            _state.MarketId = request.MarketId;
            _state.WinningSelectionId = request.WinningSelectionId;
            _state.StartedAt = DateTimeOffset.UtcNow;
            
            var sagaId = Guid.NewGuid();
            _state.CurrentSagaId = sagaId.ToString();

            var sagaGrain = GrainFactory.GetGrain<ISettlementSagaGrain>(sagaId);
            var sagaResult = await sagaGrain.StartSettlementAsync(request);

            if (sagaResult.IsSuccess)
            {
                _state.Status = sagaResult.Status;
                _state.AffectedBetIds = sagaResult.AffectedBetIds.ToList();
                _state.TotalPayouts = sagaResult.TotalPayouts;
                _state.CompletedAt = DateTimeOffset.UtcNow;
                
                _logger.LogInformation("Settlement completed for market {MarketId}: {SuccessfulSettlements} successful, {FailedSettlements} failed",
                    request.MarketId, sagaResult.SuccessfulSettlements, sagaResult.FailedSettlements);
            }
            else
            {
                _state.Status = SettlementStatus.Failed;
                _state.LastError = sagaResult.ErrorMessage;
                _state.AttemptNumber++;

                _logger.LogError("Settlement failed for market {MarketId}: {Error}", request.MarketId, sagaResult.ErrorMessage);
            }

            return sagaResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to settle market {MarketId} for event {EventId}", request.MarketId, request.EventId);
            
            _state.Status = SettlementStatus.Failed;
            _state.LastError = ex.Message;
            
            return SettlementResult.Failed(ex.Message);
        }
    }

    public async ValueTask<SettlementResult> ReverseSettlementAsync(SettlementReversal reversal)
    {
        try
        {
            if (_state.Status != SettlementStatus.Completed)
            {
                return SettlementResult.Failed("Cannot reverse settlement that is not completed");
            }

            _logger.LogInformation("Starting settlement reversal for market {MarketId}, reason: {Reason}", 
                reversal.MarketId, reversal.Reason);

            var sagaId = Guid.NewGuid();
            var sagaGrain = GrainFactory.GetGrain<ISettlementSagaGrain>(sagaId);
            
            var compensationResult = await sagaGrain.CompensateAsync(reversal.Reason);

            if (compensationResult.IsSuccess)
            {
                _state.Status = SettlementStatus.Pending;
                _state.CurrentSagaId = null;
                _state.CompletedAt = null;
                _state.LastError = null;
            }
            else
            {
                _state.LastError = compensationResult.ErrorMessage;
            }

            return compensationResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reverse settlement for market {MarketId}", reversal.MarketId);
            return SettlementResult.Failed(ex.Message);
        }
    }

    public ValueTask<SettlementStatus> GetSettlementStatusAsync() => 
        ValueTask.FromResult(_state.Status);

    public ValueTask<IReadOnlyList<Guid>> GetAffectedBetsAsync() => 
        ValueTask.FromResult<IReadOnlyList<Guid>>(_state.AffectedBetIds.AsReadOnly());

    public ValueTask<bool> IsSettlementInProgressAsync() => 
        ValueTask.FromResult(_state.Status == SettlementStatus.InProgress);
}