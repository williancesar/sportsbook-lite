using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Runtime;
using Prometheus;
using System.Diagnostics;
using SportsbookLite.Infrastructure.Metrics;

namespace SportsbookLite.Infrastructure.Logging;

/// <summary>
/// Enhanced Orleans grain call filter for structured logging with Loki integration
/// </summary>
public class EnhancedGrainInstrumentationFilter : IIncomingGrainCallFilter
{
    private readonly ILogger<EnhancedGrainInstrumentationFilter> _logger;
    
    // Add concurrent calls gauge
    private static readonly Gauge ConcurrentGrainCalls = Prometheus.Metrics
        .CreateGauge("orleans_concurrent_grain_calls", "Number of concurrent grain calls",
            new GaugeConfiguration
            {
                LabelNames = new[] { "grain_type", "method" }
            });
    
    // Add grain errors counter
    private static readonly Counter GrainErrors = Prometheus.Metrics
        .CreateCounter("orleans_grain_errors_total", "Total grain errors",
            new CounterConfiguration
            {
                LabelNames = new[] { "grain_type", "method", "error_type" }
            });

    public EnhancedGrainInstrumentationFilter(ILogger<EnhancedGrainInstrumentationFilter> logger)
    {
        _logger = logger;
    }

    public async Task Invoke(IIncomingGrainCallContext context)
    {
        var grainType = context.Grain.GetType().Name;
        var methodName = context.ImplementationMethod?.Name ?? "Unknown";
        
        // Try to get grain ID - grains may not always have accessible IDs
        string grainId;
        try
        {
            if (context.Grain is IAddressable addressable)
            {
                grainId = addressable.GetPrimaryKeyString() ?? addressable.GetPrimaryKey().ToString();
            }
            else
            {
                grainId = context.Grain.GetHashCode().ToString();
            }
        }
        catch
        {
            grainId = "unknown";
        }
        
        // Get or create correlation ID
        var correlationId = RequestContext.Get("CorrelationId")?.ToString() ?? Guid.NewGuid().ToString();
        RequestContext.Set("CorrelationId", correlationId);
        
        // Track concurrent grain calls
        ConcurrentGrainCalls.WithLabels(grainType, methodName).Inc();
        
        var stopwatch = Stopwatch.StartNew();
        
        using (_logger.BeginScope(new Dictionary<string, object?>
        {
            ["CorrelationId"] = correlationId,
            ["GrainMethod"] = methodName,
            ["GrainType"] = grainType,
            ["GrainId"] = grainId,
            ["CallerId"] = RequestContext.Get("CallerId")?.ToString(),
            ["RequestId"] = RequestContext.Get("RequestId")?.ToString(),
            ["SourceContext"] = $"Orleans.{grainType}"
        }))
        {
            try
            {
                _logger.LogDebug("Grain call started: {GrainType}.{Method} for grain {GrainId}", 
                    grainType, methodName, grainId);
                
                await context.Invoke();
                
                stopwatch.Stop();
                
                // Record successful call metrics
                OrleansMetrics.GrainMethodDuration
                    .WithLabels(grainType, methodName, "success")
                    .Observe(stopwatch.Elapsed.TotalSeconds);
                
                OrleansMetrics.GrainMethodCalls
                    .WithLabels(grainType, methodName, "success")
                    .Inc();
                    
                _logger.LogInformation("Grain call completed: {GrainType}.{Method} for grain {GrainId} in {Duration}ms", 
                    grainType, methodName, grainId, stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                
                // Record failed call metrics
                OrleansMetrics.GrainMethodDuration
                    .WithLabels(grainType, methodName, "failure")
                    .Observe(stopwatch.Elapsed.TotalSeconds);
                
                OrleansMetrics.GrainMethodCalls
                    .WithLabels(grainType, methodName, "failure")
                    .Inc();
                
                GrainErrors
                    .WithLabels(grainType, methodName, ex.GetType().Name)
                    .Inc();
                    
                _logger.LogError(ex, "Grain call failed: {GrainType}.{Method} for grain {GrainId} after {Duration}ms", 
                    grainType, methodName, grainId, stopwatch.ElapsedMilliseconds);
                
                throw;
            }
            finally
            {
                ConcurrentGrainCalls.WithLabels(grainType, methodName).Dec();
            }
        }
    }
}