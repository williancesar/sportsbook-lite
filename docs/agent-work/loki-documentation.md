# Loki Logging Documentation - Sportsbook-Lite

## Table of Contents

1. [Executive Summary](#executive-summary)
2. [Quick Start Guide for Developers](#quick-start-guide-for-developers)
3. [Architecture Overview](#architecture-overview)
4. [Configuration Reference](#configuration-reference)
5. [Developer Guide - Structured Logging](#developer-guide---structured-logging)
6. [Operational Runbook](#operational-runbook)
7. [LogQL Query Examples](#logql-query-examples)
8. [Troubleshooting Guide](#troubleshooting-guide)
9. [Best Practices](#best-practices)
10. [Integration with Monitoring Stack](#integration-with-monitoring-stack)
11. [Performance Tuning](#performance-tuning)
12. [Security Considerations](#security-considerations)
13. [Maintenance Procedures](#maintenance-procedures)
14. [FAQ](#faq)
15. [References and Resources](#references-and-resources)

---

## Executive Summary

Grafana Loki is our centralized logging solution for the Sportsbook-Lite distributed Orleans application. It provides:

- **Centralized Log Aggregation**: Collects logs from all services (Orleans Silo, API, infrastructure)
- **Horizontal Scalability**: Scales with our distributed architecture
- **Cost-Effective Storage**: Uses object storage and compression
- **Native Grafana Integration**: Seamless correlation with metrics
- **Label-Based Indexing**: Efficient querying without full-text indexing
- **Multi-Tenancy Support**: Isolates logs by environment and service

### Key Benefits
- **Unified Observability**: Correlate logs with metrics in single Grafana dashboards
- **Minimal Resource Usage**: Lightweight compared to Elasticsearch
- **Developer-Friendly**: Simple LogQL query language
- **Cloud-Native**: Kubernetes-native with built-in horizontal scaling

### System Requirements
- Docker/Kubernetes for deployment
- 2GB RAM minimum (4GB recommended for production)
- 50GB storage for 30-day retention (adjustable)
- Network connectivity between services

---

## Quick Start Guide for Developers

### 1. Local Development Setup

#### Start Loki with Docker Compose

```bash
# Start the complete monitoring stack including Loki
docker-compose -f docker/docker-compose.yml -f docker/docker-compose.monitoring.yml up -d

# Verify Loki is running
curl -s http://localhost:3100/ready | jq .

# Check Grafana is accessible
open http://localhost:3000  # Default: admin/admin
```

#### View Logs in Grafana

1. Navigate to Grafana: `http://localhost:3000`
2. Go to Explore (compass icon)
3. Select "Loki" datasource
4. Use this query to see all logs:
   ```logql
   {service_name=~"sportsbook.*"}
   ```

### 2. Writing Structured Logs in C#

#### Basic Structured Logging

```csharp
using Serilog;

public class BetGrain : Grain, IBetGrain
{
    private readonly ILogger _logger;
    
    public BetGrain(ILogger<BetGrain> logger)
    {
        _logger = logger.ForContext<BetGrain>();
    }
    
    public async ValueTask<BetResult> PlaceBetAsync(PlaceBetRequest request)
    {
        // Structured logging with properties
        _logger.Information("Placing bet {BetId} for user {UserId} on market {MarketId} with amount {Amount:C}", 
            request.BetId, 
            request.UserId, 
            request.MarketId, 
            request.Amount);
        
        try
        {
            var result = await ProcessBetAsync(request);
            
            _logger.Information("Bet {BetId} placed successfully with odds {Odds}", 
                request.BetId, 
                result.Odds);
                
            return result;
        }
        catch (InsufficientFundsException ex)
        {
            _logger.Warning(ex, "Insufficient funds for bet {BetId}. Required: {Required:C}, Available: {Available:C}",
                request.BetId,
                request.Amount,
                ex.AvailableBalance);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to place bet {BetId}", request.BetId);
            throw;
        }
    }
}
```

#### Correlation IDs for Distributed Tracing

```csharp
public class CorrelationMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault() 
            ?? Guid.NewGuid().ToString();
            
        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (LogContext.PushProperty("RequestPath", context.Request.Path))
        using (LogContext.PushProperty("RequestMethod", context.Request.Method))
        {
            context.Response.Headers.Add("X-Correlation-ID", correlationId);
            await next(context);
        }
    }
}
```

### 3. Common Development Commands

```bash
# Tail logs from specific service
docker logs -f sportsbook-orleans --tail 100

# Query logs via Loki API
curl -G -s "http://localhost:3100/loki/api/v1/query_range" \
  --data-urlencode 'query={service_name="sportsbook-host"}' \
  --data-urlencode 'start=1h' | jq .

# Test log ingestion
curl -X POST "http://localhost:3100/loki/api/v1/push" \
  -H "Content-Type: application/json" \
  -d '{
    "streams": [
      {
        "stream": {
          "service": "test",
          "level": "info"
        },
        "values": [
          ["'$(date +%s)000000000'", "Test log message"]
        ]
      }
    ]
  }'
```

---

## Architecture Overview

### Loki Components in Sportsbook-Lite

```
┌─────────────────────────────────────────────────────────────────────┐
│                        Loki Logging Architecture                     │
├─────────────────────────────────────────────────────────────────────┤
│                                                                      │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐             │
│  │Orleans Silo  │  │FastEndpoints │  │Infrastructure│             │
│  │   (Host)     │  │    (API)     │  │  Services    │             │
│  └──────┬───────┘  └──────┬───────┘  └──────┬───────┘             │
│         │                  │                  │                      │
│         └──────────────────┴──────────────────┘                     │
│                            │                                         │
│                     Serilog + Loki Sink                             │
│                            │                                         │
│         ┌──────────────────▼──────────────────┐                     │
│         │          Loki Gateway               │                     │
│         │     (HTTP API - Port 3100)          │                     │
│         └──────────────────┬──────────────────┘                     │
│                            │                                         │
│         ┌──────────────────▼──────────────────┐                     │
│         │          Loki Distributor           │                     │
│         │     (Hashing & Replication)         │                     │
│         └──────────────────┬──────────────────┘                     │
│                            │                                         │
│         ┌──────────────────▼──────────────────┐                     │
│         │          Loki Ingester              │                     │
│         │    (In-memory chunks & WAL)         │                     │
│         └──────────────────┬──────────────────┘                     │
│                            │                                         │
│         ┌──────────────────▼──────────────────┐                     │
│         │      Object Storage (S3/Minio)      │                     │
│         │    (Long-term chunk storage)        │                     │
│         └──────────────────────────────────────┘                     │
│                                                                      │
│         ┌──────────────────────────────────────┐                     │
│         │            Grafana                   │                     │
│         │    (Query & Visualization)           │                     │
│         └──────────────────────────────────────┘                     │
│                                                                      │
└─────────────────────────────────────────────────────────────────────┘
```

### Data Flow

1. **Log Generation**: Applications generate structured logs via Serilog
2. **Batching**: Serilog.Sinks.Grafana.Loki batches logs (default: 1000 entries or 5 seconds)
3. **Compression**: Logs are compressed (gzip) before transmission
4. **Ingestion**: Loki distributor receives and validates logs
5. **Indexing**: Minimal indexing on labels (service, environment, level)
6. **Storage**: Chunks stored in object storage with metadata in index
7. **Querying**: Grafana queries via LogQL, Loki fetches relevant chunks

---

## Configuration Reference

### Serilog Configuration for Loki

#### appsettings.json (API Project)

```json
{
  "Serilog": {
    "Using": ["Serilog.Sinks.Console", "Serilog.Sinks.Grafana.Loki"],
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "Orleans": "Warning",
        "System": "Warning",
        "FastEndpoints": "Information"
      }
    },
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "theme": "Serilog.Sinks.SystemConsole.Themes.AnsiConsoleTheme::Code, Serilog.Sinks.Console",
          "outputTemplate": "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}"
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
          "propertiesAsLabels": ["level", "app"],
          "batchPostingLimit": 1000,
          "period": "00:00:05",
          "queueLimit": 10000,
          "httpRequestTimeout": "00:00:30",
          "textFormatter": "Serilog.Sinks.Grafana.Loki.LokiJsonTextFormatter, Serilog.Sinks.Grafana.Loki"
        }
      }
    ],
    "Enrich": ["FromLogContext", "WithMachineName", "WithThreadId", "WithEnvironmentName"],
    "Properties": {
      "Application": "SportsbookLite.Api"
    }
  }
}
```

#### Program.cs Configuration (Host Project)

```csharp
// Enhanced Serilog configuration with Loki
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Orleans", LogEventLevel.Warning)
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
    .WriteTo.GrafanaLoki(
        uri: configuration["Loki:Uri"] ?? "http://localhost:3100",
        labels: new[]
        {
            new LokiLabel { Key = "service_name", Value = "sportsbook-host" },
            new LokiLabel { Key = "environment", Value = environment },
            new LokiLabel { Key = "orleans_cluster_id", Value = orleansClusterId },
            new LokiLabel { Key = "orleans_service_id", Value = orleansServiceId },
            new LokiLabel { Key = "hostname", Value = Environment.MachineName },
            new LokiLabel { Key = "version", Value = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown" }
        },
        batchPostingLimit: 1000,
        period: TimeSpan.FromSeconds(5),
        queueLimit: 10000,
        httpClient: new HttpClient { Timeout = TimeSpan.FromSeconds(30) })
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithEnvironmentName()
    .Enrich.WithProperty("Application", "SportsbookLite.Host")
    .CreateLogger();
```

### Loki Server Configuration

#### loki-config.yaml

```yaml
auth_enabled: false

server:
  http_listen_port: 3100
  grpc_listen_port: 9096
  log_level: info

common:
  path_prefix: /tmp/loki
  storage:
    filesystem:
      chunks_directory: /tmp/loki/chunks
      rules_directory: /tmp/loki/rules
  replication_factor: 1
  ring:
    instance_addr: 127.0.0.1
    kvstore:
      store: inmemory

schema_config:
  configs:
    - from: 2024-01-01
      store: boltdb-shipper
      object_store: filesystem
      schema: v11
      index:
        prefix: index_
        period: 24h

storage_config:
  boltdb_shipper:
    active_index_directory: /tmp/loki/boltdb-shipper-active
    cache_location: /tmp/loki/boltdb-shipper-cache
    cache_ttl: 24h
    shared_store: filesystem
  filesystem:
    directory: /tmp/loki/chunks

compactor:
  working_directory: /tmp/loki/boltdb-shipper-compactor
  shared_store: filesystem
  retention_enabled: true
  retention_delete_delay: 2h
  retention_delete_worker_count: 150

limits_config:
  enforce_metric_name: false
  reject_old_samples: true
  reject_old_samples_max_age: 168h
  ingestion_rate_mb: 16
  ingestion_burst_size_mb: 32
  max_entries_limit_per_query: 5000
  max_query_length: 721h
  max_query_parallelism: 32
  retention_period: 744h  # 31 days

chunk_store_config:
  max_look_back_period: 0s

table_manager:
  retention_deletes_enabled: true
  retention_period: 744h

query_range:
  results_cache:
    cache:
      embedded_cache:
        enabled: true
        max_size_mb: 100
```

### Docker Compose for Loki

```yaml
# docker/docker-compose.monitoring.yml (Loki section)
loki:
  image: grafana/loki:2.9.3
  container_name: sportsbook-loki
  restart: unless-stopped
  ports:
    - "3100:3100"
  command: -config.file=/etc/loki/local-config.yaml
  volumes:
    - ./monitoring/loki/loki-config.yaml:/etc/loki/local-config.yaml:ro
    - loki_data:/tmp/loki
  networks:
    - sportsbook-network
    - monitoring-network
  healthcheck:
    test: ["CMD", "wget", "--no-verbose", "--tries=1", "--spider", "http://localhost:3100/ready"]
    interval: 30s
    timeout: 10s
    retries: 3
    start_period: 30s
  environment:
    - LOKI_LOG_LEVEL=info
```

---

## Developer Guide - Structured Logging

### Log Levels and When to Use Them

| Level | Usage | Examples |
|-------|-------|----------|
| **Verbose** | Detailed diagnostic information | Grain state changes, detailed request/response data |
| **Debug** | Debugging information | Method entry/exit, variable values |
| **Information** | Normal application flow | Successful operations, business events |
| **Warning** | Unexpected but handled situations | Retry attempts, degraded performance |
| **Error** | Errors that don't stop the application | Failed operations, caught exceptions |
| **Fatal** | Application-terminating errors | Startup failures, critical service unavailable |

### Structured Logging Patterns

#### 1. Business Event Logging

```csharp
public class BettingEventLogger
{
    private readonly ILogger _logger;
    
    public void LogBetPlaced(Bet bet, decimal odds)
    {
        _logger.Information("Bet placed {@Bet} with {Odds:F2} odds at {Timestamp}",
            new 
            { 
                bet.Id, 
                bet.UserId, 
                bet.MarketId, 
                bet.Amount,
                bet.Type 
            },
            odds,
            DateTimeOffset.UtcNow);
    }
    
    public void LogBetSettled(Guid betId, BetOutcome outcome, decimal payout)
    {
        _logger.Information("Bet settled: {BetId} with outcome {Outcome} and payout {Payout:C}",
            betId,
            outcome.ToString(),
            payout);
    }
}
```

#### 2. Performance Logging

```csharp
public class PerformanceLogger
{
    private readonly ILogger _logger;
    
    public IDisposable MeasureOperation(string operationName, Dictionary<string, object> properties = null)
    {
        return new OperationTimer(_logger, operationName, properties);
    }
    
    private class OperationTimer : IDisposable
    {
        private readonly ILogger _logger;
        private readonly string _operationName;
        private readonly Dictionary<string, object> _properties;
        private readonly Stopwatch _stopwatch;
        
        public OperationTimer(ILogger logger, string operationName, Dictionary<string, object> properties)
        {
            _logger = logger;
            _operationName = operationName;
            _properties = properties ?? new Dictionary<string, object>();
            _stopwatch = Stopwatch.StartNew();
            
            _logger.Debug("Starting operation {OperationName} with {@Properties}",
                operationName, properties);
        }
        
        public void Dispose()
        {
            _stopwatch.Stop();
            _properties["DurationMs"] = _stopwatch.ElapsedMilliseconds;
            
            if (_stopwatch.ElapsedMilliseconds > 1000)
            {
                _logger.Warning("Slow operation {OperationName} took {DurationMs}ms {@Properties}",
                    _operationName, _stopwatch.ElapsedMilliseconds, _properties);
            }
            else
            {
                _logger.Information("Completed operation {OperationName} in {DurationMs}ms",
                    _operationName, _stopwatch.ElapsedMilliseconds);
            }
        }
    }
}

// Usage
using (performanceLogger.MeasureOperation("PlaceBet", new Dictionary<string, object> 
    { 
        ["BetId"] = betId,
        ["UserId"] = userId 
    }))
{
    // Operation code
}
```

#### 3. Exception Logging with Context

```csharp
public class ExceptionLogger
{
    private readonly ILogger _logger;
    
    public void LogException(Exception ex, string operation, object context = null)
    {
        var errorId = Guid.NewGuid();
        
        _logger.Error(ex, "Error {ErrorId} in {Operation} with context {@Context}",
            errorId,
            operation,
            context ?? new { });
            
        // Log additional details for specific exception types
        switch (ex)
        {
            case DbUpdateException dbEx:
                _logger.Error("Database error {ErrorId}: {SqlError}",
                    errorId,
                    dbEx.InnerException?.Message);
                break;
                
            case TimeoutException:
                _logger.Error("Timeout error {ErrorId} after default timeout period", errorId);
                break;
                
            case OrleansException orleansEx:
                _logger.Error("Orleans error {ErrorId}: Grain={GrainType}, Method={Method}",
                    errorId,
                    orleansEx.Data["GrainType"],
                    orleansEx.Data["Method"]);
                break;
        }
    }
}
```

#### 4. Distributed Tracing with Correlation IDs

```csharp
public class DistributedTracingLogger
{
    public static IDisposable BeginScope(string correlationId, string userId = null)
    {
        var properties = new List<IDisposable>
        {
            LogContext.PushProperty("CorrelationId", correlationId),
            LogContext.PushProperty("TraceId", Activity.Current?.TraceId.ToString() ?? ""),
            LogContext.PushProperty("SpanId", Activity.Current?.SpanId.ToString() ?? "")
        };
        
        if (!string.IsNullOrEmpty(userId))
        {
            properties.Add(LogContext.PushProperty("UserId", userId));
        }
        
        return new CompositeDisposable(properties);
    }
    
    private class CompositeDisposable : IDisposable
    {
        private readonly List<IDisposable> _disposables;
        
        public CompositeDisposable(List<IDisposable> disposables)
        {
            _disposables = disposables;
        }
        
        public void Dispose()
        {
            foreach (var disposable in _disposables)
            {
                disposable?.Dispose();
            }
        }
    }
}

// Usage in grain
public async Task<BetResult> PlaceBetAsync(PlaceBetRequest request)
{
    using (DistributedTracingLogger.BeginScope(request.CorrelationId, request.UserId))
    {
        _logger.Information("Processing bet placement request");
        // Rest of the method
    }
}
```

### Log Message Templates Best Practices

#### DO ✅

```csharp
// Use structured properties
_logger.Information("User {UserId} placed bet {BetId} for {Amount:C}", userId, betId, amount);

// Use semantic property names
_logger.Information("Bet settlement completed in {ElapsedMs}ms for {BetCount} bets", elapsed, count);

// Include units in property names when not obvious
_logger.Information("Cache hit rate: {HitRatePercent}%", hitRate * 100);

// Use @ for complex objects
_logger.Debug("Processing request {@Request}", request);

// Use consistent property names across the application
_logger.Information("Operation {OperationName} completed for {UserId}", op, userId);
```

#### DON'T ❌

```csharp
// Don't use string concatenation
_logger.Information("User " + userId + " placed bet"); // Bad!

// Don't use string interpolation
_logger.Information($"Bet {betId} placed"); // Bad!

// Don't log sensitive information
_logger.Information("User logged in with password {Password}", password); // Never!

// Don't use generic property names
_logger.Information("Value is {Value}", someValue); // Too generic

// Don't over-log in loops
foreach (var item in items) // Bad if items is large!
{
    _logger.Debug("Processing item {Item}", item);
}
```

---

## Operational Runbook

### Service Health Checks

#### 1. Check Loki Service Status

```bash
# Check if Loki is running
curl -s http://localhost:3100/ready | jq .

# Expected output:
{
  "ready": true
}

# Check Loki metrics
curl -s http://localhost:3100/metrics | grep loki_ingester_chunks_stored_total

# Check ring status
curl -s http://localhost:3100/ring | jq .
```

#### 2. Monitor Ingestion Rate

```logql
# In Grafana, monitor ingestion rate
rate(loki_distributor_bytes_received_total[5m])

# Check for ingestion errors
rate(loki_distributor_ingester_append_failures_total[5m]) > 0
```

### Common Operations

#### 1. Manual Log Flush

```csharp
// Force flush logs before shutdown
Log.CloseAndFlush();

// Or with timeout
Log.CloseAndFlush(TimeSpan.FromSeconds(10));
```

#### 2. Change Log Level at Runtime

```csharp
// Add dynamic log level switching
public class LogLevelController : ControllerBase
{
    [HttpPost("loglevel")]
    public IActionResult SetLogLevel([FromBody] string level)
    {
        var levelSwitch = new LoggingLevelSwitch();
        
        if (Enum.TryParse<LogEventLevel>(level, out var logLevel))
        {
            levelSwitch.MinimumLevel = logLevel;
            return Ok($"Log level set to {logLevel}");
        }
        
        return BadRequest("Invalid log level");
    }
}
```

#### 3. Export Logs

```bash
# Export logs for specific time range
logcli query '{service_name="sportsbook-host"}' \
  --from="2024-01-15T10:00:00Z" \
  --to="2024-01-15T11:00:00Z" \
  --output=jsonl > logs-export.jsonl

# Export to CSV
logcli query '{service_name="sportsbook-api"}' \
  --output=csv > api-logs.csv
```

### Incident Response Procedures

#### High Memory Usage

```bash
# 1. Check Loki memory usage
docker stats sportsbook-loki

# 2. Check ingestion buffer size
curl -s http://localhost:3100/metrics | grep loki_ingester_memory_chunks

# 3. Force flush if needed
curl -X POST http://localhost:3100/flush

# 4. Restart with memory limits
docker update --memory="2g" --memory-swap="2g" sportsbook-loki
docker restart sportsbook-loki
```

#### Log Ingestion Stopped

```bash
# 1. Check Loki health
curl -s http://localhost:3100/ready

# 2. Check application connectivity
docker exec sportsbook-api curl -s http://loki:3100/ready

# 3. Check for rate limiting
curl -s http://localhost:3100/metrics | grep -E "rate_limit|rejected"

# 4. Review Loki logs
docker logs sportsbook-loki --tail 100

# 5. Restart services in order
docker restart sportsbook-loki
sleep 30
docker restart sportsbook-orleans
docker restart sportsbook-api
```

#### Disk Space Issues

```bash
# 1. Check disk usage
df -h /var/lib/docker/volumes/loki_data

# 2. Check retention settings
grep retention /path/to/loki-config.yaml

# 3. Manually trigger compaction
curl -X POST http://localhost:3100/loki/api/v1/compact

# 4. Clean old chunks (if retention not working)
find /var/lib/docker/volumes/loki_data -name "*.gz" -mtime +30 -delete

# 5. Restart Loki
docker restart sportsbook-loki
```

### Backup and Recovery

#### Backup Procedure

```bash
#!/bin/bash
# backup-loki.sh

BACKUP_DIR="/backup/loki/$(date +%Y%m%d_%H%M%S)"
LOKI_DATA="/var/lib/docker/volumes/loki_data/_data"

# Create backup directory
mkdir -p "$BACKUP_DIR"

# Stop Loki to ensure consistency
docker stop sportsbook-loki

# Backup data
tar -czf "$BACKUP_DIR/loki-chunks.tar.gz" "$LOKI_DATA/chunks"
tar -czf "$BACKUP_DIR/loki-index.tar.gz" "$LOKI_DATA/index"

# Backup configuration
cp /path/to/loki-config.yaml "$BACKUP_DIR/"

# Restart Loki
docker start sportsbook-loki

# Upload to S3 (optional)
aws s3 cp "$BACKUP_DIR" s3://sportsbook-backups/loki/ --recursive

echo "Backup completed: $BACKUP_DIR"
```

#### Recovery Procedure

```bash
#!/bin/bash
# restore-loki.sh

BACKUP_DIR="$1"
LOKI_DATA="/var/lib/docker/volumes/loki_data/_data"

if [ -z "$BACKUP_DIR" ]; then
    echo "Usage: ./restore-loki.sh <backup_directory>"
    exit 1
fi

# Stop Loki
docker stop sportsbook-loki

# Clear existing data
rm -rf "$LOKI_DATA/chunks" "$LOKI_DATA/index"

# Restore data
tar -xzf "$BACKUP_DIR/loki-chunks.tar.gz" -C /
tar -xzf "$BACKUP_DIR/loki-index.tar.gz" -C /

# Restore configuration
cp "$BACKUP_DIR/loki-config.yaml" /path/to/loki-config.yaml

# Start Loki
docker start sportsbook-loki

echo "Recovery completed from: $BACKUP_DIR"
```

---

## LogQL Query Examples

### Basic Queries

```logql
# All logs from a service
{service_name="sportsbook-host"}

# Logs with specific level
{service_name="sportsbook-api", level="error"}

# Multiple services
{service_name=~"sportsbook-.*"}

# Time range (last hour)
{service_name="sportsbook-host"}[1h]
```

### Filtering and Parsing

```logql
# Filter by log content
{service_name="sportsbook-api"} |= "PlaceBet"

# Exclude certain messages
{service_name="sportsbook-host"} != "HealthCheck"

# Parse JSON logs
{service_name="sportsbook-api"} 
  | json 
  | UserId="12345"

# Extract fields with regex
{service_name="sportsbook-host"} 
  | regexp "BetId=(?P<bet_id>[a-f0-9-]+)"
  | bet_id != ""
```

### Business Metrics Queries

```logql
# Count bets placed per minute
sum(rate({service_name="sportsbook-api"} |= "Bet placed" [1m]))

# Average bet amount
avg_over_time({service_name="sportsbook-api"} 
  | json 
  | unwrap Amount [5m])

# Failed bet attempts
count_over_time({service_name="sportsbook-api"} 
  |= "Failed to place bet" [1h])

# Settlement processing time
histogram_quantile(0.95,
  sum(rate({service_name="sportsbook-host"} 
    |= "Bet settled"
    | json
    | unwrap DurationMs [5m])) by (le))
```

### Performance Analysis

```logql
# Slow operations (>1s)
{service_name=~"sportsbook-.*"} 
  | json 
  | DurationMs > 1000

# API response times by endpoint
avg by (endpoint) (
  {service_name="sportsbook-api"} 
  | json 
  | unwrap DurationMs
)

# Database query performance
{service_name="sportsbook-host"} 
  |= "SQL" 
  | json 
  | unwrap QueryTimeMs 
  | QueryTimeMs > 100
```

### Error Analysis

```logql
# All errors grouped by message
sum by (msg) (
  count_over_time({level="error"} [1h])
)

# Exception types
{level="error"} 
  | json 
  | line_format "{{.Exception}}"
  | regexp "(?P<exception_type>[A-Za-z]+Exception)"

# Error rate by service
sum by (service_name) (
  rate({level="error"} [5m])
)

# Correlation ID tracking
{CorrelationId="abc-123-def"} 
  | json
```

### Orleans-Specific Queries

```logql
# Grain activation failures
{service_name="sportsbook-host"} 
  |= "Grain activation failed"

# Cluster membership changes
{service_name="sportsbook-host"} 
  |= "ClusterMembership"

# Grain call duration
{service_name="sportsbook-host"} 
  | json 
  | GrainType="BetGrain" 
  | unwrap Duration

# Silo startup/shutdown
{service_name="sportsbook-host"} 
  |~ "(Starting|Stopping) Orleans"
```

### Alert Queries

```logql
# High error rate alert
rate({level="error"}[5m]) > 0.05

# No logs received alert
absent_over_time({service_name="sportsbook-api"}[5m])

# High memory usage logs
{service_name=~"sportsbook-.*"} 
  |= "OutOfMemory"

# Database connection failures
{service_name=~"sportsbook-.*"} 
  |= "database connection" 
  |= "failed"
```

---

## Troubleshooting Guide

### Common Issues and Solutions

#### Issue 1: Logs Not Appearing in Grafana

**Symptoms:**
- No logs visible in Grafana Explore
- Loki datasource shows as connected

**Diagnosis:**
```bash
# Check if logs are reaching Loki
curl -G -s "http://localhost:3100/loki/api/v1/query" \
  --data-urlencode 'query={service_name=~".*"}' | jq .

# Check Serilog configuration
docker exec sportsbook-api cat /app/appsettings.json | jq .Serilog

# Check network connectivity
docker exec sportsbook-api ping -c 1 loki
```

**Solutions:**
1. Verify Loki URL in Serilog configuration
2. Check firewall/network policies
3. Ensure labels match query filters
4. Verify time range in Grafana

#### Issue 2: High Memory Usage in Loki

**Symptoms:**
- Loki container using excessive memory
- OOM kills
- Slow query performance

**Diagnosis:**
```bash
# Check memory metrics
curl -s http://localhost:3100/metrics | grep -E "go_memstats_alloc_bytes|heap"

# Check chunk cache size
curl -s http://localhost:3100/metrics | grep chunk_cache
```

**Solutions:**
```yaml
# Adjust Loki configuration
chunk_store_config:
  chunk_cache_config:
    embedded_cache:
      enabled: true
      max_size_mb: 100  # Reduce cache size

limits_config:
  max_query_series: 500  # Limit query scope
  max_entries_limit_per_query: 1000  # Reduce result size
```

#### Issue 3: Duplicate Logs

**Symptoms:**
- Same log entry appears multiple times
- Increased storage usage

**Diagnosis:**
```logql
# Check for duplicates
{service_name="sportsbook-api"} 
  | json 
  | __timestamp__ = timestamp
```

**Solutions:**
1. Check for multiple Serilog sinks
2. Verify single logger initialization
3. Check for retry logic in Serilog sink
4. Set idempotency key in logs

#### Issue 4: Slow Query Performance

**Symptoms:**
- Queries timeout
- Grafana shows loading spinner indefinitely

**Diagnosis:**
```bash
# Check query performance
time curl -G -s "http://localhost:3100/loki/api/v1/query_range" \
  --data-urlencode 'query={service_name="sportsbook-api"}' \
  --data-urlencode 'limit=100'
```

**Solutions:**
1. Add more specific labels to narrow search
2. Reduce time range
3. Use `line_format` to reduce data transfer
4. Increase query parallelism in Loki config

#### Issue 5: Log Ingestion Delays

**Symptoms:**
- Logs appear in Grafana with delay
- Real-time monitoring not working

**Diagnosis:**
```csharp
// Add timestamp to logs
_logger.Information("Event at {EventTime} logged at {LogTime}", 
    eventTime, DateTimeOffset.UtcNow);
```

**Solutions:**
1. Reduce Serilog batch period
2. Check system time synchronization
3. Reduce Loki flush period
4. Monitor network latency

### Debugging Techniques

#### 1. Enable Debug Logging in Serilog

```csharp
.MinimumLevel.Debug()
.WriteTo.Console(
    restrictedToMinimumLevel: LogEventLevel.Debug)
```

#### 2. Enable Loki Debug Mode

```yaml
server:
  log_level: debug
```

#### 3. Trace HTTP Requests

```csharp
// Add HTTP logging
.WriteTo.GrafanaLoki(
    httpClient: new HttpClient(new LoggingHandler(new HttpClientHandler())))

public class LoggingHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Sending to Loki: {request.RequestUri}");
        var response = await base.SendAsync(request, cancellationToken);
        Console.WriteLine($"Loki response: {response.StatusCode}");
        return response;
    }
}
```

---

## Best Practices

### 1. Label Strategy

**DO:**
- Use static labels (service_name, environment, version)
- Keep cardinality low (<100 unique label combinations)
- Use consistent label names across services

**DON'T:**
- Use high-cardinality labels (user_id, request_id)
- Change labels frequently
- Use labels for data that belongs in log message

```csharp
// Good - Low cardinality labels
labels: new[]
{
    new LokiLabel { Key = "service_name", Value = "sportsbook-api" },
    new LokiLabel { Key = "environment", Value = "production" },
    new LokiLabel { Key = "region", Value = "us-east-1" }
}

// Bad - High cardinality
labels: new[]
{
    new LokiLabel { Key = "user_id", Value = userId }, // Don't do this!
    new LokiLabel { Key = "request_id", Value = requestId } // Don't do this!
}
```

### 2. Message Structure

```csharp
// Consistent structure for similar events
public static class LogTemplates
{
    public const string BetPlaced = "Bet placed: {BetId} by {UserId} on {MarketId} for {Amount:C}";
    public const string BetSettled = "Bet settled: {BetId} with outcome {Outcome} payout {Payout:C}";
    public const string BetFailed = "Bet failed: {BetId} reason {Reason}";
}

// Usage
_logger.Information(LogTemplates.BetPlaced, betId, userId, marketId, amount);
```

### 3. Contextual Logging

```csharp
public class GrainLoggingContext : IDisposable
{
    private readonly List<IDisposable> _contexts = new();
    
    public GrainLoggingContext(IGrain grain, string operation)
    {
        var grainId = grain.GetPrimaryKeyString();
        var grainType = grain.GetType().Name;
        
        _contexts.Add(LogContext.PushProperty("GrainId", grainId));
        _contexts.Add(LogContext.PushProperty("GrainType", grainType));
        _contexts.Add(LogContext.PushProperty("Operation", operation));
    }
    
    public void Dispose()
    {
        foreach (var context in _contexts)
            context?.Dispose();
    }
}

// Usage in grain
using (new GrainLoggingContext(this, nameof(PlaceBetAsync)))
{
    _logger.Information("Processing bet placement");
    // Method logic
}
```

### 4. Performance Considerations

```csharp
// Avoid expensive operations in log statements
// Bad
_logger.Debug("Data: {Data}", JsonConvert.SerializeObject(largeObject));

// Good - Only serialize if logging is enabled
if (_logger.IsEnabled(LogEventLevel.Debug))
{
    _logger.Debug("Data: {Data}", JsonConvert.SerializeObject(largeObject));
}

// Better - Use destructuring for objects
_logger.Debug("Data: {@Data}", largeObject);
```

### 5. Security Best Practices

```csharp
public static class LogSanitizer
{
    public static string SanitizeEmail(string email)
    {
        var parts = email.Split('@');
        if (parts.Length != 2) return "invalid";
        return $"{parts[0][0]}***@{parts[1]}";
    }
    
    public static string SanitizeToken(string token)
    {
        if (string.IsNullOrEmpty(token)) return "empty";
        return $"{token.Substring(0, 4)}...{token.Substring(token.Length - 4)}";
    }
    
    public static decimal MaskAmount(decimal amount)
    {
        return Math.Round(amount / 10) * 10; // Round to nearest 10
    }
}

// Usage
_logger.Information("User {Email} authenticated", 
    LogSanitizer.SanitizeEmail(email));
```

---

## Integration with Monitoring Stack

### Grafana Dashboard Configuration

#### 1. Add Loki Data Source

```json
{
  "name": "Loki",
  "type": "loki",
  "access": "proxy",
  "url": "http://loki:3100",
  "jsonData": {
    "maxLines": 1000,
    "derivedFields": [
      {
        "matcherRegex": "(?:traceID|trace_id)=(\\w+)",
        "name": "TraceID",
        "url": "http://tempo:16686/trace/${__value.raw}"
      }
    ]
  }
}
```

#### 2. Unified Dashboard with Metrics and Logs

```json
{
  "dashboard": {
    "title": "Sportsbook Unified Observability",
    "panels": [
      {
        "title": "API Request Rate",
        "targets": [
          {
            "expr": "rate(api_requests_total[5m])",
            "datasource": "Prometheus"
          }
        ]
      },
      {
        "title": "Recent Errors",
        "targets": [
          {
            "expr": "{level=\"error\"} |= \"\"",
            "datasource": "Loki"
          }
        ]
      },
      {
        "title": "Bet Processing",
        "targets": [
          {
            "expr": "rate(business_bets_placed_total[5m])",
            "datasource": "Prometheus"
          },
          {
            "expr": "{service_name=\"sportsbook-api\"} |= \"Bet placed\"",
            "datasource": "Loki"
          }
        ]
      }
    ]
  }
}
```

### Correlation with Metrics

```csharp
public class MetricsAndLoggingCorrelation
{
    private readonly ILogger _logger;
    private readonly IMetrics _metrics;
    
    public async Task<BetResult> PlaceBetWithObservability(PlaceBetRequest request)
    {
        using var activity = Activity.StartActivity("PlaceBet");
        var timer = _metrics.Measure.Timer.Time("bet.placement.duration");
        
        try
        {
            _logger.Information("Starting bet placement {BetId} with trace {TraceId}",
                request.BetId,
                activity?.TraceId);
                
            var result = await PlaceBetInternal(request);
            
            _metrics.Measure.Counter.Increment("bet.placement.success");
            _logger.Information("Bet placed successfully {BetId}", request.BetId);
            
            return result;
        }
        catch (Exception ex)
        {
            _metrics.Measure.Counter.Increment("bet.placement.failure");
            _logger.Error(ex, "Bet placement failed {BetId} with trace {TraceId}",
                request.BetId,
                activity?.TraceId);
            throw;
        }
        finally
        {
            timer.Dispose();
        }
    }
}
```

### Alert Rules for Logs

```yaml
# prometheus-alerts.yml
groups:
  - name: log_alerts
    rules:
      - alert: HighErrorRate
        expr: |
          sum(rate({level="error"}[5m])) > 0.05
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "High error rate detected"
          description: "Error rate is {{ $value }} errors per second"
      
      - alert: NoLogsReceived
        expr: |
          absent_over_time({service_name="sportsbook-api"}[10m])
        for: 5m
        labels:
          severity: critical
        annotations:
          summary: "No logs received from API"
          description: "No logs received from sportsbook-api for 10 minutes"
      
      - alert: DatabaseConnectionErrors
        expr: |
          sum(count_over_time({service_name=~"sportsbook-.*"} 
            |= "database connection failed"[5m])) > 5
        for: 2m
        labels:
          severity: critical
        annotations:
          summary: "Database connection failures"
          description: "{{ $value }} database connection failures in last 5 minutes"
```

---

## Performance Tuning

### Loki Configuration Optimization

```yaml
# Optimized loki-config.yaml for production

# Ingester settings
ingester:
  chunk_idle_period: 30m      # How long chunks sit idle before flushing
  chunk_retain_period: 15m    # How long to keep chunks in memory after flushing
  max_chunk_age: 2h          # Maximum chunk age before flushing
  chunk_target_size: 1536000  # Target chunk size in bytes (~1.5MB)
  max_transfer_retries: 0     # Disable transfers on shutdown for speed
  
  wal:
    enabled: true
    dir: /loki/wal
    replay_memory_ceiling: 2GB  # Limit WAL replay memory usage

# Querier settings
querier:
  max_concurrent: 20           # Maximum concurrent queries
  query_timeout: 5m           # Query timeout
  engine:
    max_look_back_period: 30d # Limit query range

# Query frontend settings
query_range:
  split_queries_by_interval: 30m  # Split long queries
  cache_results: true
  results_cache:
    cache:
      embedded_cache:
        enabled: true
        max_size_mb: 100
        ttl: 1h

# Storage settings
storage_config:
  boltdb_shipper:
    shared_store: s3
    cache_ttl: 24h
    index_gateway_client:
      server_address: index-gateway:9095
  
  aws:
    s3: s3://region/bucket-name
    s3forcepathstyle: false
    bucketnames: loki-chunks
    region: us-east-1
    
  cache_config:
    enable_fifocache: true
    fifocache:
      max_size_bytes: 1GB
      ttl: 24h

# Limits for production
limits_config:
  ingestion_rate_mb: 64
  ingestion_burst_size_mb: 128
  max_entries_limit_per_query: 10000
  max_streams_per_user: 10000
  max_global_streams_per_user: 10000
  max_query_length: 721h
  max_query_parallelism: 32
  cardinality_limit: 100000
```

### Serilog Performance Optimization

```csharp
// Optimized Serilog configuration
public static class OptimizedLogging
{
    public static LoggerConfiguration CreateOptimizedLogger(IConfiguration configuration)
    {
        return new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Orleans", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .WriteTo.Async(a => a.GrafanaLoki(
                uri: configuration["Loki:Uri"],
                batchPostingLimit: 1000,
                period: TimeSpan.FromSeconds(2),
                queueLimit: 100000,
                httpClient: CreateOptimizedHttpClient(),
                textFormatter: new CompactJsonFormatter()))
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "SportsbookLite")
            // Use memory-efficient enrichers
            .Enrich.WithProperty("Version", GetVersion())
            .Enrich.WithProperty("Environment", GetEnvironment());
    }
    
    private static HttpClient CreateOptimizedHttpClient()
    {
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(15),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(5),
            MaxConnectionsPerServer = 10,
            EnableMultipleHttp2Connections = true
        };
        
        return new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(30),
            DefaultRequestHeaders = 
            {
                { "Keep-Alive", "true" },
                { "Connection", "keep-alive" }
            }
        };
    }
}
```

### Query Performance Tips

```logql
# Optimize queries by:

# 1. Use specific labels to reduce data scanned
{service_name="sportsbook-api", environment="production"}

# 2. Use time ranges to limit data
{service_name="sportsbook-api"}[1h]

# 3. Use line filters early in pipeline
{service_name="sportsbook-api"} |= "error" |= "database"

# 4. Avoid regex when possible (use |= instead)
# Bad
{service_name="sportsbook-api"} |~ ".*error.*"
# Good
{service_name="sportsbook-api"} |= "error"

# 5. Limit results
{service_name="sportsbook-api"} | limit 100

# 6. Use unwrap sparingly
# Only unwrap when aggregating
sum_over_time({service_name="sportsbook-api"} 
  | json 
  | unwrap duration [5m])
```

---

## Security Considerations

### 1. Authentication and Authorization

```yaml
# loki-config.yaml with authentication
auth_enabled: true

server:
  http_listen_port: 3100
  grpc_listen_port: 9096
  http_server_read_timeout: 600s
  http_server_write_timeout: 600s

# Multi-tenancy configuration
limits_config:
  max_global_streams_per_user: 5000
  ingestion_rate_mb: 10
  ingestion_burst_size_mb: 20
  per_stream_rate_limit: 512KB
  per_stream_rate_limit_burst: 1MB

# Rate limiting per tenant
distributor:
  ring:
    kvstore:
      store: consul
      prefix: collectors/
```

### 2. TLS Configuration

```csharp
// Secure Loki connection
public static IHostBuilder CreateHostBuilder(string[] args) =>
    Host.CreateDefaultBuilder(args)
        .UseSerilog((context, services, configuration) => configuration
            .WriteTo.GrafanaLoki(
                uri: "https://loki.sportsbook.com:3100",
                httpClient: CreateSecureHttpClient(),
                credentials: new LokiCredentials
                {
                    Username = context.Configuration["Loki:Username"],
                    Password = context.Configuration["Loki:Password"]
                }));

private static HttpClient CreateSecureHttpClient()
{
    var handler = new HttpClientHandler
    {
        ClientCertificates = { GetClientCertificate() },
        ServerCertificateCustomValidationCallback = ValidateServerCertificate
    };
    
    return new HttpClient(handler);
}
```

### 3. Log Sanitization

```csharp
public class SanitizingFormatter : ITextFormatter
{
    private readonly ITextFormatter _innerFormatter;
    private readonly HashSet<string> _sensitiveProperties = new()
    {
        "Password", "Token", "ApiKey", "Secret", "CreditCard", "SSN"
    };
    
    public void Format(LogEvent logEvent, TextWriter output)
    {
        var sanitized = new LogEvent(
            logEvent.Timestamp,
            logEvent.Level,
            logEvent.Exception,
            logEvent.MessageTemplate,
            SanitizeProperties(logEvent.Properties));
            
        _innerFormatter.Format(sanitized, output);
    }
    
    private IEnumerable<LogEventProperty> SanitizeProperties(
        IReadOnlyDictionary<string, LogEventPropertyValue> properties)
    {
        foreach (var property in properties)
        {
            if (_sensitiveProperties.Contains(property.Key))
            {
                yield return new LogEventProperty(property.Key, 
                    new ScalarValue("***REDACTED***"));
            }
            else
            {
                yield return new LogEventProperty(property.Key, property.Value);
            }
        }
    }
}
```

### 4. Access Control

```yaml
# Kubernetes NetworkPolicy for Loki
apiVersion: networking.k8s.io/v1
kind: NetworkPolicy
metadata:
  name: loki-network-policy
  namespace: monitoring
spec:
  podSelector:
    matchLabels:
      app: loki
  policyTypes:
  - Ingress
  - Egress
  ingress:
  - from:
    - namespaceSelector:
        matchLabels:
          name: sportsbook
    - namespaceSelector:
        matchLabels:
          name: monitoring
    ports:
    - protocol: TCP
      port: 3100
  egress:
  - to:
    - namespaceSelector:
        matchLabels:
          name: storage
    ports:
    - protocol: TCP
      port: 443  # S3 API
```

---

## Maintenance Procedures

### Daily Maintenance Tasks

```bash
#!/bin/bash
# daily-loki-maintenance.sh

echo "Starting daily Loki maintenance - $(date)"

# 1. Check service health
echo "Checking Loki health..."
curl -s http://localhost:3100/ready || echo "WARNING: Loki not ready"

# 2. Check disk usage
echo "Checking disk usage..."
DISK_USAGE=$(df -h /var/lib/loki | awk 'NR==2 {print $5}' | sed 's/%//')
if [ "$DISK_USAGE" -gt 80 ]; then
    echo "WARNING: Disk usage is ${DISK_USAGE}%"
fi

# 3. Check ingestion rate
echo "Checking ingestion rate..."
curl -s http://localhost:3100/metrics | grep -E "loki_distributor_bytes_received_total"

# 4. Compact old chunks
echo "Triggering compaction..."
curl -X POST http://localhost:3100/loki/api/v1/compact

# 5. Check for errors in logs
echo "Checking for errors..."
docker logs sportsbook-loki --since 24h 2>&1 | grep -c ERROR || echo "No errors found"

echo "Daily maintenance completed - $(date)"
```

### Weekly Maintenance Tasks

```bash
#!/bin/bash
# weekly-loki-maintenance.sh

echo "Starting weekly Loki maintenance - $(date)"

# 1. Backup configuration
echo "Backing up configuration..."
cp /etc/loki/loki-config.yaml /backup/loki/config-$(date +%Y%m%d).yaml

# 2. Analyze query performance
echo "Analyzing slow queries..."
curl -s http://localhost:3100/metrics | grep -E "query_duration_seconds" | \
  awk '{print $2}' | sort -rn | head -10

# 3. Review and optimize index
echo "Optimizing index..."
find /var/lib/loki/index -name "*.db" -mtime +7 -exec \
  sqlite3 {} "VACUUM;" \;

# 4. Generate usage report
echo "Generating usage report..."
cat <<EOF > /reports/loki-weekly-$(date +%Y%m%d).txt
Loki Weekly Report - $(date)
=====================================
Total Logs Ingested: $(curl -s http://localhost:3100/metrics | grep -E "loki_ingester_chunks_created_total" | awk '{print $2}')
Active Streams: $(curl -s http://localhost:3100/metrics | grep -E "loki_ingester_streams_created_total" | awk '{print $2}')
Query Rate: $(curl -s http://localhost:3100/metrics | grep -E "loki_request_duration_seconds_count" | awk '{print $2}')
Storage Used: $(du -sh /var/lib/loki)
EOF

echo "Weekly maintenance completed - $(date)"
```

### Monthly Maintenance Tasks

```bash
#!/bin/bash
# monthly-loki-maintenance.sh

echo "Starting monthly Loki maintenance - $(date)"

# 1. Review and update retention policies
echo "Reviewing retention policies..."
grep retention /etc/loki/loki-config.yaml

# 2. Performance baseline
echo "Creating performance baseline..."
curl -s http://localhost:3100/metrics > /metrics/loki-baseline-$(date +%Y%m).txt

# 3. Security audit
echo "Running security audit..."
# Check for exposed ports
netstat -tulpn | grep loki
# Check for unauthorized access attempts
grep -E "401|403" /var/log/loki/access.log | wc -l

# 4. Capacity planning
echo "Capacity planning analysis..."
cat <<EOF
Current Growth Rate: $(calculate_growth_rate)
Projected Storage (3 months): $(calculate_projection)
Recommended Action: $(recommend_action)
EOF

echo "Monthly maintenance completed - $(date)"
```

### Upgrade Procedures

```bash
#!/bin/bash
# upgrade-loki.sh

NEW_VERSION="2.9.4"
BACKUP_DIR="/backup/loki/upgrade-$(date +%Y%m%d)"

echo "Starting Loki upgrade to version $NEW_VERSION"

# 1. Create backup
echo "Creating backup..."
mkdir -p "$BACKUP_DIR"
docker exec sportsbook-loki tar -czf - /loki > "$BACKUP_DIR/loki-data.tar.gz"
cp docker-compose.yml "$BACKUP_DIR/"

# 2. Pull new image
echo "Pulling new image..."
docker pull grafana/loki:$NEW_VERSION

# 3. Stop current instance
echo "Stopping current Loki..."
docker-compose stop loki

# 4. Update docker-compose.yml
echo "Updating configuration..."
sed -i "s|grafana/loki:.*|grafana/loki:$NEW_VERSION|" docker-compose.yml

# 5. Start new version
echo "Starting new version..."
docker-compose up -d loki

# 6. Verify upgrade
echo "Verifying upgrade..."
sleep 30
curl -s http://localhost:3100/ready || {
    echo "ERROR: Loki not ready, rolling back..."
    docker-compose down loki
    cp "$BACKUP_DIR/docker-compose.yml" .
    docker-compose up -d loki
    exit 1
}

echo "Upgrade completed successfully"
```

---

## FAQ

### General Questions

**Q: How much storage does Loki require?**
A: Storage requirements depend on log volume and retention. Rough estimate:
- 1GB/day of raw logs ≈ 100-200MB compressed storage
- 30-day retention ≈ 3-6GB per GB/day of logs
- Indexes add ~10% overhead

**Q: Can Loki replace our existing ELK stack?**
A: Yes, for log aggregation and analysis. Key differences:
- Loki: Label-indexed, cost-effective, Grafana-native
- ELK: Full-text search, more complex queries, higher resource usage

**Q: What's the maximum retention period?**
A: Technically unlimited, practically limited by:
- Storage costs
- Query performance (older data = slower queries)
- Compliance requirements
- Recommended: 30-90 days hot, 1 year cold storage

### Configuration Questions

**Q: How do I change log level at runtime?**
A: Three options:
1. Use LogLevelSwitch in Serilog
2. Implement configuration reload endpoint
3. Use environment variables with container restart

**Q: Can I use multiple Loki instances?**
A: Yes, for high availability:
```yaml
WriteTo:
  - Name: GrafanaLoki
    Args:
      uri: "http://loki-primary:3100"
  - Name: GrafanaLoki
    Args:
      uri: "http://loki-secondary:3100"
```

**Q: How do I filter sensitive data?**
A: Use custom formatters or enrichers to redact sensitive fields before sending to Loki.

### Performance Questions

**Q: Why are my queries slow?**
A: Common causes:
1. Too broad label selectors
2. Large time ranges
3. Complex regex patterns
4. Insufficient resources
5. Missing indexes

**Q: How can I reduce Loki memory usage?**
A: 
1. Reduce chunk_idle_period
2. Lower max_look_back_period
3. Decrease cache sizes
4. Use streaming queries
5. Implement retention policies

**Q: What's the maximum ingestion rate?**
A: Depends on resources, typically:
- Single instance: 50-100 MB/s
- Distributed: 1+ GB/s
- Factors: CPU, network, storage IOPS

### Troubleshooting Questions

**Q: Logs are missing - where do I check?**
A: Debugging checklist:
1. Check Serilog errors in application logs
2. Verify network connectivity to Loki
3. Check Loki ingestion metrics
4. Review rate limiting settings
5. Verify timestamp alignment

**Q: How do I recover from corruption?**
A: Recovery steps:
1. Stop Loki
2. Run chunk validation: `loki -verify-chunks`
3. Remove corrupted chunks
4. Restore from backup if needed
5. Restart with WAL replay

**Q: Can I delete specific logs?**
A: No, Loki doesn't support selective deletion. Options:
1. Wait for retention to remove old logs
2. Use recording rules to exclude sensitive data
3. Implement pre-ingestion filtering

---

## References and Resources

### Official Documentation
- [Grafana Loki Documentation](https://grafana.com/docs/loki/latest/)
- [LogQL Query Language](https://grafana.com/docs/loki/latest/logql/)
- [Loki Best Practices](https://grafana.com/docs/loki/latest/best-practices/)
- [Serilog.Sinks.Grafana.Loki](https://github.com/serilog-contrib/serilog-sinks-grafana-loki)

### Related Project Documentation
- [Monitoring Architecture Plan](./monitoring-architecture-plan.md)
- [Metrics Implementation Guide](./metrics-implementation-csharp.md)
- [Monitoring Infrastructure DevOps](./monitoring-infrastructure-devops.md)
- [Complete Monitoring Guide](./monitoring-complete-guide.md)

### Community Resources
- [Grafana Community Forums](https://community.grafana.com/c/grafana-loki/)
- [Loki Slack Channel](https://slack.grafana.com/)
- [GitHub Issues](https://github.com/grafana/loki/issues)

### Tools and Utilities
- [LogCLI](https://grafana.com/docs/loki/latest/tools/logcli/) - Command-line client
- [Loki Canary](https://grafana.com/docs/loki/latest/tools/canary/) - Monitoring tool
- [Promtail](https://grafana.com/docs/loki/latest/clients/promtail/) - Log collection agent
- [Grafana Agent](https://grafana.com/docs/agent/latest/) - Unified telemetry collector

### Example Configurations
- [Production Config Examples](https://github.com/grafana/loki/tree/main/production)
- [Kubernetes Manifests](https://github.com/grafana/loki/tree/main/production/kubernetes)
- [Docker Compose Examples](https://github.com/grafana/loki/tree/main/examples/docker-compose)

### Performance Tuning Guides
- [Loki Performance Tuning](https://grafana.com/blog/2021/02/16/the-essential-guide-to-loki-performance-tuning/)
- [Query Optimization](https://grafana.com/docs/loki/latest/operations/query-optimization/)
- [Storage Recommendations](https://grafana.com/docs/loki/latest/operations/storage/)

---

## Appendix: Complete Implementation Checklist

### Phase 1: Setup ✅
- [ ] Install Serilog.Sinks.Grafana.Loki NuGet package
- [ ] Configure Serilog in Host project
- [ ] Configure Serilog in API project
- [ ] Add Loki to docker-compose.yml
- [ ] Create loki-config.yaml
- [ ] Test local development setup

### Phase 2: Integration ✅
- [ ] Add Loki datasource to Grafana
- [ ] Create basic log dashboard
- [ ] Implement correlation IDs
- [ ] Add structured logging to grains
- [ ] Configure log enrichment
- [ ] Test log aggregation

### Phase 3: Production Ready ✅
- [ ] Configure Kubernetes deployment
- [ ] Set up persistent storage
- [ ] Implement retention policies
- [ ] Configure backup procedures
- [ ] Add security (TLS, authentication)
- [ ] Create runbook documentation

### Phase 4: Optimization ✅
- [ ] Tune ingestion performance
- [ ] Optimize query performance
- [ ] Implement caching strategies
- [ ] Add monitoring for Loki itself
- [ ] Create alert rules
- [ ] Performance test under load

### Phase 5: Operations ✅
- [ ] Train team on LogQL
- [ ] Create query library
- [ ] Document troubleshooting procedures
- [ ] Establish maintenance schedule
- [ ] Plan capacity for growth
- [ ] Regular backup verification

---

*Document Version: 1.0*  
*Last Updated: January 2025*  
*Author: Sportsbook-Lite Technical Documentation Team*  
*Review Cycle: Monthly*

---

## Contact and Support

For questions or issues related to Loki logging implementation:

1. **Internal Team**: Create issue in project repository
2. **Slack Channel**: #sportsbook-monitoring
3. **On-Call**: Check PagerDuty for current on-call engineer
4. **Documentation Updates**: Submit PR to `/docs/agent-work/`

---

**END OF DOCUMENT**