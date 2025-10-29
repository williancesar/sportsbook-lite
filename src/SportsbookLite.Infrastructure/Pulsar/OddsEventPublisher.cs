using Microsoft.Extensions.Logging;
using SportsbookLite.Contracts.Events;

namespace SportsbookLite.Infrastructure.Pulsar;

public sealed class OddsEventPublisher
{
    private readonly IPulsarService _pulsarService;
    private readonly ILogger<OddsEventPublisher> _logger;

    public OddsEventPublisher(IPulsarService pulsarService, ILogger<OddsEventPublisher> logger)
    {
        _pulsarService = pulsarService;
        _logger = logger;
    }

    public async ValueTask PublishOddsUpdatedAsync(OddsUpdatedEvent oddsUpdatedEvent)
    {
        try
        {
            await _pulsarService.PublishAsync(oddsUpdatedEvent);
            _logger.LogDebug("Published OddsUpdatedEvent for market {MarketId}", oddsUpdatedEvent.MarketId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish OddsUpdatedEvent for market {MarketId}", oddsUpdatedEvent.MarketId);
            throw;
        }
    }

    public async ValueTask PublishOddsSuspendedAsync(OddsSuspendedEvent oddsSuspendedEvent)
    {
        try
        {
            await _pulsarService.PublishAsync(oddsSuspendedEvent);
            _logger.LogDebug("Published OddsSuspendedEvent for market {MarketId}", oddsSuspendedEvent.MarketId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish OddsSuspendedEvent for market {MarketId}", oddsSuspendedEvent.MarketId);
            throw;
        }
    }

    public async ValueTask PublishOddsResumedAsync(OddsResumedEvent oddsResumedEvent)
    {
        try
        {
            await _pulsarService.PublishAsync(oddsResumedEvent);
            _logger.LogDebug("Published OddsResumedEvent for market {MarketId}", oddsResumedEvent.MarketId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish OddsResumedEvent for market {MarketId}", oddsResumedEvent.MarketId);
            throw;
        }
    }

    public async ValueTask PublishVolatilityChangedAsync(OddsVolatilityChangedEvent volatilityChangedEvent)
    {
        try
        {
            await _pulsarService.PublishAsync(volatilityChangedEvent);
            _logger.LogDebug("Published OddsVolatilityChangedEvent for market {MarketId}, Level changed from {PreviousLevel} to {NewLevel}",
                volatilityChangedEvent.MarketId, volatilityChangedEvent.PreviousLevel, volatilityChangedEvent.NewLevel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish OddsVolatilityChangedEvent for market {MarketId}", volatilityChangedEvent.MarketId);
            throw;
        }
    }

    public async ValueTask PublishAllEventsAsync(
        OddsUpdatedEvent? oddsUpdated = null,
        OddsSuspendedEvent? oddsSuspended = null,
        OddsResumedEvent? oddsResumed = null,
        OddsVolatilityChangedEvent? volatilityChanged = null)
    {
        var tasks = new List<ValueTask>();

        if (oddsUpdated.HasValue)
            tasks.Add(PublishOddsUpdatedAsync(oddsUpdated.Value));

        if (oddsSuspended.HasValue)
            tasks.Add(PublishOddsSuspendedAsync(oddsSuspended.Value));

        if (oddsResumed.HasValue)
            tasks.Add(PublishOddsResumedAsync(oddsResumed.Value));

        if (volatilityChanged.HasValue)
            tasks.Add(PublishVolatilityChangedAsync(volatilityChanged.Value));

        if (tasks.Any())
        {
            try
            {
                await Task.WhenAll(tasks.Select(t => t.AsTask()));
                _logger.LogDebug("Successfully published {EventCount} odds events", tasks.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish one or more odds events");
                throw;
            }
        }
    }
}