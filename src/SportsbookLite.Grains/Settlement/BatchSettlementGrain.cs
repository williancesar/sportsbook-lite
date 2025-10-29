using SportsbookLite.Contracts.Settlement;

namespace SportsbookLite.Grains.Settlement;

public sealed class BatchSettlementGrain : Grain, IBatchSettlementGrain
{
    private readonly ILogger<BatchSettlementGrain> _logger;
    private BatchSettlementState _state = new();

    public BatchSettlementGrain(ILogger<BatchSettlementGrain> logger)
    {
        _logger = logger;
    }

    public async ValueTask<SettlementResult> ProcessBatchAsync(SettlementBatch batch)
    {
        if (_state.Status == SettlementStatus.InProgress)
        {
            return SettlementResult.Failed("Batch processing already in progress");
        }

        try
        {
            _state.Status = SettlementStatus.InProgress;
            _state.Batch = batch;
            _state.StartedAt = DateTimeOffset.UtcNow;
            _state.LastActivityAt = DateTimeOffset.UtcNow;
            _state.ProcessedCount = 0;
            _state.FailedCount = 0;
            
            _logger.LogInformation("Starting batch settlement processing for {Count} requests", batch.Count);

            var processedRequests = new List<string>();
            var failedRequests = new List<string>();
            var successfulSettlements = 0;
            var failedSettlements = 0;
            var totalPayouts = Money.Zero();
            var affectedBetIds = new List<Guid>();

            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount,
                CancellationToken = CancellationToken.None
            };

            await Parallel.ForEachAsync(batch.Requests, parallelOptions, async (request, ct) =>
            {
                if (_state.IsCancelled) return;

                try
                {
                    var settlementGrain = GrainFactory.GetGrain<ISettlementGrain>(request.MarketId);
                    var result = await settlementGrain.SettleMarketAsync(request);

                    lock (processedRequests)
                    {
                        if (result.IsSuccess)
                        {
                            processedRequests.Add($"{request.EventId}:{request.MarketId}");
                            successfulSettlements += result.SuccessfulSettlements;
                            affectedBetIds.AddRange(result.AffectedBetIds);
                            totalPayouts = Money.Create(totalPayouts.Amount + result.TotalPayouts.Amount, totalPayouts.Currency);
                        }
                        else
                        {
                            failedRequests.Add($"{request.EventId}:{request.MarketId}:{result.ErrorMessage}");
                            failedSettlements++;
                        }

                        _state.ProcessedCount++;
                        _state.LastActivityAt = DateTimeOffset.UtcNow;
                    }

                    if (_state.ProcessedCount % 10 == 0)
                    {
                        _logger.LogInformation("Processed {Count}/{Total} settlement requests", 
                            _state.ProcessedCount, batch.Count);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process settlement request for market {MarketId}", request.MarketId);
                    
                    lock (failedRequests)
                    {
                        failedRequests.Add($"{request.EventId}:{request.MarketId}:{ex.Message}");
                        _state.FailedCount++;
                        _state.ProcessedCount++;
                    }
                }
            });

            _state.ProcessedRequests = processedRequests;
            _state.FailedRequests = failedRequests;
            _state.CompletedAt = DateTimeOffset.UtcNow;

            if (_state.IsCancelled)
            {
                _state.Status = SettlementStatus.Failed;
                return SettlementResult.Failed("Batch processing was cancelled");
            }
            else if (failedSettlements == 0)
            {
                _state.Status = SettlementStatus.Completed;
                
                _logger.LogInformation("Batch settlement completed successfully: {Success} processed, {Failed} failed", 
                    successfulSettlements, failedSettlements);
                
                return SettlementResult.Success(affectedBetIds, totalPayouts, successfulSettlements);
            }
            else if (successfulSettlements > 0)
            {
                _state.Status = SettlementStatus.PartiallyCompleted;
                
                _logger.LogWarning("Batch settlement partially completed: {Success} processed, {Failed} failed", 
                    successfulSettlements, failedSettlements);
                
                return SettlementResult.Partial(
                    affectedBetIds, 
                    totalPayouts, 
                    successfulSettlements, 
                    failedSettlements,
                    failedRequests);
            }
            else
            {
                _state.Status = SettlementStatus.Failed;
                
                _logger.LogError("Batch settlement failed: all {Count} requests failed", batch.Count);
                
                return SettlementResult.Failed($"All {batch.Count} settlement requests failed");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Batch settlement processing failed with exception");
            
            _state.Status = SettlementStatus.Failed;
            _state.CompletedAt = DateTimeOffset.UtcNow;
            
            return SettlementResult.Failed(ex.Message);
        }
    }

    public ValueTask<SettlementStatus> GetBatchStatusAsync() => 
        ValueTask.FromResult(_state.Status);

    public ValueTask<int> GetProcessedCountAsync() => 
        ValueTask.FromResult(_state.ProcessedCount);

    public ValueTask<int> GetRemainingCountAsync()
    {
        var total = _state.Batch?.Count ?? 0;
        var remaining = Math.Max(0, total - _state.ProcessedCount);
        return ValueTask.FromResult(remaining);
    }

    public ValueTask<TimeSpan> GetEstimatedTimeRemainingAsync()
    {
        if (_state.Status != SettlementStatus.InProgress || _state.ProcessedCount == 0)
        {
            return ValueTask.FromResult(TimeSpan.Zero);
        }

        var elapsed = DateTimeOffset.UtcNow - _state.StartedAt;
        var averageTimePerRequest = elapsed.TotalMilliseconds / _state.ProcessedCount;
        var remaining = (_state.Batch?.Count ?? 0) - _state.ProcessedCount;
        var estimatedRemainingMs = averageTimePerRequest * remaining;

        return ValueTask.FromResult(TimeSpan.FromMilliseconds(estimatedRemainingMs));
    }

    public async ValueTask CancelBatchAsync()
    {
        if (_state.Status == SettlementStatus.InProgress)
        {
            _state.IsCancelled = true;
            _logger.LogInformation("Batch settlement cancellation requested");
        }
    }
}