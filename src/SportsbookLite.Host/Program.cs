using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Configuration;
using Orleans.Hosting;
using Orleans.Runtime;
using Prometheus;
using Prometheus.DotNetRuntime;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.Grafana.Loki;
using SportsbookLite.Infrastructure.Metrics;
using System.Net;

// Configure Serilog from configuration
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development"}.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(configuration)
    .Enrich.FromLogContext()
    .Enrich.WithEnvironmentName()
    .Enrich.WithMachineName()
    .Enrich.WithProcessId()
    .Enrich.WithProcessName()
    .Enrich.WithThreadId()
    .CreateLogger();

try
{
    Log.Information("Starting Orleans Silo Host...");

    var builder = Host.CreateDefaultBuilder(args)
        .UseOrleans((context, siloBuilder) =>
        {
            var environment = context.HostingEnvironment.EnvironmentName;
            var isDocker = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true" ||
                           !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ORLEANS_CLUSTER_ID"));

            // Get Redis connection string based on execution context
            var redisHost = isDocker ? "redis" : "localhost";
            var redisPassword = context.Configuration["REDIS_PASSWORD"] ??
                               Environment.GetEnvironmentVariable("REDIS_PASSWORD") ??
                               "dev123";
            var redisConnection = $"{redisHost}:6379,password={redisPassword},abortConnect=false";

            // Check if Redis clustering should be used (default to true)
            var useRedisClustering = context.Configuration["USE_REDIS_CLUSTERING"] != "false" &&
                                    Environment.GetEnvironmentVariable("USE_REDIS_CLUSTERING") != "false";

            if (useRedisClustering)
            {
                try
                {
                    // Use Redis clustering for all environments
                    Log.Information("Configuring Orleans with Redis clustering at {RedisHost}", redisHost);
                    siloBuilder.UseRedisClustering(redisConnection)
                        .Configure<ClusterOptions>(options =>
                        {
                            options.ClusterId = context.Configuration["Orleans:ClusterId"] ??
                                              Environment.GetEnvironmentVariable("ORLEANS_CLUSTER_ID") ??
                                              "sportsbook-dev";
                            options.ServiceId = context.Configuration["Orleans:ServiceId"] ??
                                              Environment.GetEnvironmentVariable("ORLEANS_SERVICE_ID") ??
                                              "sportsbook-silo";
                        });
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to configure Redis clustering, falling back to localhost clustering");
                    // Fallback to localhost clustering if Redis is not available
                    siloBuilder.UseLocalhostClustering()
                        .Configure<ClusterOptions>(options =>
                        {
                            options.ClusterId = "sportsbook-dev";
                            options.ServiceId = "sportsbook-silo";
                        });
                }
            }
            else
            {
                // Use localhost clustering if explicitly disabled
                Log.Information("Using localhost clustering (Redis clustering disabled)");
                siloBuilder.UseLocalhostClustering()
                    .Configure<ClusterOptions>(options =>
                    {
                        options.ClusterId = "sportsbook-dev";
                        options.ServiceId = "sportsbook-silo";
                    });
            }

            // Configure endpoints - listen on all addresses when in Docker
            siloBuilder.ConfigureEndpoints(
                siloPort: 11111,
                gatewayPort: 30000,
                listenOnAnyHostAddress: isDocker);

            // Configure storage - use PostgreSQL for all environments since it's available
            var connectionString = context.Configuration["ConnectionStrings:Database"] ??
                "Host=localhost;Database=sportsbook;Username=dev;Password=dev123";

            siloBuilder.AddAdoNetGrainStorage("Default", options =>
            {
                options.ConnectionString = connectionString;
                options.Invariant = "Npgsql";
            });

            // Application parts are automatically discovered in Orleans 9

            // Configure grain call filters for metrics and logging
            siloBuilder.AddIncomingGrainCallFilter<SportsbookLite.Infrastructure.Logging.EnhancedGrainInstrumentationFilter>();

            // Configure logging
            siloBuilder.ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddSerilog();
            });

            // Orleans Dashboard not available for Orleans 9.x yet

            // Configure telemetry - will be added in future Orleans versions
        })
        .ConfigureServices((context, services) =>
        {
            // Add Prometheus metrics
            services.AddSingleton<IHostedService>(provider =>
            {
                // Start Prometheus metrics server on port 9090
                var server = new MetricServer(port: 9090);
                return new PrometheusMetricsService(server);
            });

            // Register grain call filter
            services.AddSingleton<SportsbookLite.Infrastructure.Logging.EnhancedGrainInstrumentationFilter>();

            // Add health checks
            services.AddHealthChecks()
                .AddCheck("orleans", () =>
                {
                    // Check Orleans silo health
                    return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy();
                });

            // Configure .NET runtime metrics
            services.AddSingleton<IHostedService, DotNetRuntimeMetricsService>();
        })
        .UseSerilog()
        .UseConsoleLifetime();

    var host = builder.Build();

    // Initialize metrics
    InitializeMetrics();

    await host.RunAsync();
    
    Log.Information("Orleans Silo Host stopped successfully");
    return 0;
}
catch (Exception ex)
{
    Log.Fatal(ex, "Orleans Silo Host terminated unexpectedly");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}

void InitializeMetrics()
{
    // Initialize business metrics
    BusinessMetrics.UpdateSystemHealth("orleans-silo", 100);
    
    // Initialize Orleans metrics
    OrleansMetrics.SiloStatus.WithLabels(Environment.MachineName, "sportsbook-dev").Set(1);
    OrleansMetrics.ClusterMembership.WithLabels("active", "sportsbook-dev").Set(1);
    
    Log.Information("Metrics initialized successfully");
}

/// <summary>
/// Hosted service for Prometheus metrics server
/// </summary>
public class PrometheusMetricsService : IHostedService
{
    private readonly MetricServer _server;

    public PrometheusMetricsService(MetricServer server)
    {
        _server = server;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _server.Start();
        Log.Information("Prometheus metrics server started on port 9090");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _server.Stop();
        return Task.CompletedTask;
    }
}

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

// Note: Orleans telemetry consumer will be implemented when Orleans adds
// native Prometheus support in future versions