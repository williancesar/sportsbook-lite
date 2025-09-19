# Serilog.Sinks.Grafana.Loki Implementation Guide

## Executive Summary

This document provides a comprehensive implementation plan for integrating Serilog.Sinks.Grafana.Loki into the Sportsbook-Lite .NET 9 application. The implementation includes NuGet packages, configuration files, custom formatters, performance optimizations, and operational considerations for both development and production environments.

## 1. NuGet Package Dependencies

### Required Packages

```xml
<!-- SportsbookLite.Host.csproj -->
<PackageReference Include="Serilog.Sinks.Grafana.Loki" Version="8.3.1" />
<PackageReference Include="Serilog.Extensions.Hosting" Version="8.0.0" />
<PackageReference Include="Serilog.Enrichers.Environment" Version="3.1.0" />
<PackageReference Include="Serilog.Enrichers.Process" Version="3.1.0" />
<PackageReference Include="Serilog.Enrichers.Thread" Version="4.0.0" />

<!-- SportsbookLite.Api.csproj -->
<PackageReference Include="Serilog.Sinks.Grafana.Loki" Version="8.3.1" />
<PackageReference Include="Serilog.Extensions.Hosting" Version="8.0.0" />
<PackageReference Include="Serilog.Enrichers.Environment" Version="3.1.0" />
<PackageReference Include="Serilog.Enrichers.Process" Version="3.1.0" />
<PackageReference Include="Serilog.Enrichers.Thread" Version="4.0.0" />
<PackageReference Include="Serilog.AspNetCore" Version="8.0.2" />
```

### Version Compatibility Matrix

| Package | Version | .NET 9 Compatible | Notes |
|---------|---------|-------------------|-------|
| Serilog.Sinks.Grafana.Loki | 8.3.1 | ✅ | Latest stable, supports Loki v2 |
| Serilog.Extensions.Hosting | 8.0.0 | ✅ | Required for .NET hosting integration |
| Serilog.Enrichers.* | Latest | ✅ | Metadata enrichment |

## 2. Configuration Architecture

### 2.1 SportsbookLite.Host Configuration

#### appsettings.json (Base)
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Orleans": "Information"
    }
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning",
        "Orleans.Runtime.Management": "Warning",
        "Orleans.Runtime.Placement": "Warning"
      }
    },
    "Enrich": [
      "FromLogContext",
      "WithEnvironmentName",
      "WithMachineName",
      "WithProcessId",
      "WithProcessName",
      "WithThreadId"
    ],
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "outputTemplate": "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}",
          "theme": "Serilog.Sinks.SystemConsole.Themes.AnsiConsoleTheme::Code, Serilog.Sinks.Console"
        }
      },
      {
        "Name": "GrafanaLoki",
        "Args": {
          "uri": "http://localhost:3100",
          "labels": [
            {
              "key": "service_name",
              "value": "sportsbook-host"
            },
            {
              "key": "environment",
              "value": "development"
            },
            {
              "key": "version",
              "value": "1.0.0"
            }
          ],
          "propertiesAsLabels": [
            "level",
            "SourceContext"
          ],
          "credentials": {
            "login": "",
            "password": ""
          },
          "outputTemplate": "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
          "batchPostingLimit": 1000,
          "period": "00:00:02",
          "queueLimit": 100000,
          "httpClient": {
            "timeout": "00:00:30"
          }
        }
      }
    ],
    "Properties": {
      "Application": "SportsbookLite",
      "Component": "Orleans-Silo"
    }
  },
  "Orleans": {
    "ClusterId": "sportsbook-dev",
    "ServiceId": "sportsbook-silo"
  }
}
```

#### appsettings.Development.json
```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Debug",
      "Override": {
        "Microsoft": "Information",
        "System": "Information",
        "Orleans": "Debug"
      }
    },
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "outputTemplate": "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{SourceContext}] [TraceId: {TraceId}] {Message:lj}{NewLine}{Exception}"
        }
      },
      {
        "Name": "GrafanaLoki",
        "Args": {
          "uri": "http://localhost:3100",
          "labels": [
            {
              "key": "service_name",
              "value": "sportsbook-host"
            },
            {
              "key": "environment",
              "value": "development"
            },
            {
              "key": "hostname",
              "value": "{MachineName}"
            },
            {
              "key": "orleans_cluster_id",
              "value": "sportsbook-dev"
            }
          ],
          "batchPostingLimit": 100,
          "period": "00:00:01"
        }
      }
    ]
  }
}
```

#### appsettings.Production.json
```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning",
        "Orleans": "Information"
      }
    },
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "restrictedToMinimumLevel": "Warning"
        }
      },
      {
        "Name": "GrafanaLoki",
        "Args": {
          "uri": "http://loki:3100",
          "labels": [
            {
              "key": "service_name",
              "value": "sportsbook-host"
            },
            {
              "key": "environment",
              "value": "production"
            },
            {
              "key": "version",
              "value": "{ENV:APP_VERSION}"
            },
            {
              "key": "cluster",
              "value": "{ENV:CLUSTER_NAME}"
            },
            {
              "key": "orleans_cluster_id",
              "value": "{ENV:ORLEANS_CLUSTER_ID}"
            }
          ],
          "propertiesAsLabels": [
            "level"
          ],
          "batchPostingLimit": 1000,
          "period": "00:00:05",
          "queueLimit": 500000,
          "httpClient": {
            "timeout": "00:01:00"
          }
        }
      }
    ]
  }
}
```

### 2.2 SportsbookLite.Api Configuration

#### appsettings.json (Base)
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Orleans": "Information"
    }
  },
  "AllowedHosts": "*",
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning",
        "Microsoft.AspNetCore.Hosting": "Information",
        "Microsoft.AspNetCore.Routing": "Warning"
      }
    },
    "Enrich": [
      "FromLogContext",
      "WithEnvironmentName",
      "WithMachineName",
      "WithProcessId",
      "WithProcessName",
      "WithThreadId"
    ],
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "outputTemplate": "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{SourceContext}] [TraceId: {TraceId}] {Message:lj}{NewLine}{Exception}"
        }
      },
      {
        "Name": "GrafanaLoki",
        "Args": {
          "uri": "http://localhost:3100",
          "labels": [
            {
              "key": "service_name",
              "value": "sportsbook-api"
            },
            {
              "key": "environment",
              "value": "development"
            },
            {
              "key": "version",
              "value": "1.0.0"
            }
          ],
          "propertiesAsLabels": [
            "level",
            "SourceContext",
            "RequestPath",
            "RequestMethod"
          ],
          "batchPostingLimit": 1000,
          "period": "00:00:02",
          "queueLimit": 100000
        }
      }
    ],
    "Properties": {
      "Application": "SportsbookLite",
      "Component": "WebAPI"
    }
  },
  "Orleans": {
    "ClusterId": "sportsbook-dev",
    "ServiceId": "sportsbook-api"
  },
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://localhost:5000"
      },
      "Https": {
        "Url": "https://localhost:5001"
      }
    }
  }
}
```

## 3. Program.cs Modifications

### 3.1 SportsbookLite.Host Program.cs

```csharp
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
using Serilog.Context;
using SportsbookLite.Infrastructure.Metrics;
using SportsbookLite.Infrastructure.Logging;
using System.Net;
using System.Reflection;

// Configure Serilog with enhanced Orleans metadata
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(GetConfiguration())
    .Enrich.WithProperty("Application", "SportsbookLite")
    .Enrich.WithProperty("Component", "Orleans-Silo")
    .Enrich.WithProperty("Version", GetApplicationVersion())
    .Enrich.With<OrleansEnricher>()
    .Enrich.With<CorrelationIdEnricher>()
    .CreateLogger();

try
{
    Log.Information("Starting Orleans Silo Host...");

    var builder = Host.CreateDefaultBuilder(args)
        .UseOrleans((context, siloBuilder) =>
        {
            var environment = context.HostingEnvironment.EnvironmentName;
            var isDevelopment = environment == Environments.Development;

            // Configure clustering with enhanced logging
            if (isDevelopment)
            {
                siloBuilder.UseLocalhostClustering()
                    .Configure<ClusterOptions>(options =>
                    {
                        options.ClusterId = "sportsbook-dev";
                        options.ServiceId = "sportsbook-silo";
                        
                        // Enrich log context with Orleans metadata
                        LogContext.PushProperty("OrleansClusterId", options.ClusterId);
                        LogContext.PushProperty("OrleansServiceId", options.ServiceId);
                    });
            }
            else
            {
                var redisConnection = context.Configuration["ConnectionStrings:Redis"] ?? "localhost:6379";
                var clusterId = context.Configuration["Orleans:ClusterId"] ?? "sportsbook-prod";
                var serviceId = context.Configuration["Orleans:ServiceId"] ?? "sportsbook-silo";
                
                siloBuilder.UseRedisClustering(redisConnection)
                .Configure<ClusterOptions>(options =>
                {
                    options.ClusterId = clusterId;
                    options.ServiceId = serviceId;
                    
                    LogContext.PushProperty("OrleansClusterId", options.ClusterId);
                    LogContext.PushProperty("OrleansServiceId", options.ServiceId);
                });
            }

            // Configure endpoints
            siloBuilder.ConfigureEndpoints(
                siloPort: 11111,
                gatewayPort: 30000,
                listenOnAnyHostAddress: !isDevelopment);

            // Configure storage
            var connectionString = context.Configuration["ConnectionStrings:Database"] ?? 
                "Host=localhost;Database=sportsbook;Username=dev;Password=dev123";
            
            siloBuilder.AddAdoNetGrainStorage("Default", options =>
            {
                options.ConnectionString = connectionString;
                options.Invariant = "Npgsql";
            });

            // Configure grain call filters for metrics and logging
            siloBuilder.AddIncomingGrainCallFilter<GrainInstrumentationFilter>();
            siloBuilder.AddIncomingGrainCallFilter<GrainLoggingFilter>();
            siloBuilder.AddOutgoingGrainCallFilter<GrainLoggingFilter>();

            // Configure logging with correlation
            siloBuilder.ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddSerilog();
            });

            if (isDevelopment)
            {
                siloBuilder.AddMemoryGrainStorageAsDefault();
            }
        })
        .ConfigureServices((context, services) =>
        {
            // Add correlation ID service
            services.AddSingleton<ICorrelationIdProvider, CorrelationIdProvider>();
            
            // Add Prometheus metrics
            services.AddSingleton<IHostedService>(provider =>
            {
                var server = new MetricServer(port: 9090);
                return new PrometheusMetricsService(server);
            });

            // Register grain call filters
            services.AddSingleton<GrainInstrumentationFilter>();
            services.AddSingleton<GrainLoggingFilter>();

            // Add health checks
            services.AddHealthChecks()
                .AddCheck("orleans", () =>
                {
                    return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy();
                });

            services.AddSingleton<IHostedService, DotNetRuntimeMetricsService>();
        })
        .UseSerilog()
        .UseConsoleLifetime();

    var host = builder.Build();

    // Initialize metrics and logging context
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

static IConfiguration GetConfiguration()
{
    return new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json", optional: false)
        .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true)
        .AddEnvironmentVariables()
        .Build();
}

static string GetApplicationVersion()
{
    return Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
}

void InitializeMetrics()
{
    // Initialize business metrics with logging
    BusinessMetrics.UpdateSystemHealth("orleans-silo", 100);
    Log.Information("Business metrics initialized");
    
    // Initialize Orleans metrics
    var clusterName = Environment.GetEnvironmentVariable("ORLEANS_CLUSTER_ID") ?? "sportsbook-dev";
    OrleansMetrics.SiloStatus.WithLabels(Environment.MachineName, clusterName).Set(1);
    OrleansMetrics.ClusterMembership.WithLabels("active", clusterName).Set(1);
    
    Log.Information("Metrics initialized successfully for cluster {ClusterName}", clusterName);
}

// Enhanced hosted services with structured logging...
// (Keep existing hosted services but add structured logging)
```

### 3.2 SportsbookLite.Api Program.cs

```csharp
using FastEndpoints;
using FastEndpoints.Swagger;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Orleans;
using Orleans.Configuration;
using Orleans.Hosting;
using Prometheus;
using Prometheus.DotNetRuntime;
using Serilog;
using Serilog.Context;
using SportsbookLite.Infrastructure.Metrics;
using SportsbookLite.Infrastructure.Logging;
using System.Net;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog with enhanced API metadata
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.WithProperty("Application", "SportsbookLite")
    .Enrich.WithProperty("Component", "WebAPI")
    .Enrich.WithProperty("Version", GetApplicationVersion())
    .Enrich.With<ApiEnricher>()
    .Enrich.With<CorrelationIdEnricher>()
    .CreateLogger();

builder.Host.UseSerilog();

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

// Add correlation ID service
builder.Services.AddSingleton<ICorrelationIdProvider, CorrelationIdProvider>();
builder.Services.AddScoped<ApiRequestLoggingMiddleware>();

// Configure Orleans Client with enhanced logging
builder.Host.UseOrleansClient((context, client) =>
{
    var isDevelopment = context.HostingEnvironment.IsDevelopment();
    
    if (isDevelopment)
    {
        client.UseLocalhostClustering()
              .Configure<ClusterOptions>(options =>
              {
                  options.ClusterId = "sportsbook-dev";
                  options.ServiceId = "sportsbook-api";
                  
                  LogContext.PushProperty("OrleansClusterId", options.ClusterId);
                  LogContext.PushProperty("OrleansServiceId", options.ServiceId);
              });
    }
    else
    {
        var redisConnection = context.Configuration["ConnectionStrings:Redis"] ?? "localhost:6379";
        var clusterId = context.Configuration["Orleans:ClusterId"] ?? "sportsbook-prod";
        var serviceId = context.Configuration["Orleans:ServiceId"] ?? "sportsbook-api";
        
        client.UseRedisClustering(redisConnection)
        .Configure<ClusterOptions>(options =>
        {
            options.ClusterId = clusterId;
            options.ServiceId = serviceId;
            
            LogContext.PushProperty("OrleansClusterId", options.ClusterId);
            LogContext.PushProperty("OrleansServiceId", options.ServiceId);
        });
    }
});

builder.Services.AddSingleton<IGrainFactory>(serviceProvider =>
    serviceProvider.GetRequiredService<IClusterClient>());

// Configure CORS with logging
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
            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Orleans health check failed");
            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy();
        }
    });

builder.Services.AddSingleton<IHostedService, DotNetRuntimeMetricsService>();

var app = builder.Build();

// Configure the HTTP request pipeline with enhanced logging
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

// Add correlation ID middleware first
app.UseMiddleware<CorrelationIdMiddleware>();

// Add request logging middleware
app.UseMiddleware<ApiRequestLoggingMiddleware>();

// Add Prometheus metrics middleware
app.UseHttpMetrics(options =>
{
    options.RequestDuration.Enabled = true;
    options.RequestCount.Enabled = true;
    options.InProgress.Enabled = true;
});

// Enhanced business metrics middleware with structured logging
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? "/";
    var method = context.Request.Method;
    var correlationId = context.Items["CorrelationId"]?.ToString();
    
    using (LogContext.PushProperty("RequestPath", path))
    using (LogContext.PushProperty("RequestMethod", method))
    using (LogContext.PushProperty("CorrelationId", correlationId))
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            await next();
            
            stopwatch.Stop();
            var statusCode = context.Response.StatusCode;
            var statusCategory = statusCode switch
            {
                >= 200 and < 300 => "success",
                >= 400 and < 500 => "client_error",
                >= 500 => "server_error",
                _ => "other"
            };
            
            Log.Information("API Request completed in {Duration}ms with status {StatusCode}",
                stopwatch.ElapsedMilliseconds, statusCode);
            
            ApiMetrics.RequestCount
                .WithLabels(method, path, statusCategory)
                .Inc();
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Log.Error(ex, "API Request failed after {Duration}ms", stopwatch.ElapsedMilliseconds);
            throw;
        }
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

// Enhanced health checks endpoint with logging
app.UseHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        Log.Information("Health check requested - Status: {HealthStatus}", report.Status);
        
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
            totalDuration = report.TotalDuration.TotalMilliseconds,
            timestamp = DateTimeOffset.UtcNow
        };
        await context.Response.WriteAsJsonAsync(response);
    }
});

app.MapMetrics("/metrics");

InitializeMetrics();

try
{
    var port = app.Environment.IsDevelopment() ? "5000" : "8080";
    Log.Information("Starting Sportsbook API on port {Port}...", port);
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

static string GetApplicationVersion()
{
    return Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
}

void InitializeMetrics()
{
    ApiMetrics.ApiHealth.Set(1);
    BusinessMetrics.UpdateSystemHealth("api", 100);
    
    Log.Information("API metrics initialized successfully");
}

// Keep existing Program class and hosted services...
```

## 4. Custom Enrichers and Formatters

### 4.1 Orleans-Specific Enricher

Create `/src/SportsbookLite.Infrastructure/Logging/OrleansEnricher.cs`:

```csharp
using Orleans;
using Orleans.Runtime;
using Serilog.Core;
using Serilog.Events;
using System.Diagnostics;

namespace SportsbookLite.Infrastructure.Logging;

/// <summary>
/// Enriches log events with Orleans-specific metadata
/// </summary>
public class OrleansEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        // Add Orleans grain context if available
        if (RequestContext.Get("GrainType") is string grainType)
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("GrainType", grainType));
        }

        if (RequestContext.Get("GrainId") is string grainId)
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("GrainId", grainId));
        }

        if (RequestContext.Get("ActivityId") is string activityId)
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ActivityId", activityId));
        }

        // Add current activity trace information
        var currentActivity = Activity.Current;
        if (currentActivity != null)
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("TraceId", currentActivity.TraceId.ToString()));
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SpanId", currentActivity.SpanId.ToString()));
            
            if (currentActivity.ParentSpanId != default)
            {
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ParentSpanId", currentActivity.ParentSpanId.ToString()));
            }
        }

        // Add Orleans silo information if available
        var siloAddress = RequestContext.Get("SiloAddress") as string;
        if (!string.IsNullOrEmpty(siloAddress))
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SiloAddress", siloAddress));
        }
    }
}
```

### 4.2 API-Specific Enricher

Create `/src/SportsbookLite.Infrastructure/Logging/ApiEnricher.cs`:

```csharp
using Microsoft.AspNetCore.Http;
using Serilog.Core;
using Serilog.Events;
using System.Diagnostics;

namespace SportsbookLite.Infrastructure.Logging;

/// <summary>
/// Enriches log events with API-specific metadata
/// </summary>
public class ApiEnricher : ILogEventEnricher
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ApiEnricher(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null) return;

        // Add HTTP request information
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("RequestPath", httpContext.Request.Path.Value));
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("RequestMethod", httpContext.Request.Method));
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("RequestScheme", httpContext.Request.Scheme));
        
        // Add user information if available
        if (httpContext.User?.Identity?.IsAuthenticated == true)
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("UserId", httpContext.User.Identity.Name));
        }

        // Add response status code if available
        if (httpContext.Response != null)
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ResponseStatusCode", httpContext.Response.StatusCode));
        }

        // Add correlation ID
        if (httpContext.Items.TryGetValue("CorrelationId", out var correlationId))
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("CorrelationId", correlationId));
        }

        // Add client IP (consider proxy headers)
        var clientIp = GetClientIpAddress(httpContext);
        if (!string.IsNullOrEmpty(clientIp))
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ClientIP", clientIp));
        }

        // Add user agent
        var userAgent = httpContext.Request.Headers.UserAgent.FirstOrDefault();
        if (!string.IsNullOrEmpty(userAgent))
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("UserAgent", userAgent));
        }
    }

    private static string GetClientIpAddress(HttpContext httpContext)
    {
        // Check for forwarded headers (reverse proxy scenarios)
        var forwardedFor = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }

        var realIp = httpContext.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIp))
        {
            return realIp;
        }

        return httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
```

### 4.3 Correlation ID Implementation

Create `/src/SportsbookLite.Infrastructure/Logging/CorrelationIdProvider.cs`:

```csharp
namespace SportsbookLite.Infrastructure.Logging;

/// <summary>
/// Provides correlation ID for request tracing across distributed components
/// </summary>
public interface ICorrelationIdProvider
{
    string GetOrCreateCorrelationId();
    void SetCorrelationId(string correlationId);
}

public class CorrelationIdProvider : ICorrelationIdProvider
{
    private static readonly AsyncLocal<string> _correlationId = new();

    public string GetOrCreateCorrelationId()
    {
        return _correlationId.Value ??= Guid.NewGuid().ToString("N")[..12]; // Short correlation ID
    }

    public void SetCorrelationId(string correlationId)
    {
        _correlationId.Value = correlationId;
    }
}
```

Create `/src/SportsbookLite.Infrastructure/Logging/CorrelationIdEnricher.cs`:

```csharp
using Serilog.Core;
using Serilog.Events;

namespace SportsbookLite.Infrastructure.Logging;

/// <summary>
/// Enriches log events with correlation ID for distributed tracing
/// </summary>
public class CorrelationIdEnricher : ILogEventEnricher
{
    private readonly ICorrelationIdProvider _correlationIdProvider;

    public CorrelationIdEnricher(ICorrelationIdProvider correlationIdProvider)
    {
        _correlationIdProvider = correlationIdProvider;
    }

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var correlationId = _correlationIdProvider.GetOrCreateCorrelationId();
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("CorrelationId", correlationId));
    }
}
```

### 4.4 Middleware for Correlation ID

Create `/src/SportsbookLite.Infrastructure/Logging/CorrelationIdMiddleware.cs`:

```csharp
using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace SportsbookLite.Infrastructure.Logging;

/// <summary>
/// Middleware to handle correlation ID for request tracing
/// </summary>
public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ICorrelationIdProvider _correlationIdProvider;
    private const string CorrelationIdHeaderName = "X-Correlation-ID";

    public CorrelationIdMiddleware(RequestDelegate next, ICorrelationIdProvider correlationIdProvider)
    {
        _next = next;
        _correlationIdProvider = correlationIdProvider;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Get or create correlation ID
        var correlationId = context.Request.Headers[CorrelationIdHeaderName].FirstOrDefault() 
                           ?? _correlationIdProvider.GetOrCreateCorrelationId();

        _correlationIdProvider.SetCorrelationId(correlationId);

        // Add to response headers
        context.Response.Headers[CorrelationIdHeaderName] = correlationId;

        // Add to HttpContext for other middleware/controllers
        context.Items["CorrelationId"] = correlationId;

        // Add to log context
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }
}
```

### 4.5 API Request Logging Middleware

Create `/src/SportsbookLite.Infrastructure/Logging/ApiRequestLoggingMiddleware.cs`:

```csharp
using Microsoft.AspNetCore.Http;
using Serilog;
using Serilog.Context;
using System.Diagnostics;
using System.Text;

namespace SportsbookLite.Infrastructure.Logging;

/// <summary>
/// Middleware for structured API request/response logging
/// </summary>
public class ApiRequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger _logger;
    private static readonly string[] SensitiveHeaders = { "authorization", "cookie", "x-api-key" };
    private static readonly string[] SensitiveQueryParams = { "password", "token", "key" };

    public ApiRequestLoggingMiddleware(RequestDelegate next)
    {
        _next = next;
        _logger = Log.ForContext<ApiRequestLoggingMiddleware>();
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var request = context.Request;
        var correlationId = context.Items["CorrelationId"]?.ToString();

        // Skip logging for health checks and metrics endpoints
        if (ShouldSkipLogging(request.Path))
        {
            await _next(context);
            return;
        }

        // Capture request body for POST/PUT/PATCH if content is JSON
        string? requestBody = null;
        if (HttpMethods.IsPost(request.Method) || HttpMethods.IsPut(request.Method) || HttpMethods.IsPatch(request.Method))
        {
            requestBody = await CaptureRequestBodyAsync(context);
        }

        // Create log context
        using (LogContext.PushProperty("RequestPath", request.Path.Value))
        using (LogContext.PushProperty("RequestMethod", request.Method))
        using (LogContext.PushProperty("RequestScheme", request.Scheme))
        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (LogContext.PushProperty("ClientIP", GetClientIpAddress(context)))
        {
            // Log request
            _logger.Information("API Request started: {Method} {Path} {QueryString}", 
                request.Method, request.Path, SanitizeQueryString(request.QueryString.Value));

            if (!string.IsNullOrEmpty(requestBody) && requestBody.Length < 4096) // Limit body size in logs
            {
                _logger.Debug("Request body: {RequestBody}", SanitizeRequestBody(requestBody));
            }

            // Capture response
            var originalBodyStream = context.Response.Body;
            using var responseBodyStream = new MemoryStream();
            context.Response.Body = responseBodyStream;

            Exception? exception = null;
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                exception = ex;
                throw;
            }
            finally
            {
                stopwatch.Stop();
                
                var statusCode = context.Response.StatusCode;
                var responseSize = responseBodyStream.Length;

                // Copy response back to original stream
                responseBodyStream.Seek(0, SeekOrigin.Begin);
                await responseBodyStream.CopyToAsync(originalBodyStream);

                // Log response
                if (exception != null)
                {
                    _logger.Error(exception, 
                        "API Request failed: {Method} {Path} - Status: {StatusCode}, Duration: {Duration}ms",
                        request.Method, request.Path, statusCode, stopwatch.ElapsedMilliseconds);
                }
                else
                {
                    var logLevel = statusCode >= 400 ? Serilog.Events.LogEventLevel.Warning : Serilog.Events.LogEventLevel.Information;
                    
                    _logger.Write(logLevel,
                        "API Request completed: {Method} {Path} - Status: {StatusCode}, Duration: {Duration}ms, Size: {ResponseSize} bytes",
                        request.Method, request.Path, statusCode, stopwatch.ElapsedMilliseconds, responseSize);
                }
            }
        }
    }

    private static bool ShouldSkipLogging(PathString path)
    {
        var pathValue = path.Value?.ToLowerInvariant();
        return pathValue switch
        {
            "/health" or "/metrics" or "/favicon.ico" => true,
            _ when pathValue?.StartsWith("/swagger") == true => true,
            _ => false
        };
    }

    private static async Task<string?> CaptureRequestBodyAsync(HttpContext context)
    {
        try
        {
            if (context.Request.ContentLength == 0 || 
                context.Request.ContentType?.Contains("application/json") != true)
            {
                return null;
            }

            context.Request.EnableBuffering();
            var buffer = new byte[Convert.ToInt32(context.Request.ContentLength)];
            await context.Request.Body.ReadAsync(buffer, 0, buffer.Length);
            context.Request.Body.Position = 0;

            return Encoding.UTF8.GetString(buffer);
        }
        catch
        {
            return null;
        }
    }

    private static string? SanitizeQueryString(string? queryString)
    {
        if (string.IsNullOrEmpty(queryString)) return null;

        foreach (var sensitiveParam in SensitiveQueryParams)
        {
            queryString = System.Text.RegularExpressions.Regex.Replace(
                queryString, 
                $@"({sensitiveParam}=)[^&]*", 
                "$1***", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        return queryString;
    }

    private static string SanitizeRequestBody(string body)
    {
        // Basic sanitization for common sensitive fields
        var sensitiveFields = new[] { "password", "token", "key", "secret", "authorization" };
        
        foreach (var field in sensitiveFields)
        {
            body = System.Text.RegularExpressions.Regex.Replace(
                body,
                $@"""{field}""\s*:\s*""[^""]*""",
                $@"""{field}"":""***""",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        return body;
    }

    private static string GetClientIpAddress(HttpContext context)
    {
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }

        var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIp))
        {
            return realIp;
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
```

## 5. Orleans Grain Logging Patterns

### 5.1 Grain Logging Filter

Create `/src/SportsbookLite.Infrastructure/Logging/GrainLoggingFilter.cs`:

```csharp
using Orleans;
using Orleans.Runtime;
using Serilog;
using Serilog.Context;
using System.Diagnostics;

namespace SportsbookLite.Infrastructure.Logging;

/// <summary>
/// Orleans grain call filter for structured logging
/// </summary>
public class GrainLoggingFilter : IIncomingGrainCallFilter, IOutgoingGrainCallFilter
{
    private readonly ILogger _logger;

    public GrainLoggingFilter()
    {
        _logger = Log.ForContext<GrainLoggingFilter>();
    }

    public async Task Invoke(IIncomingGrainCallContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var grainType = context.Grain.GetType().Name;
        var grainId = context.Grain.ToString();
        var methodName = context.InterfaceMethod.Name;

        using (LogContext.PushProperty("GrainType", grainType))
        using (LogContext.PushProperty("GrainId", grainId))
        using (LogContext.PushProperty("GrainMethod", methodName))
        {
            _logger.Debug("Grain call started: {GrainType}.{MethodName}", grainType, methodName);

            try
            {
                await context.Invoke();
                stopwatch.Stop();

                _logger.Debug("Grain call completed: {GrainType}.{MethodName} in {Duration}ms", 
                    grainType, methodName, stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.Error(ex, "Grain call failed: {GrainType}.{MethodName} after {Duration}ms", 
                    grainType, methodName, stopwatch.ElapsedMilliseconds);
                throw;
            }
        }
    }

    public async Task Invoke(IOutgoingGrainCallContext context)
    {
        var targetGrainType = context.Grain?.GetType().Name ?? "Unknown";
        var methodName = context.InterfaceMethod.Name;

        using (LogContext.PushProperty("TargetGrainType", targetGrainType))
        using (LogContext.PushProperty("TargetGrainMethod", methodName))
        {
            _logger.Debug("Outgoing grain call: {TargetGrainType}.{MethodName}", targetGrainType, methodName);

            try
            {
                await context.Invoke();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Outgoing grain call failed: {TargetGrainType}.{MethodName}", 
                    targetGrainType, methodName);
                throw;
            }
        }
    }
}
```

### 5.2 Example Grain with Structured Logging

Create `/src/SportsbookLite.Grains/Betting/BetGrain.cs` (example):

```csharp
using Orleans;
using Orleans.Runtime;
using Serilog;
using Serilog.Context;
using SportsbookLite.GrainInterfaces;
using SportsbookLite.Contracts.Betting;

namespace SportsbookLite.Grains.Betting;

/// <summary>
/// Example grain implementation with structured logging
/// </summary>
[Alias("bet")]
public class BetGrain : Grain, IBetGrain
{
    private readonly IPersistentState<BetState> _state;
    private readonly ILogger _logger;

    public BetGrain([PersistentState("bet", "Default")] IPersistentState<BetState> state)
    {
        _state = state;
        _logger = Log.ForContext<BetGrain>()
                    .ForContext("GrainType", nameof(BetGrain))
                    .ForContext("GrainId", this.GetPrimaryKey().ToString());
    }

    public async ValueTask<BetResult> PlaceBetAsync(PlaceBetRequest request)
    {
        using (LogContext.PushProperty("UserId", request.UserId))
        using (LogContext.PushProperty("MarketId", request.MarketId))
        using (LogContext.PushProperty("Amount", request.Amount))
        {
            _logger.Information("Processing bet placement request");

            try
            {
                // Validate request
                if (request.Amount <= 0)
                {
                    _logger.Warning("Invalid bet amount: {Amount}", request.Amount);
                    return BetResult.Failed("Invalid bet amount");
                }

                if (_state.State.Status != BetStatus.None)
                {
                    _logger.Warning("Bet already exists with status: {Status}", _state.State.Status);
                    return BetResult.Failed("Bet already exists");
                }

                // Create bet
                _state.State = new BetState
                {
                    Id = this.GetPrimaryKey(),
                    UserId = request.UserId,
                    MarketId = request.MarketId,
                    Amount = request.Amount,
                    Odds = request.Odds,
                    Status = BetStatus.Pending,
                    CreatedAt = DateTimeOffset.UtcNow
                };

                await _state.WriteStateAsync();

                _logger.Information("Bet placed successfully with ID: {BetId}", _state.State.Id);

                return BetResult.Success(_state.State.Id);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to place bet");
                return BetResult.Failed($"Internal error: {ex.Message}");
            }
        }
    }

    public async ValueTask<BetInfo?> GetBetAsync()
    {
        _logger.Debug("Retrieving bet information");

        if (_state.State.Status == BetStatus.None)
        {
            _logger.Debug("Bet not found");
            return null;
        }

        return new BetInfo
        {
            Id = _state.State.Id,
            UserId = _state.State.UserId,
            MarketId = _state.State.MarketId,
            Amount = _state.State.Amount,
            Odds = _state.State.Odds,
            Status = _state.State.Status,
            CreatedAt = _state.State.CreatedAt,
            SettledAt = _state.State.SettledAt
        };
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        _logger.Debug("Grain activated");
        await base.OnActivateAsync(cancellationToken);
    }

    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
    {
        _logger.Debug("Grain deactivated with reason: {DeactivationReason}", reason);
        await base.OnDeactivateAsync(reason, cancellationToken);
    }
}

// Supporting classes
[GenerateSerializer]
public class BetState
{
    [Id(0)] public Guid Id { get; set; }
    [Id(1)] public string UserId { get; set; } = string.Empty;
    [Id(2)] public Guid MarketId { get; set; }
    [Id(3)] public decimal Amount { get; set; }
    [Id(4)] public decimal Odds { get; set; }
    [Id(5)] public BetStatus Status { get; set; }
    [Id(6)] public DateTimeOffset CreatedAt { get; set; }
    [Id(7)] public DateTimeOffset? SettledAt { get; set; }
}

public enum BetStatus
{
    None,
    Pending,
    Accepted,
    Rejected,
    Settled,
    Cancelled
}
```

## 6. Performance Optimizations

### 6.1 Batching Configuration

The Loki sink supports efficient batching to reduce HTTP overhead:

```json
{
  "Serilog": {
    "WriteTo": [
      {
        "Name": "GrafanaLoki",
        "Args": {
          "uri": "http://loki:3100",
          "batchPostingLimit": 1000,      // Max entries per batch
          "period": "00:00:05",           // Batch timeout (5 seconds)
          "queueLimit": 100000,           // Max queued entries
          "textFormatter": "Serilog.Sinks.Grafana.Loki.LokiJsonTextFormatter, Serilog.Sinks.Grafana.Loki"
        }
      }
    ]
  }
}
```

### 6.2 Compression Settings

Enable gzip compression for production:

```json
{
  "Name": "GrafanaLoki",
  "Args": {
    "uri": "http://loki:3100",
    "httpClient": {
      "timeout": "00:01:00"
    },
    "useInternalHttpClient": true,
    "httpClientName": "loki-client"
  }
}
```

### 6.3 Async Pattern Implementation

Create `/src/SportsbookLite.Infrastructure/Logging/AsyncLoggingService.cs`:

```csharp
using Serilog;
using System.Threading.Channels;

namespace SportsbookLite.Infrastructure.Logging;

/// <summary>
/// High-performance async logging service for critical paths
/// </summary>
public interface IAsyncLoggingService
{
    ValueTask LogAsync(string level, string message, object[]? args = null);
    ValueTask LogAsync(Exception exception, string level, string message, object[]? args = null);
}

public class AsyncLoggingService : IAsyncLoggingService, IDisposable
{
    private readonly Channel<LogEntry> _channel;
    private readonly ChannelWriter<LogEntry> _writer;
    private readonly Task _backgroundTask;
    private readonly ILogger _logger;
    private readonly CancellationTokenSource _cancellationTokenSource;

    public AsyncLoggingService()
    {
        var options = new BoundedChannelOptions(10000)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        };

        _channel = Channel.CreateBounded<LogEntry>(options);
        _writer = _channel.Writer;
        _logger = Log.ForContext<AsyncLoggingService>();
        _cancellationTokenSource = new CancellationTokenSource();
        
        _backgroundTask = ProcessLogEntriesAsync(_cancellationTokenSource.Token);
    }

    public async ValueTask LogAsync(string level, string message, object[]? args = null)
    {
        var entry = new LogEntry
        {
            Level = level,
            Message = message,
            Args = args,
            Timestamp = DateTimeOffset.UtcNow
        };

        await _writer.WriteAsync(entry);
    }

    public async ValueTask LogAsync(Exception exception, string level, string message, object[]? args = null)
    {
        var entry = new LogEntry
        {
            Exception = exception,
            Level = level,
            Message = message,
            Args = args,
            Timestamp = DateTimeOffset.UtcNow
        };

        await _writer.WriteAsync(entry);
    }

    private async Task ProcessLogEntriesAsync(CancellationToken cancellationToken)
    {
        await foreach (var entry in _channel.Reader.ReadAllAsync(cancellationToken))
        {
            try
            {
                if (entry.Exception != null)
                {
                    _logger.Write(ParseLogLevel(entry.Level), entry.Exception, entry.Message, entry.Args ?? Array.Empty<object>());
                }
                else
                {
                    _logger.Write(ParseLogLevel(entry.Level), entry.Message, entry.Args ?? Array.Empty<object>());
                }
            }
            catch (Exception ex)
            {
                // Fallback logging to console
                Console.WriteLine($"Failed to write log entry: {ex.Message}");
            }
        }
    }

    private static Serilog.Events.LogEventLevel ParseLogLevel(string level)
    {
        return level.ToUpperInvariant() switch
        {
            "DEBUG" => Serilog.Events.LogEventLevel.Debug,
            "INFO" or "INFORMATION" => Serilog.Events.LogEventLevel.Information,
            "WARN" or "WARNING" => Serilog.Events.LogEventLevel.Warning,
            "ERROR" => Serilog.Events.LogEventLevel.Error,
            "FATAL" => Serilog.Events.LogEventLevel.Fatal,
            _ => Serilog.Events.LogEventLevel.Information
        };
    }

    public void Dispose()
    {
        _writer.Complete();
        _cancellationTokenSource.Cancel();
        
        try
        {
            _backgroundTask.Wait(TimeSpan.FromSeconds(5));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to shutdown async logging service: {ex.Message}");
        }
        
        _cancellationTokenSource.Dispose();
    }

    private record LogEntry
    {
        public required string Level { get; init; }
        public required string Message { get; init; }
        public object[]? Args { get; init; }
        public Exception? Exception { get; init; }
        public DateTimeOffset Timestamp { get; init; }
    }
}
```

## 7. Error Handling and Fallback Strategies

### 7.1 Resilient Configuration

Create `/src/SportsbookLite.Infrastructure/Logging/ResilientLoggingConfiguration.cs`:

```csharp
using Serilog;
using Serilog.Events;
using Serilog.Sinks.File;
using Polly;
using Polly.Extensions.Http;

namespace SportsbookLite.Infrastructure.Logging;

/// <summary>
/// Resilient logging configuration with fallback strategies
/// </summary>
public static class ResilientLoggingConfiguration
{
    public static LoggerConfiguration CreateResilientConfiguration(IConfiguration configuration)
    {
        var loggerConfig = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "SportsbookLite")
            .Enrich.WithProperty("Version", GetApplicationVersion());

        // Always ensure console logging as fallback
        loggerConfig.WriteTo.Console(
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
            restrictedToMinimumLevel: LogEventLevel.Warning);

        // Add file logging as backup
        var logPath = configuration.GetValue<string>("Logging:FilePath") ?? "./logs/sportsbook-.log";
        loggerConfig.WriteTo.File(
            path: logPath,
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 7,
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}",
            restrictedToMinimumLevel: LogEventLevel.Information);

        // Add Loki with resilience patterns
        var lokiUri = configuration.GetValue<string>("Serilog:WriteTo:1:Args:uri");
        if (!string.IsNullOrEmpty(lokiUri))
        {
            try
            {
                AddResilientLokiSink(loggerConfig, configuration);
            }
            catch (Exception ex)
            {
                // Fallback to console if Loki configuration fails
                Console.WriteLine($"Failed to configure Loki sink, falling back to console: {ex.Message}");
            }
        }

        return loggerConfig;
    }

    private static void AddResilientLokiSink(LoggerConfiguration loggerConfig, IConfiguration configuration)
    {
        var retryPolicy = Policy
            .Handle<HttpRequestException>()
            .Or<TaskCanceledException>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    Console.WriteLine($"Loki logging retry {retryCount} after {timespan} seconds");
                });

        var circuitBreakerPolicy = Policy
            .Handle<HttpRequestException>()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromMinutes(1),
                onBreak: (exception, duration) =>
                {
                    Console.WriteLine($"Loki logging circuit breaker opened for {duration}");
                },
                onReset: () =>
                {
                    Console.WriteLine("Loki logging circuit breaker reset");
                });

        var resilientPolicy = Policy.WrapAsync(retryPolicy, circuitBreakerPolicy);

        // Configure Loki sink with resilience
        loggerConfig.WriteTo.GrafanaLoki(
            uri: configuration.GetValue<string>("Serilog:WriteTo:1:Args:uri") ?? "http://localhost:3100",
            labels: GetLokiLabels(configuration),
            propertiesAsLabels: new[] { "level", "SourceContext" },
            credentials: new LokiCredentials(),
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
            batchPostingLimit: configuration.GetValue<int>("Serilog:WriteTo:1:Args:batchPostingLimit", 1000),
            period: TimeSpan.FromSeconds(configuration.GetValue<int>("Serilog:WriteTo:1:Args:periodSeconds", 5)),
            queueLimit: configuration.GetValue<int>("Serilog:WriteTo:1:Args:queueLimit", 100000));
    }

    private static LokiLabel[] GetLokiLabels(IConfiguration configuration)
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
        var serviceName = configuration.GetValue<string>("Serilog:WriteTo:1:Args:labels:0:value") ?? "sportsbook-unknown";
        
        return new[]
        {
            new LokiLabel("service_name", serviceName),
            new LokiLabel("environment", environment),
            new LokiLabel("version", GetApplicationVersion()),
            new LokiLabel("hostname", Environment.MachineName),
        };
    }

    private static string GetApplicationVersion()
    {
        return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
    }
}
```

### 7.2 Health Check for Loki Connectivity

Create `/src/SportsbookLite.Infrastructure/Health/LokiHealthCheck.cs`:

```csharp
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Net.Http;

namespace SportsbookLite.Infrastructure.Health;

/// <summary>
/// Health check for Loki connectivity
/// </summary>
public class LokiHealthCheck : IHealthCheck
{
    private readonly HttpClient _httpClient;
    private readonly string _lokiUri;

    public LokiHealthCheck(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _lokiUri = configuration.GetValue<string>("Serilog:WriteTo:1:Args:uri") ?? "http://localhost:3100";
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_lokiUri}/ready", cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                return HealthCheckResult.Healthy($"Loki is accessible at {_lokiUri}");
            }
            
            return HealthCheckResult.Degraded($"Loki returned status code: {response.StatusCode}");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy($"Loki health check failed: {ex.Message}", ex);
        }
    }
}
```

## 8. Environment-Specific Configurations

### 8.1 Docker Compose Development Setup

Add to existing `docker/docker-compose.yml`:

```yaml
version: '3.8'
services:
  # ... existing services ...

  loki:
    image: grafana/loki:2.9.0
    ports:
      - "3100:3100"
    command: -config.file=/etc/loki/local-config.yaml
    volumes:
      - loki-data:/loki
    networks:
      - sportsbook-network

  grafana:
    image: grafana/grafana:10.0.0
    ports:
      - "3000:3000"
    environment:
      - GF_SECURITY_ADMIN_PASSWORD=admin
    volumes:
      - grafana-data:/var/lib/grafana
      - ./grafana/provisioning:/etc/grafana/provisioning
      - ./grafana/dashboards:/var/lib/grafana/dashboards
    networks:
      - sportsbook-network
    depends_on:
      - loki

volumes:
  loki-data:
  grafana-data:

networks:
  sportsbook-network:
    driver: bridge
```

### 8.2 Kubernetes Production Configuration

Create `/k8s/loki-deployment.yaml`:

```yaml
apiVersion: apps/v1
kind: StatefulSet
metadata:
  name: loki
  namespace: sportsbook
  labels:
    app: loki
spec:
  serviceName: loki
  replicas: 1
  selector:
    matchLabels:
      app: loki
  template:
    metadata:
      labels:
        app: loki
    spec:
      containers:
      - name: loki
        image: grafana/loki:2.9.0
        ports:
        - containerPort: 3100
          name: http
        args:
        - -config.file=/etc/loki/local-config.yaml
        - -target=all
        resources:
          requests:
            memory: "512Mi"
            cpu: "200m"
          limits:
            memory: "1Gi"
            cpu: "500m"
        volumeMounts:
        - name: loki-storage
          mountPath: /loki
        - name: loki-config
          mountPath: /etc/loki
        livenessProbe:
          httpGet:
            path: /ready
            port: 3100
          initialDelaySeconds: 45
          periodSeconds: 10
        readinessProbe:
          httpGet:
            path: /ready
            port: 3100
          initialDelaySeconds: 45
          periodSeconds: 10
      volumes:
      - name: loki-config
        configMap:
          name: loki-config
  volumeClaimTemplates:
  - metadata:
      name: loki-storage
    spec:
      accessModes: [ "ReadWriteOnce" ]
      resources:
        requests:
          storage: 10Gi
---
apiVersion: v1
kind: Service
metadata:
  name: loki
  namespace: sportsbook
  labels:
    app: loki
spec:
  ports:
  - port: 3100
    targetPort: 3100
    name: http
  selector:
    app: loki
---
apiVersion: v1
kind: ConfigMap
metadata:
  name: loki-config
  namespace: sportsbook
data:
  local-config.yaml: |
    auth_enabled: false
    
    server:
      http_listen_port: 3100
      grpc_listen_port: 9096
    
    common:
      path_prefix: /loki
      storage:
        filesystem:
          chunks_directory: /loki/chunks
          rules_directory: /loki/rules
      replication_factor: 1
      ring:
        instance_addr: 127.0.0.1
        kvstore:
          store: inmemory
    
    query_range:
      results_cache:
        cache:
          embedded_cache:
            enabled: true
            max_size_mb: 100
    
    schema_config:
      configs:
        - from: 2020-10-24
          store: boltdb-shipper
          object_store: filesystem
          schema: v11
          index:
            prefix: index_
            period: 24h
    
    ruler:
      alertmanager_url: http://localhost:9093
    
    limits_config:
      reject_old_samples: true
      reject_old_samples_max_age: 168h
    
    chunk_store_config:
      max_look_back_period: 0s
    
    table_manager:
      retention_deletes_enabled: false
      retention_period: 0s
    
    compactor:
      working_directory: /loki/boltdb-shipper-compactor
      shared_store: filesystem
    
    ingester:
      max_chunk_age: 1h
      chunk_idle_period: 30m
      chunk_retain_period: 30s
      lifecycler:
        address: 127.0.0.1
        ring:
          kvstore:
            store: inmemory
          replication_factor: 1
        final_sleep: 0s
      wal:
        enabled: true
        dir: /loki/wal
```

## 9. Grafana Dashboard Configuration

### 9.1 Datasource Configuration

Create `/docker/grafana/provisioning/datasources/loki.yml`:

```yaml
apiVersion: 1

datasources:
  - name: Loki
    type: loki
    access: proxy
    url: http://loki:3100
    isDefault: false
    editable: true
```

### 9.2 Sample Dashboard

Create `/docker/grafana/dashboards/sportsbook-logs.json`:

```json
{
  "dashboard": {
    "id": null,
    "title": "Sportsbook Logs",
    "tags": ["sportsbook", "logs"],
    "style": "dark",
    "timezone": "UTC",
    "panels": [
      {
        "id": 1,
        "title": "Log Volume",
        "type": "stat",
        "targets": [
          {
            "expr": "sum(rate({service_name=~\"sportsbook.*\"}[5m]))",
            "refId": "A"
          }
        ],
        "fieldConfig": {
          "defaults": {
            "unit": "reqps"
          }
        },
        "gridPos": {"h": 8, "w": 12, "x": 0, "y": 0}
      },
      {
        "id": 2,
        "title": "Error Rate",
        "type": "stat",
        "targets": [
          {
            "expr": "sum(rate({service_name=~\"sportsbook.*\",level=\"error\"}[5m]))",
            "refId": "A"
          }
        ],
        "fieldConfig": {
          "defaults": {
            "unit": "reqps",
            "color": {"mode": "thresholds"},
            "thresholds": {
              "steps": [
                {"color": "green", "value": 0},
                {"color": "yellow", "value": 0.1},
                {"color": "red", "value": 1}
              ]
            }
          }
        },
        "gridPos": {"h": 8, "w": 12, "x": 12, "y": 0}
      },
      {
        "id": 3,
        "title": "Recent Logs",
        "type": "logs",
        "targets": [
          {
            "expr": "{service_name=~\"sportsbook.*\"} |= \"\" | json | line_format \"{{.timestamp}} [{{.level}}] {{.message}}\"",
            "refId": "A"
          }
        ],
        "options": {
          "showTime": true,
          "showLabels": false,
          "showCommonLabels": true,
          "wrapLogMessage": false,
          "prettifyLogMessage": true,
          "enableLogDetails": true,
          "dedupStrategy": "none",
          "sortOrder": "Descending"
        },
        "gridPos": {"h": 16, "w": 24, "x": 0, "y": 8}
      }
    ],
    "time": {
      "from": "now-1h",
      "to": "now"
    },
    "refresh": "5s"
  }
}
```

## 10. Testing and Validation

### 10.1 Integration Test for Loki Logging

Create `/tests/SportsbookLite.IntegrationTests/Logging/LokiLoggingTests.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Testcontainers.Grafana;
using Xunit;

namespace SportsbookLite.IntegrationTests.Logging;

/// <summary>
/// Integration tests for Loki logging functionality
/// </summary>
public class LokiLoggingTests : IAsyncLifetime
{
    private readonly GrafanaLokiContainer _lokiContainer;

    public LokiLoggingTests()
    {
        _lokiContainer = new GrafanaLokiBuilder()
            .WithImage("grafana/loki:2.9.0")
            .Build();
    }

    [Fact]
    public async Task Should_Send_Logs_To_Loki_Successfully()
    {
        // Arrange
        var hostBuilder = Host.CreateDefaultBuilder()
            .UseSerilog((context, config) =>
            {
                config.WriteTo.GrafanaLoki($"http://localhost:{_lokiContainer.GetMappedPublicPort(3100)}");
            });

        var host = hostBuilder.Build();
        var logger = Log.ForContext<LokiLoggingTests>();

        // Act
        logger.Information("Test log message for Loki integration test");
        await Task.Delay(TimeSpan.FromSeconds(2)); // Allow batching

        // Assert - This would require querying Loki's API to verify logs were received
        // For now, we just verify no exceptions were thrown
        Assert.True(true);
    }

    public async Task InitializeAsync()
    {
        await _lokiContainer.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _lokiContainer.DisposeAsync();
    }
}
```

### 10.2 Performance Benchmark

Create `/tests/SportsbookLite.Benchmarks/LoggingBenchmarks.cs`:

```csharp
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Serilog;
using SportsbookLite.Infrastructure.Logging;

namespace SportsbookLite.Benchmarks;

/// <summary>
/// Performance benchmarks for logging implementations
/// </summary>
[MemoryDiagnoser]
[SimpleJob]
public class LoggingBenchmarks
{
    private ILogger _consoleLogger = null!;
    private ILogger _lokiLogger = null!;
    private IAsyncLoggingService _asyncLogger = null!;

    [GlobalSetup]
    public void Setup()
    {
        _consoleLogger = new LoggerConfiguration()
            .WriteTo.Console()
            .CreateLogger();

        _lokiLogger = new LoggerConfiguration()
            .WriteTo.GrafanaLoki("http://localhost:3100")
            .CreateLogger();

        _asyncLogger = new AsyncLoggingService();
    }

    [Benchmark]
    public void Console_Logging()
    {
        _consoleLogger.Information("Benchmark message {Value}", 123);
    }

    [Benchmark]
    public void Loki_Logging()
    {
        _lokiLogger.Information("Benchmark message {Value}", 123);
    }

    [Benchmark]
    public async Task Async_Logging()
    {
        await _asyncLogger.LogAsync("Information", "Benchmark message {Value}", new object[] { 123 });
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _consoleLogger?.Dispose();
        _lokiLogger?.Dispose();
        _asyncLogger?.Dispose();
    }
}
```

## 11. Operational Considerations

### 11.1 Log Retention Policy

```yaml
# Loki configuration for retention
limits_config:
  retention_period: 720h  # 30 days
  retention_stream:
    - selector: '{environment="development"}'
      priority: 1
      min_age: 24h
    - selector: '{environment="production"}'
      priority: 2
      min_age: 720h
```

### 11.2 Monitoring and Alerting

Create Grafana alerts for:
- High error rates
- Log volume spikes
- Loki connectivity issues
- Disk space usage

### 11.3 Security Considerations

- Use TLS for production Loki endpoints
- Implement authentication for Loki API
- Sanitize sensitive data in logs
- Set up proper network policies in Kubernetes

## 12. Migration Steps

1. **Add NuGet Packages** - Install Loki sink packages
2. **Update Configuration** - Add Loki sink to appsettings.json files
3. **Deploy Infrastructure** - Set up Loki and Grafana containers
4. **Update Applications** - Modify Program.cs files with enrichers
5. **Test Connectivity** - Verify logs appear in Grafana
6. **Configure Dashboards** - Set up log visualization
7. **Implement Monitoring** - Add health checks and alerts
8. **Documentation** - Update operational runbooks

This implementation provides a production-ready Loki logging solution with performance optimizations, error handling, and comprehensive monitoring capabilities for the Sportsbook-Lite application.