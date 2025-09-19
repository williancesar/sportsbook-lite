using FastEndpoints;
using FastEndpoints.Swagger;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Orleans;
using Orleans.Configuration;
using Orleans.Hosting;
using Prometheus;
using Prometheus.DotNetRuntime;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.Grafana.Loki;
using SportsbookLite.Api.Middleware;
using SportsbookLite.Infrastructure.Metrics;
using System.Net;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog with enrichers and Loki sink
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithEnvironmentName()
    .Enrich.WithMachineName()
    .Enrich.WithProcessId()
    .Enrich.WithProcessName()
    .Enrich.WithThreadId()
    .Enrich.WithProperty("Application", "SportsbookLite.Api")
    .CreateLogger();

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services));

// Add services
builder.Services
    .AddFastEndpoints()
    .SwaggerDocument(o =>
    {
        o.DocumentSettings = s =>
        {
            s.Title = "Sportsbook Lite API";
            s.Version = "v1";
            s.Description = "A lightweight sportsbook API built with Orleans and FastEndpoints";
        };
        o.MaxEndpointVersion = 1;
        o.ShortSchemaNames = true;
    });

// Configure Orleans Client
builder.Host.UseOrleansClient((context, client) =>
{
    var isDevelopment = context.HostingEnvironment.IsDevelopment();
    var isDocker = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true" ||
                   Environment.GetEnvironmentVariable("DOCKER_CONTAINER") == "true";

    if (isDevelopment)
    {
        // Determine Orleans silo endpoint based on environment
        var orleansHost = isDocker ? "orleans-silo" : "localhost";
        var orleansEndpoint = new IPEndPoint(
            IPAddress.TryParse(orleansHost, out var ip) ? ip : Dns.GetHostAddresses(orleansHost)[0],
            30000);

        // For development, connect to silo using static clustering
        client.UseStaticClustering(orleansEndpoint)
              .Configure<ClusterOptions>(options =>
              {
                  options.ClusterId = context.Configuration["Orleans:ClusterId"] ?? "sportsbook-dev";
                  options.ServiceId = context.Configuration["Orleans:ServiceId"] ?? "sportsbook-api";
              });
    }
    else
    {
        // Production: Use Redis clustering
        var redisConnection = context.Configuration["ConnectionStrings:Redis"] ?? "localhost:6379";
        client.UseRedisClustering(redisConnection)
        .Configure<ClusterOptions>(options =>
        {
            options.ClusterId = context.Configuration["Orleans:ClusterId"] ?? "sportsbook-prod";
            options.ServiceId = context.Configuration["Orleans:ServiceId"] ?? "sportsbook-api";
        });
    }
});

builder.Services.AddSingleton<IGrainFactory>(serviceProvider =>
    serviceProvider.GetRequiredService<IClusterClient>());

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

// Add health checks
builder.Services.AddHealthChecks()
    .AddCheck("orleans", () =>
    {
        try
        {
            // In production, you would check actual Orleans connectivity
            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy();
        }
        catch
        {
            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy();
        }
    });

// Configure .NET runtime metrics
builder.Services.AddSingleton<IHostedService, DotNetRuntimeMetricsService>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

// Add correlation ID middleware for distributed tracing
app.UseCorrelationId();

// Add Prometheus metrics middleware
app.UseHttpMetrics(options =>
{
    // Customize HTTP metrics collection
    options.RequestDuration.Enabled = true;
    options.RequestCount.Enabled = true;
    options.InProgress.Enabled = true;
    // Group 2xx, 3xx, 4xx, 5xx status codes
});

// Custom metrics middleware for business metrics
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? "/";
    var method = context.Request.Method;
    
    // Track API requests by endpoint
    using (ApiMetrics.RequestDuration.WithLabels(method, path).NewTimer())
    {
        await next();
        
        // Track response status
        var statusCode = context.Response.StatusCode;
        var statusCategory = statusCode switch
        {
            >= 200 and < 300 => "success",
            >= 400 and < 500 => "client_error",
            >= 500 => "server_error",
            _ => "other"
        };
        
        ApiMetrics.RequestCount
            .WithLabels(method, path, statusCategory)
            .Inc();
    }
});

app.UseHttpsRedirection()
   .UseCors()
   .UseFastEndpoints(c =>
   {
       c.Errors.UseProblemDetails();
       c.Endpoints.RoutePrefix = "api";
       c.Versioning.Prefix = "v";
       c.Throttle.HeaderName = "X-Rate-Limit";
   })
   .UseSwaggerGen();

// Health checks endpoint
app.UseHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var response = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(x => new
            {
                name = x.Key,
                status = x.Value.Status.ToString(),
                description = x.Value.Description,
                duration = x.Value.Duration.TotalMilliseconds
            }),
            totalDuration = report.TotalDuration.TotalMilliseconds
        };
        await context.Response.WriteAsJsonAsync(response);
    }
});

// Prometheus metrics endpoint
app.MapMetrics("/metrics");

// Initialize metrics
InitializeMetrics();

try
{
    Log.Information("Starting Sportsbook API on port {Port}...", 
        app.Environment.IsDevelopment() ? "5000" : "8080");
    await app.RunAsync();
    return 0;
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}

void InitializeMetrics()
{
    // Initialize API metrics
    ApiMetrics.ApiHealth.Set(1);
    BusinessMetrics.UpdateSystemHealth("api", 100);
    
    Log.Information("API metrics initialized successfully");
}

// Make Program class accessible for integration tests
public partial class Program { }

/// <summary>
/// Hosted service for .NET runtime metrics
/// </summary>
public class DotNetRuntimeMetricsService : IHostedService
{
    private IDisposable? _collector;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _collector = DotNetRuntimeStatsBuilder
            .Default()
            .WithContentionStats()
            .WithGcStats()
            .WithThreadPoolStats()
            .WithExceptionStats()
            .WithJitStats()
            .StartCollecting();
            
        Log.Information(".NET runtime metrics collection started");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _collector?.Dispose();
        return Task.CompletedTask;
    }
}

/// <summary>
/// API-specific metrics
/// </summary>
public static class ApiMetrics
{
    public static readonly Counter RequestCount = Prometheus.Metrics
        .CreateCounter("api_requests_total", "Total API requests",
            new CounterConfiguration
            {
                LabelNames = new[] { "method", "endpoint", "status" }
            });

    public static readonly Histogram RequestDuration = Prometheus.Metrics
        .CreateHistogram("api_request_duration_seconds", "API request duration",
            new HistogramConfiguration
            {
                LabelNames = new[] { "method", "endpoint" },
                Buckets = Histogram.ExponentialBuckets(0.001, 2, 15) // 1ms to 16s
            });

    public static readonly Gauge ApiHealth = Prometheus.Metrics
        .CreateGauge("api_health_status", "API health status (1=healthy, 0=unhealthy)");

    public static readonly Counter AuthenticationFailures = Prometheus.Metrics
        .CreateCounter("api_auth_failures_total", "Total authentication failures",
            new CounterConfiguration
            {
                LabelNames = new[] { "reason" }
            });

    public static readonly Counter ValidationErrors = Prometheus.Metrics
        .CreateCounter("api_validation_errors_total", "Total validation errors",
            new CounterConfiguration
            {
                LabelNames = new[] { "endpoint", "field" }
            });
}