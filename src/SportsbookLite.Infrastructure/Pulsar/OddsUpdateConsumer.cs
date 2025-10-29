using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orleans;
using SportsbookLite.Contracts.Events;
using SportsbookLite.Contracts.Odds;
using SportsbookLite.GrainInterfaces;

namespace SportsbookLite.Infrastructure.Pulsar;

[EventHandler("odds-consumer")]
public sealed class OddsUpdateConsumer : BackgroundService, IEventHandler<OddsUpdatedEvent>
{
    private readonly IPulsarService _pulsarService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OddsUpdateConsumer> _logger;
    private IAsyncDisposable? _subscription;

    public OddsUpdateConsumer(
        IPulsarService pulsarService,
        IServiceProvider serviceProvider,
        ILogger<OddsUpdateConsumer> logger)
    {
        _pulsarService = pulsarService;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _pulsarService.StartAsync(stoppingToken);
        
        _subscription = await _pulsarService.SubscribeAsync<OddsUpdatedEvent>("odds-consumer", this);
        
        _logger.LogInformation("OddsUpdateConsumer started and listening for odds updates");
        
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("OddsUpdateConsumer stopping");
        }
        finally
        {
            if (_subscription != null)
            {
                await _subscription.DisposeAsync();
            }
        }
    }

    public async ValueTask HandleAsync(OddsUpdatedEvent eventData, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Processing odds updated event for market {MarketId}", eventData.MarketId);
            
            using var scope = _serviceProvider.CreateScope();
            var grainFactory = scope.ServiceProvider.GetRequiredService<IGrainFactory>();
            
            var oddsGrain = grainFactory.GetGrain<IOddsGrain>(eventData.MarketId);
            var currentSnapshot = await oddsGrain.GetCurrentOddsAsync();
            
            _logger.LogInformation("Processed odds update for market {MarketId} - Current volatility: {Volatility}, Suspended: {IsSuspended}",
                eventData.MarketId, currentSnapshot.Volatility, currentSnapshot.IsSuspended);
                
            if (currentSnapshot.Volatility >= OddsVolatility.High)
            {
                _logger.LogWarning("High volatility detected for market {MarketId}: {Volatility}", 
                    eventData.MarketId, currentSnapshot.Volatility);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing odds updated event for market {MarketId}", eventData.MarketId);
            throw;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping OddsUpdateConsumer");
        
        if (_subscription != null)
        {
            await _subscription.DisposeAsync();
        }
        
        await _pulsarService.StopAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }
}