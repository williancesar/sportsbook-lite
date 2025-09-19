using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Runtime;
using Prometheus;
using System.Diagnostics;

namespace SportsbookLite.Infrastructure.Metrics;

/// <summary>
/// Orleans-specific metrics for grain operations and cluster health
/// </summary>
public static class OrleansMetrics
{
    // Grain Metrics
    public static readonly Counter GrainActivations = Prometheus.Metrics
        .CreateCounter("orleans_grain_activations_total", "Total grain activations",
            new CounterConfiguration
            {
                LabelNames = new[] { "grain_type", "silo" }
            });

    public static readonly Counter GrainDeactivations = Prometheus.Metrics
        .CreateCounter("orleans_grain_deactivations_total", "Total grain deactivations",
            new CounterConfiguration
            {
                LabelNames = new[] { "grain_type", "silo", "reason" }
            });

    public static readonly Histogram GrainMethodDuration = Prometheus.Metrics
        .CreateHistogram("orleans_grain_method_duration_seconds", "Grain method execution time",
            new HistogramConfiguration
            {
                LabelNames = new[] { "grain_type", "method", "status" },
                Buckets = Histogram.ExponentialBuckets(0.001, 2, 15) // 1ms to 16s
            });

    public static readonly Counter GrainMethodCalls = Prometheus.Metrics
        .CreateCounter("orleans_grain_method_calls_total", "Total grain method calls",
            new CounterConfiguration
            {
                LabelNames = new[] { "grain_type", "method", "status" }
            });

    public static readonly Gauge ActiveGrains = Prometheus.Metrics
        .CreateGauge("orleans_active_grains", "Number of active grains",
            new GaugeConfiguration
            {
                LabelNames = new[] { "grain_type", "silo" }
            });

    // Silo Metrics
    public static readonly Gauge SiloStatus = Prometheus.Metrics
        .CreateGauge("orleans_silo_status", "Silo status (1=active, 0=inactive)",
            new GaugeConfiguration
            {
                LabelNames = new[] { "silo_address", "cluster_id" }
            });

    public static readonly Counter SiloMessages = Prometheus.Metrics
        .CreateCounter("orleans_silo_messages_total", "Total messages processed by silo",
            new CounterConfiguration
            {
                LabelNames = new[] { "direction", "message_type" }
            });

    public static readonly Histogram MessageLatency = Prometheus.Metrics
        .CreateHistogram("orleans_message_latency_seconds", "Message processing latency",
            new HistogramConfiguration
            {
                LabelNames = new[] { "message_type", "target_grain" },
                Buckets = Histogram.LinearBuckets(0.001, 0.005, 20) // 1ms to 100ms
            });

    // Cluster Metrics
    public static readonly Gauge ClusterMembership = Prometheus.Metrics
        .CreateGauge("orleans_cluster_membership", "Number of silos in cluster",
            new GaugeConfiguration
            {
                LabelNames = new[] { "status", "cluster_id" }
            });

    // Reminder Metrics
    public static readonly Counter ReminderTicks = Prometheus.Metrics
        .CreateCounter("orleans_reminder_ticks_total", "Total reminder ticks executed",
            new CounterConfiguration
            {
                LabelNames = new[] { "grain_type", "reminder_name", "status" }
            });

    // Stream Metrics
    public static readonly Counter StreamMessages = Prometheus.Metrics
        .CreateCounter("orleans_stream_messages_total", "Total stream messages",
            new CounterConfiguration
            {
                LabelNames = new[] { "stream_provider", "namespace", "direction" }
            });

    public static readonly Gauge StreamSubscriptions = Prometheus.Metrics
        .CreateGauge("orleans_stream_subscriptions", "Active stream subscriptions",
            new GaugeConfiguration
            {
                LabelNames = new[] { "stream_provider", "namespace" }
            });

    // Storage Metrics
    public static readonly Histogram StorageOperationDuration = Prometheus.Metrics
        .CreateHistogram("orleans_storage_operation_duration_seconds", "Storage operation duration",
            new HistogramConfiguration
            {
                LabelNames = new[] { "operation", "storage_provider", "grain_type" },
                Buckets = Histogram.ExponentialBuckets(0.001, 2, 12) // 1ms to 2s
            });

    public static readonly Counter StorageOperations = Prometheus.Metrics
        .CreateCounter("orleans_storage_operations_total", "Total storage operations",
            new CounterConfiguration
            {
                LabelNames = new[] { "operation", "storage_provider", "status" }
            });
}

/// <summary>
/// Grain instrumentation filter for automatic metrics collection
/// </summary>
public class GrainInstrumentationFilter : IIncomingGrainCallFilter
{
    private readonly ILogger<GrainInstrumentationFilter> _logger;

    public GrainInstrumentationFilter(ILogger<GrainInstrumentationFilter> logger)
    {
        _logger = logger;
    }

    public async Task Invoke(IIncomingGrainCallContext context)
    {
        var grainType = context.Grain.GetType().Name;
        var methodName = context.ImplementationMethod?.Name ?? "Unknown";
        var stopwatch = Stopwatch.StartNew();
        var status = "success";

        try
        {
            await context.Invoke();
        }
        catch (Exception ex)
        {
            status = "error";
            _logger.LogError(ex, "Error in grain method {GrainType}.{Method}", grainType, methodName);
            throw;
        }
        finally
        {
            stopwatch.Stop();
            
            // Record metrics
            OrleansMetrics.GrainMethodCalls
                .WithLabels(grainType, methodName, status)
                .Inc();
            
            OrleansMetrics.GrainMethodDuration
                .WithLabels(grainType, methodName, status)
                .Observe(stopwatch.Elapsed.TotalSeconds);
        }
    }
}

/// <summary>
/// Base class for instrumented grains with lifecycle metrics
/// </summary>
public abstract class BaseInstrumentedGrain : Grain
{
    private readonly string _grainType;
    private readonly string _siloAddress;

    protected BaseInstrumentedGrain()
    {
        _grainType = GetType().Name;
        _siloAddress = Environment.MachineName; // Or use RuntimeIdentity
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        OrleansMetrics.GrainActivations
            .WithLabels(_grainType, _siloAddress)
            .Inc();
        
        OrleansMetrics.ActiveGrains
            .WithLabels(_grainType, _siloAddress)
            .Inc();

        return base.OnActivateAsync(cancellationToken);
    }

    public override Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
    {
        OrleansMetrics.GrainDeactivations
            .WithLabels(_grainType, _siloAddress, reason.Description.ToString())
            .Inc();
        
        OrleansMetrics.ActiveGrains
            .WithLabels(_grainType, _siloAddress)
            .Dec();

        return base.OnDeactivateAsync(reason, cancellationToken);
    }

    /// <summary>
    /// Helper method to track storage operations
    /// </summary>
    protected async Task<T> TrackStorageOperation<T>(string operation, Func<Task<T>> storageOperation)
    {
        var stopwatch = Stopwatch.StartNew();
        var status = "success";

        try
        {
            return await storageOperation();
        }
        catch
        {
            status = "error";
            throw;
        }
        finally
        {
            stopwatch.Stop();
            
            OrleansMetrics.StorageOperations
                .WithLabels(operation, "default", status)
                .Inc();
            
            OrleansMetrics.StorageOperationDuration
                .WithLabels(operation, "default", _grainType)
                .Observe(stopwatch.Elapsed.TotalSeconds);
        }
    }

    /// <summary>
    /// Helper method to track reminder execution
    /// </summary>
    protected async Task TrackReminderExecution(string reminderName, Func<Task> reminderAction)
    {
        var status = "success";

        try
        {
            await reminderAction();
        }
        catch
        {
            status = "error";
            throw;
        }
        finally
        {
            OrleansMetrics.ReminderTicks
                .WithLabels(_grainType, reminderName, status)
                .Inc();
        }
    }
}