using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Runtime;
using System.Diagnostics;

namespace SportsbookLite.Grains;

/// <summary>
/// Base grain class that provides lifecycle logging and common functionality
/// </summary>
public abstract class BaseGrain : Grain
{
    protected ILogger Logger { get; private set; } = null!;
    private readonly Stopwatch _lifetimeStopwatch = new();
    private DateTimeOffset _activatedAt;
    
    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        _activatedAt = DateTimeOffset.UtcNow;
        _lifetimeStopwatch.Start();
        
        Logger = ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger(GetType());
        
        var grainId = this.GetPrimaryKeyString() ?? this.GetPrimaryKey().ToString();
        var grainType = GetType().Name;
        
        using (Logger.BeginScope(new Dictionary<string, object>
        {
            ["GrainId"] = grainId,
            ["GrainType"] = grainType,
            ["SiloAddress"] = RuntimeIdentity,
            ["ActivationId"] = IdentityString,
            ["ActivatedAt"] = _activatedAt
        }))
        {
            Logger.LogInformation("Grain activated: {GrainType} with ID {GrainId}", 
                grainType, grainId);
        }
        
        return base.OnActivateAsync(cancellationToken);
    }
    
    public override Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
    {
        _lifetimeStopwatch.Stop();
        
        var grainId = this.GetPrimaryKeyString() ?? this.GetPrimaryKey().ToString();
        var grainType = GetType().Name;
        
        Logger.LogInformation(
            "Grain deactivating: {GrainType} with ID {GrainId} after {LifetimeMs}ms. Reason: {DeactivationReason}", 
            grainType, 
            grainId, 
            _lifetimeStopwatch.ElapsedMilliseconds,
            reason);
        
        return base.OnDeactivateAsync(reason, cancellationToken);
    }
    
    /// <summary>
    /// Logs a business event with structured data
    /// </summary>
    protected void LogBusinessEvent(string eventName, object? eventData = null, LogLevel level = LogLevel.Information)
    {
        var grainId = this.GetPrimaryKeyString() ?? this.GetPrimaryKey().ToString();
        
        using (Logger.BeginScope(new Dictionary<string, object>
        {
            ["EventName"] = eventName,
            ["GrainId"] = grainId,
            ["GrainType"] = GetType().Name,
            ["EventTimestamp"] = DateTimeOffset.UtcNow
        }))
        {
            if (eventData != null)
            {
                Logger.Log(level, "Business event: {EventName} with data {@EventData}", eventName, eventData);
            }
            else
            {
                Logger.Log(level, "Business event: {EventName}", eventName);
            }
        }
    }
    
    /// <summary>
    /// Executes an operation with performance logging
    /// </summary>
    protected async Task<T> ExecuteWithLoggingAsync<T>(string operationName, Func<Task<T>> operation)
    {
        var stopwatch = Stopwatch.StartNew();
        var grainId = this.GetPrimaryKeyString() ?? this.GetPrimaryKey().ToString();
        
        try
        {
            Logger.LogDebug("Starting operation {OperationName} on grain {GrainId}", operationName, grainId);
            
            var result = await operation();
            
            stopwatch.Stop();
            Logger.LogInformation(
                "Completed operation {OperationName} on grain {GrainId} in {ElapsedMs}ms", 
                operationName, grainId, stopwatch.ElapsedMilliseconds);
            
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Logger.LogError(ex, 
                "Failed operation {OperationName} on grain {GrainId} after {ElapsedMs}ms", 
                operationName, grainId, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }
    
    /// <summary>
    /// Gets correlation ID from Orleans request context
    /// </summary>
    protected string GetCorrelationId()
    {
        return RequestContext.Get("CorrelationId")?.ToString() ?? Guid.NewGuid().ToString();
    }
    
    /// <summary>
    /// Sets correlation ID in Orleans request context
    /// </summary>
    protected void SetCorrelationId(string correlationId)
    {
        RequestContext.Set("CorrelationId", correlationId);
    }
}