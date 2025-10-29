# Grafana Loki Logging Architecture for SportsbookLite

## Executive Summary

This document presents a comprehensive logging architecture for integrating Grafana Loki into the SportsbookLite distributed system. The architecture addresses the unique challenges of logging in a microservices environment with Microsoft Orleans virtual actors, Apache Pulsar event streaming, and high-volume betting operations while ensuring observability, security, and performance.

## Current State Analysis

### Existing Infrastructure Assessment
- **Framework**: Serilog configured in both Orleans Host and FastEndpoints API
- **Current Sinks**: Console-only logging with structured output
- **Monitoring**: Prometheus metrics collection with 30+ business and system metrics
- **Architecture**: Distributed Orleans grains with event-driven Pulsar integration
- **Deployment**: Docker Compose for development, Kubernetes for production
- **Database**: PostgreSQL with Redis clustering for Orleans

### Identified Gaps
1. No centralized log aggregation
2. Limited log correlation between distributed components
3. No log retention or archival strategy
4. Missing structured logging for business events
5. No security controls for sensitive log data
6. No performance monitoring of logging pipeline

## Loki Integration Architecture

### 1. Log Aggregation Strategy for Distributed Orleans Grains

#### 1.1 Grain Lifecycle Logging
```csharp
public abstract class BaseGrain : Grain
{
    protected ILogger Logger { get; private set; }
    
    public override Task OnActivateAsync()
    {
        Logger = ServiceProvider.GetService<ILogger<BaseGrain>>();
        
        using (Logger.BeginScope(new Dictionary<string, object>
        {
            ["GrainId"] = this.GetPrimaryKey().ToString(),
            ["GrainType"] = this.GetType().Name,
            ["SiloAddress"] = Silo.CurrentSilo.SiloAddress.ToString(),
            ["ClusterId"] = Silo.CurrentSilo.ClusterOptions.ClusterId,
            ["ActivationId"] = RuntimeIdentity
        }))
        {
            Logger.LogInformation("Grain activated: {GrainType} {GrainId}", 
                this.GetType().Name, this.GetPrimaryKey());
        }
        
        return base.OnActivateAsync();
    }
    
    public override Task OnDeactivateAsync()
    {
        Logger.LogInformation("Grain deactivated: {GrainType} {GrainId} after {LifetimeMs}ms", 
            this.GetType().Name, this.GetPrimaryKey(), 
            (DateTimeOffset.UtcNow - ActivatedAt).TotalMilliseconds);
            
        return base.OnDeactivateAsync();
    }
}
```

#### 1.2 Grain Communication Logging
```csharp
public class GrainCallInstrumentationFilter : IIncomingGrainCallFilter
{
    private readonly ILogger<GrainCallInstrumentationFilter> _logger;

    public async Task Invoke(IIncomingGrainCallContext context)
    {
        var correlationId = Guid.NewGuid().ToString();
        
        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["GrainMethod"] = context.InterfaceMethod.Name,
            ["GrainType"] = context.Grain.GetType().Name,
            ["GrainId"] = context.Grain.GetPrimaryKey().ToString(),
            ["CallerId"] = RequestContext.Get("CallerId"),
            ["RequestId"] = RequestContext.Get("RequestId")
        }))
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                _logger.LogDebug("Grain call started: {GrainMethod}", context.InterfaceMethod.Name);
                await context.Invoke();
                
                stopwatch.Stop();
                _logger.LogInformation("Grain call completed: {GrainMethod} in {ElapsedMs}ms", 
                    context.InterfaceMethod.Name, stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Grain call failed: {GrainMethod} after {ElapsedMs}ms", 
                    context.InterfaceMethod.Name, stopwatch.ElapsedMilliseconds);
                throw;
            }
        }
    }
}
```

#### 1.3 Business Domain Logging Structure
```csharp
public static class SportsbookLogEvents
{
    // Betting Events (1000-1999)
    public static readonly EventId BetPlaced = new(1001, "BetPlaced");
    public static readonly EventId BetValidated = new(1002, "BetValidated");
    public static readonly EventId BetSettled = new(1003, "BetSettled");
    public static readonly EventId BetCancelled = new(1004, "BetCancelled");
    
    // Odds Events (2000-2999)
    public static readonly EventId OddsUpdated = new(2001, "OddsUpdated");
    public static readonly EventId MarketSuspended = new(2002, "MarketSuspended");
    public static readonly EventId MarketReopened = new(2003, "MarketReopened");
    
    // Wallet Events (3000-3999)
    public static readonly EventId FundsReserved = new(3001, "FundsReserved");
    public static readonly EventId FundsReleased = new(3002, "FundsReleased");
    public static readonly EventId PayoutProcessed = new(3003, "PayoutProcessed");
    
    // Risk Management Events (4000-4999)
    public static readonly EventId RiskLimitExceeded = new(4001, "RiskLimitExceeded");
    public static readonly EventId SuspiciousActivity = new(4002, "SuspiciousActivity");
}
```

### 2. Correlation Between Orleans Telemetry and Application Logs

#### 2.1 Correlation ID Strategy
```csharp
public class CorrelationMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault() 
            ?? Guid.NewGuid().ToString();
            
        context.Items["CorrelationId"] = correlationId;
        context.Response.Headers.Add("X-Correlation-ID", correlationId);
        
        // Set Orleans RequestContext
        RequestContext.Set("CorrelationId", correlationId);
        RequestContext.Set("RequestId", context.TraceIdentifier);
        RequestContext.Set("UserId", context.User?.Identity?.Name);
        
        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (LogContext.PushProperty("RequestId", context.TraceIdentifier))
        {
            await next(context);
        }
    }
}
```

#### 2.2 OpenTelemetry Integration
```csharp
// In Program.cs
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation()
            .AddOrleansInstrumentation()  // Future Orleans integration
            .AddNpgsqlInstrumentation()
            .SetSampler(new TraceIdRatioBasedSampler(1.0));
    })
    .WithMetrics(metrics =>
    {
        metrics
            .AddAspNetCoreInstrumentation()
            .AddRuntimeInstrumentation();
    });
```

#### 2.3 Structured Logging Enrichers
```csharp
public class OrleansEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        if (RuntimeContext.Current != null)
        {
            logEvent.AddProperty(propertyFactory.CreateProperty("GrainId", 
                RuntimeContext.Current.GrainId));
            logEvent.AddProperty(propertyFactory.CreateProperty("ActivationId", 
                RuntimeContext.Current.ActivationId));
            logEvent.AddProperty(propertyFactory.CreateProperty("SiloAddress", 
                RuntimeContext.Current.SiloAddress));
        }
        
        if (RequestContext.Get("CorrelationId") is string correlationId)
        {
            logEvent.AddProperty(propertyFactory.CreateProperty("CorrelationId", correlationId));
        }
        
        if (RequestContext.Get("UserId") is string userId)
        {
            logEvent.AddProperty(propertyFactory.CreateProperty("UserId", userId));
        }
    }
}
```

### 3. Log Retention and Storage Policies

#### 3.1 Environment-Specific Retention
```yaml
# Loki Configuration
retention_deletes_enabled: true
retention_period: 
  development: 7d      # 1 week for dev
  staging: 30d         # 1 month for staging  
  production: 90d      # 3 months for production

compaction:
  enabled: true
  compaction_interval: 10m
  retention_enabled: true
  delete_request_cancel_period: 24h

chunk_store_config:
  max_look_back_period: 0s  # Unlimited for retention period

table_manager:
  retention_deletes_enabled: true
  retention_period: 90d  # Production default
```

#### 3.2 Storage Backend Configuration
```yaml
# Development (Local)
schema_config:
  configs:
    - from: 2020-10-24
      store: boltdb-shipper
      object_store: filesystem
      schema: v11

storage_config:
  boltdb_shipper:
    active_index_directory: /loki/boltdb-shipper-active
    cache_location: /loki/boltdb-shipper-cache
    shared_store: filesystem
  filesystem:
    directory: /loki/chunks

# Production (S3/Azure Blob)
storage_config:
  aws:
    s3: s3://loki-logs-production/chunks
    s3forcepathstyle: false
    bucketnames: loki-logs-production
    region: us-east-1
    access_key_id: ${AWS_ACCESS_KEY_ID}
    secret_access_key: ${AWS_SECRET_ACCESS_KEY}
```

#### 3.3 Log Level-Based Retention
```yaml
limits_config:
  per_stream_rate_limit: 5MB
  per_stream_rate_limit_burst: 20MB
  ingestion_rate_mb: 10
  ingestion_burst_size_mb: 20
  
  # Different retention for different log levels
  retention_stream:
    - selector: '{level="error"}'
      priority: 1
      period: 1y    # Keep errors for 1 year
    - selector: '{level="warning"}'  
      priority: 2
      period: 180d  # Keep warnings for 6 months
    - selector: '{level="info"}'
      priority: 3  
      period: 90d   # Keep info for 3 months
    - selector: '{level="debug"}'
      priority: 4
      period: 7d    # Keep debug for 1 week
```

### 4. Scaling Strategy for High-Volume Betting Events

#### 4.1 Loki Scaling Architecture
```yaml
# Loki Production Deployment
apiVersion: apps/v1
kind: StatefulSet
metadata:
  name: loki
spec:
  replicas: 3
  template:
    spec:
      containers:
      - name: loki
        image: grafana/loki:2.9.0
        args:
        - -config.file=/etc/loki/config.yaml
        - -target=all
        - -server.http-listen-port=3100
        - -server.grpc-listen-port=9095
        resources:
          requests:
            memory: "2Gi"
            cpu: "1000m"
          limits:
            memory: "4Gi"
            cpu: "2000m"
        volumeMounts:
        - name: config
          mountPath: /etc/loki
        - name: storage
          mountPath: /loki
        env:
        - name: JAEGER_AGENT_HOST
          value: "jaeger-agent"
```

#### 4.2 Promtail High-Volume Configuration
```yaml
# Promtail for high-volume log shipping
server:
  http_listen_port: 9080
  grpc_listen_port: 0

positions:
  filename: /tmp/positions.yaml

clients:
  - url: http://loki-gateway:3100/loki/api/v1/push
    batchwait: 200ms      # Reduced for faster shipping
    batchsize: 1048576    # 1MB batches
    max_retries: 10
    backoff_config:
      min_period: 500ms
      max_period: 5m
      max_retries: 10

scrape_configs:
  - job_name: orleans-silo
    static_configs:
      - targets:
          - localhost
        labels:
          job: orleans-silo
          service_name: sportsbook-orleans
          environment: production
          __path__: /app/logs/orleans-*.log
    
    pipeline_stages:
      - regex:
          expression: '(?P<timestamp>\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3}) \[(?P<level>\w+)\] (?P<logger>[\w\.]+): (?P<message>.*)'
      - timestamp:
          source: timestamp
          format: '2006-01-02 15:04:05.000'
      - labels:
          level:
          logger:
      - output:
          source: message
```

#### 4.3 Load Balancing and Sharding
```yaml
# Loki Gateway for load balancing
apiVersion: apps/v1
kind: Deployment
metadata:
  name: loki-gateway
spec:
  replicas: 2
  template:
    spec:
      containers:
      - name: nginx
        image: nginx:1.25
        ports:
        - containerPort: 80
        volumeMounts:
        - name: config
          mountPath: /etc/nginx/nginx.conf
          subPath: nginx.conf
---
apiVersion: v1
kind: ConfigMap
metadata:
  name: loki-gateway-config
data:
  nginx.conf: |
    upstream loki {
        hash $request_uri consistent;
        server loki-0.loki:3100;
        server loki-1.loki:3100;
        server loki-2.loki:3100;
    }
    
    server {
        listen 80;
        location / {
            proxy_pass http://loki;
            proxy_set_header X-Scope-OrgID tenant1;
        }
    }
```

### 5. Security Considerations for Sensitive User/Betting Data

#### 5.1 Data Sanitization and Masking
```csharp
public class SensitiveDataFormatter : ITextFormatter
{
    private static readonly Regex EmailRegex = new(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b");
    private static readonly Regex CreditCardRegex = new(@"\b(?:\d{4}[-\s]?){3}\d{4}\b");
    private static readonly Regex UserIdRegex = new(@"""UserId"":\s*""([^""]+)""");
    
    public void Format(LogEvent logEvent, TextWriter output)
    {
        var message = logEvent.RenderMessage();
        
        // Mask sensitive information
        message = EmailRegex.Replace(message, "***@***.***");
        message = CreditCardRegex.Replace(message, "****-****-****-****");
        message = UserIdRegex.Replace(message, @"""UserId"": ""***MASKED***""");
        
        // Mask betting amounts above threshold
        if (logEvent.Properties.ContainsKey("BetAmount") && 
            decimal.TryParse(logEvent.Properties["BetAmount"].ToString(), out var amount))
        {
            if (amount > 10000) // Mask high-value bets
            {
                logEvent.Properties["BetAmount"] = new ScalarValue("***HIGH_VALUE***");
            }
        }
        
        output.WriteLine(JsonConvert.SerializeObject(new
        {
            Timestamp = logEvent.Timestamp,
            Level = logEvent.Level.ToString(),
            Message = message,
            Properties = logEvent.Properties.Where(p => !IsSensitiveProperty(p.Key))
        }));
    }
    
    private bool IsSensitiveProperty(string propertyName) =>
        propertyName.Contains("Password", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Contains("Token", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Contains("Secret", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Contains("Key", StringComparison.OrdinalIgnoreCase);
}
```

#### 5.2 Access Control and Authentication
```yaml
# Loki authentication configuration
auth_enabled: true

common:
  instance_addr: 127.0.0.1
  path_prefix: /loki
  storage:
    filesystem:
      chunks_directory: /loki/chunks
      rules_directory: /loki/rules
  replication_factor: 1

server:
  http_listen_port: 3100
  http_server_read_timeout: 30s
  http_server_write_timeout: 30s

# Multi-tenant configuration
limits_config:
  enforce_metric_name: false
  reject_old_samples: true
  reject_old_samples_max_age: 168h
  per_tenant_override_config: /etc/loki/tenant-overrides.yaml

# Tenant overrides
tenant_overrides:
  development:
    ingestion_rate_mb: 50
    ingestion_burst_size_mb: 100
  production:
    ingestion_rate_mb: 20
    ingestion_burst_size_mb: 50
```

#### 5.3 Network Security
```yaml
# Network policies for Loki
apiVersion: networking.k8s.io/v1
kind: NetworkPolicy
metadata:
  name: loki-network-policy
spec:
  podSelector:
    matchLabels:
      app: loki
  policyTypes:
  - Ingress
  - Egress
  ingress:
  - from:
    - podSelector:
        matchLabels:
          app: promtail
    - podSelector:
        matchLabels:
          app: grafana
    ports:
    - protocol: TCP
      port: 3100
  egress:
  - to:
    - podSelector:
        matchLabels:
          app: postgres  # For index storage
    ports:
    - protocol: TCP
      port: 5432
```

### 6. Performance Impact Mitigation Strategies

#### 6.1 Asynchronous Logging Configuration
```csharp
// High-performance Serilog configuration
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.With<OrleansEnricher>()
    .WriteTo.Async(a => a.Console(new SensitiveDataFormatter()),
        bufferSize: 10000,
        blockWhenFull: false)
    .WriteTo.Async(a => a.GrafanaLoki(
        "http://loki-gateway:3100",
        labels: new[]
        {
            new LokiLabel { Key = "service_name", Value = "sportsbook-host" },
            new LokiLabel { Key = "environment", Value = Environment.GetEnvironmentVariable("ENVIRONMENT") },
            new LokiLabel { Key = "version", Value = Assembly.GetExecutingAssembly().GetName().Version.ToString() },
            new LokiLabel { Key = "hostname", Value = Environment.MachineName },
            new LokiLabel { Key = "orleans_cluster_id", Value = "{{orleans_cluster_id}}" },
            new LokiLabel { Key = "orleans_service_id", Value = "{{orleans_service_id}}" }
        },
        textFormatter: new SensitiveDataFormatter(),
        batchPostingLimit: 1000,
        queueLimit: 100000,
        period: TimeSpan.FromSeconds(2),
        createLevelLabel: true,
        httpClient: new HttpClient { Timeout = TimeSpan.FromMinutes(1) }),
        bufferSize: 50000,
        blockWhenFull: false)
    .CreateLogger();
```

#### 6.2 Circuit Breaker for Logging
```csharp
public class LokiSinkWithCircuitBreaker : ILogEventSink
{
    private readonly ILogEventSink _lokiSink;
    private readonly ICircuitBreaker _circuitBreaker;
    private readonly ILogEventSink _fallbackSink;
    
    public LokiSinkWithCircuitBreaker(ILogEventSink lokiSink, ILogEventSink fallbackSink)
    {
        _lokiSink = lokiSink;
        _fallbackSink = fallbackSink;
        _circuitBreaker = new CircuitBreaker(
            exceptionsAllowedBeforeBreaking: 5,
            durationOfBreak: TimeSpan.FromMinutes(1));
    }
    
    public void Emit(LogEvent logEvent)
    {
        try
        {
            _circuitBreaker.Execute(() => _lokiSink.Emit(logEvent));
        }
        catch (CircuitBreakerOpenException)
        {
            // Circuit is open, use fallback
            _fallbackSink.Emit(logEvent);
        }
        catch (Exception ex)
        {
            // Log to fallback and attempt to break circuit
            _fallbackSink.Emit(logEvent);
            _fallbackSink.Emit(new LogEvent(
                DateTimeOffset.UtcNow,
                LogEventLevel.Warning,
                null,
                MessageTemplate.Empty,
                new[] { new LogEventProperty("LokiError", new ScalarValue(ex.Message)) }));
        }
    }
}
```

#### 6.3 Log Sampling for High-Volume Events
```csharp
public class AdaptiveSamplingEnricher : ILogEventEnricher
{
    private static readonly ConcurrentDictionary<string, SamplingState> _samplingStates = new();
    
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var eventType = logEvent.Properties.ContainsKey("EventType") 
            ? logEvent.Properties["EventType"].ToString() 
            : "Unknown";
            
        var samplingState = _samplingStates.GetOrAdd(eventType, _ => new SamplingState());
        
        if (!samplingState.ShouldLog())
        {
            // Add property to skip this log
            logEvent.AddProperty(propertyFactory.CreateProperty("SampledOut", true));
        }
        else
        {
            logEvent.AddProperty(propertyFactory.CreateProperty("SampleRate", samplingState.CurrentRate));
        }
    }
}

public class SamplingState
{
    private long _counter;
    private DateTime _lastRateAdjustment = DateTime.UtcNow;
    private int _currentRate = 1; // Log every event initially
    
    public int CurrentRate => _currentRate;
    
    public bool ShouldLog()
    {
        var count = Interlocked.Increment(ref _counter);
        
        // Adjust sampling rate every minute based on volume
        if (DateTime.UtcNow - _lastRateAdjustment > TimeSpan.FromMinutes(1))
        {
            AdjustSamplingRate(count);
            Interlocked.Exchange(ref _counter, 0);
            _lastRateAdjustment = DateTime.UtcNow;
        }
        
        return count % _currentRate == 0;
    }
    
    private void AdjustSamplingRate(long eventsPerMinute)
    {
        _currentRate = eventsPerMinute switch
        {
            > 10000 => 100,  // Sample 1 in 100 for very high volume
            > 5000 => 50,    // Sample 1 in 50 for high volume  
            > 1000 => 10,    // Sample 1 in 10 for medium volume
            > 100 => 5,      // Sample 1 in 5 for low-medium volume
            _ => 1           // Log everything for low volume
        };
    }
}
```

## Multi-Environment Deployment Strategy

### 7. Development Environment

#### 7.1 Docker Compose Integration
```yaml
# docker/docker-compose.yml - Add Loki services
services:
  # ... existing services ...
  
  loki:
    image: grafana/loki:2.9.0
    container_name: sportsbook-loki
    restart: unless-stopped
    command: -config.file=/etc/loki/local-config.yaml
    ports:
      - "3100:3100"
    volumes:
      - loki_data:/loki
      - ./loki/local-config.yaml:/etc/loki/local-config.yaml:ro
    networks:
      - sportsbook-network
    healthcheck:
      test: ["CMD-SHELL", "wget --no-verbose --tries=1 --spider http://localhost:3100/ready || exit 1"]
      interval: 30s
      timeout: 10s
      retries: 3

  promtail:
    image: grafana/promtail:2.9.0
    container_name: sportsbook-promtail
    restart: unless-stopped
    command: -config.file=/etc/promtail/config.yml
    volumes:
      - ./loki/promtail-config.yml:/etc/promtail/config.yml:ro
      - orleans_logs:/var/log/orleans:ro
      - api_logs:/var/log/api:ro
      - /var/run/docker.sock:/var/run/docker.sock:ro
    depends_on:
      - loki
    networks:
      - sportsbook-network

volumes:
  loki_data:
    driver: local
```

#### 7.2 Development Loki Configuration
```yaml
# docker/loki/local-config.yaml
auth_enabled: false

server:
  http_listen_port: 3100
  grpc_listen_port: 9096

common:
  instance_addr: 127.0.0.1
  path_prefix: /loki
  storage:
    filesystem:
      chunks_directory: /loki/chunks
      rules_directory: /loki/rules
  replication_factor: 1

schema_config:
  configs:
    - from: 2020-10-24
      store: boltdb-shipper
      object_store: filesystem
      schema: v11
      index:
        prefix: index_
        period: 24h

storage_config:
  boltdb_shipper:
    active_index_directory: /loki/boltdb-shipper-active
    cache_location: /loki/boltdb-shipper-cache
    shared_store: filesystem
  filesystem:
    directory: /loki/chunks

limits_config:
  enforce_metric_name: false
  reject_old_samples: true
  reject_old_samples_max_age: 168h
  ingestion_rate_mb: 50
  ingestion_burst_size_mb: 100

chunk_store_config:
  max_look_back_period: 0s

table_manager:
  retention_deletes_enabled: true
  retention_period: 168h  # 7 days for development

ruler:
  storage:
    type: local
    local:
      directory: /loki/rules
  rule_path: /loki/rules
  alertmanager_url: http://alertmanager:9093
```

### 8. Production Environment

#### 8.1 Kubernetes Deployment
```yaml
# k8s/loki-deployment.yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: loki-config
  namespace: sportsbook-lite
data:
  loki.yaml: |
    auth_enabled: true
    
    server:
      http_listen_port: 3100
      grpc_listen_port: 9095
      http_server_read_timeout: 30s
      http_server_write_timeout: 30s
    
    common:
      instance_addr: 127.0.0.1
      path_prefix: /loki
      storage:
        s3:
          s3: s3://sportsbook-loki-chunks
          region: us-east-1
          access_key_id: ${AWS_ACCESS_KEY_ID}
          secret_access_key: ${AWS_SECRET_ACCESS_KEY}
      replication_factor: 3
      ring:
        kvstore:
          store: consul
          consul:
            host: consul:8500
    
    memberlist:
      join_members: ["loki-memberlist"]
    
    ingester:
      lifecycler:
        join_after: 30s
        observe_period: 5s
        final_sleep: 0s
    
    schema_config:
      configs:
        - from: 2020-10-24
          store: boltdb-shipper
          object_store: s3
          schema: v11
          index:
            prefix: index_
            period: 24h
    
    storage_config:
      boltdb_shipper:
        active_index_directory: /loki/boltdb-shipper-active
        cache_location: /loki/boltdb-shipper-cache
        shared_store: s3
        cache_ttl: 24h
      
      aws:
        s3: s3://sportsbook-loki-chunks
        s3forcepathstyle: false
        bucketnames: sportsbook-loki-chunks
        region: us-east-1
        access_key_id: ${AWS_ACCESS_KEY_ID}
        secret_access_key: ${AWS_SECRET_ACCESS_KEY}
    
    limits_config:
      enforce_metric_name: false
      reject_old_samples: true
      reject_old_samples_max_age: 168h
      ingestion_rate_mb: 20
      ingestion_burst_size_mb: 50
      per_stream_rate_limit: 5MB
      per_stream_rate_limit_burst: 20MB
    
    chunk_store_config:
      max_look_back_period: 0s
      chunk_cache_config:
        redis:
          endpoint: redis:6379
          timeout: 100ms
          expiration: 1h
    
    table_manager:
      retention_deletes_enabled: true
      retention_period: 2160h  # 90 days
    
    ruler:
      storage:
        type: s3
        s3:
          s3: s3://sportsbook-loki-rules
      rule_path: /loki/rules
      alertmanager_url: http://alertmanager:9093
      ring:
        kvstore:
          store: consul
          consul:
            host: consul:8500

---
apiVersion: apps/v1
kind: StatefulSet
metadata:
  name: loki
  namespace: sportsbook-lite
spec:
  serviceName: loki
  replicas: 3
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
        args:
        - -config.file=/etc/loki/loki.yaml
        - -target=all
        ports:
        - name: http
          containerPort: 3100
        - name: grpc
          containerPort: 9095
        - name: memberlist
          containerPort: 7946
        env:
        - name: AWS_ACCESS_KEY_ID
          valueFrom:
            secretKeyRef:
              name: loki-secrets
              key: aws-access-key-id
        - name: AWS_SECRET_ACCESS_KEY
          valueFrom:
            secretKeyRef:
              name: loki-secrets
              key: aws-secret-access-key
        volumeMounts:
        - name: config
          mountPath: /etc/loki
        - name: storage
          mountPath: /loki
        resources:
          requests:
            memory: "2Gi"
            cpu: "1000m"
          limits:
            memory: "4Gi"
            cpu: "2000m"
        livenessProbe:
          httpGet:
            path: /ready
            port: 3100
          initialDelaySeconds: 45
        readinessProbe:
          httpGet:
            path: /ready
            port: 3100
          initialDelaySeconds: 45
      volumes:
      - name: config
        configMap:
          name: loki-config
  volumeClaimTemplates:
  - metadata:
      name: storage
    spec:
      accessModes: ["ReadWriteOnce"]
      resources:
        requests:
          storage: 50Gi
      storageClassName: ssd-storage
```

### 9. Grafana Integration and Dashboards

#### 9.1 Loki Data Source Configuration
```yaml
# k8s/grafana-datasource.yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: grafana-datasources
  namespace: sportsbook-lite
data:
  loki.yaml: |
    apiVersion: 1
    datasources:
    - name: Loki
      type: loki
      access: proxy
      url: http://loki:3100
      isDefault: false
      editable: true
      jsonData:
        maxLines: 1000
        derivedFields:
          - datasourceUid: prometheus
            matcherRegex: "correlation_id=(\\w+)"
            name: TraceID
            url: "http://jaeger:16686/trace/${__value.raw}"
```

#### 9.2 Log-Based Alerting Rules
```yaml
# k8s/loki-alerts.yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: loki-alerts
data:
  alerts.yaml: |
    groups:
    - name: sportsbook-log-alerts
      rules:
      - alert: HighErrorRate
        expr: |
          (
            sum(rate({service_name="sportsbook-orleans"} |= "ERROR" [5m])) by (service_name)
            /
            sum(rate({service_name="sportsbook-orleans"}[5m])) by (service_name)
          ) > 0.05
        for: 2m
        labels:
          severity: warning
        annotations:
          summary: "High error rate in Orleans logs"
          description: "Orleans service {{ $labels.service_name }} has error rate above 5%"
      
      - alert: CriticalBettingError
        expr: |
          count_over_time({service_name="sportsbook-orleans"} |= "BetPlaced" |= "ERROR" [5m]) > 0
        labels:
          severity: critical
        annotations:
          summary: "Critical betting operation failure"
          description: "Betting operations are failing - immediate attention required"
      
      - alert: GrainActivationFailures
        expr: |
          sum(count_over_time({service_name="sportsbook-orleans"} |= "Grain activation failed" [5m])) > 10
        for: 1m
        labels:
          severity: warning
        annotations:
          summary: "High grain activation failures"
          description: "More than 10 grain activation failures in 5 minutes"
      
      - alert: DatabaseConnectionErrors  
        expr: |
          count_over_time({service_name=~"sportsbook-.*"} |= "database" |= "connection" |= "error" [5m]) > 0
        labels:
          severity: critical
        annotations:
          summary: "Database connection errors detected"
          description: "Database connectivity issues detected in logs"
```

#### 9.3 Comprehensive Log Dashboard
```json
{
  "dashboard": {
    "id": null,
    "title": "SportsbookLite Logging Dashboard",
    "tags": ["logs", "sportsbook", "orleans"],
    "timezone": "UTC",
    "panels": [
      {
        "id": 1,
        "title": "Log Volume by Service",
        "type": "stat",
        "targets": [
          {
            "expr": "sum(rate({service_name=~\"sportsbook-.*\"}[5m])) by (service_name)",
            "legendFormat": "{{service_name}}"
          }
        ],
        "fieldConfig": {
          "defaults": {
            "unit": "reqps",
            "color": {"mode": "palette-classic"}
          }
        }
      },
      {
        "id": 2,
        "title": "Error Logs Timeline", 
        "type": "logs",
        "targets": [
          {
            "expr": "{service_name=~\"sportsbook-.*\"} |= \"ERROR\"",
            "refId": "A"
          }
        ],
        "options": {
          "showTime": true,
          "showLabels": true,
          "sortOrder": "Descending",
          "wrapLogMessage": true
        }
      },
      {
        "id": 3,
        "title": "Grain Lifecycle Events",
        "type": "logs",
        "targets": [
          {
            "expr": "{service_name=\"sportsbook-orleans\"} |~ \"(activated|deactivated)\"",
            "refId": "A"
          }
        ]
      },
      {
        "id": 4,
        "title": "Business Event Distribution",
        "type": "piechart", 
        "targets": [
          {
            "expr": "sum(count_over_time({service_name=~\"sportsbook-.*\"} | json | EventType != \"\" [1h])) by (EventType)",
            "legendFormat": "{{EventType}}"
          }
        ]
      },
      {
        "id": 5,
        "title": "Correlation ID Trace",
        "type": "logs",
        "targets": [
          {
            "expr": "{service_name=~\"sportsbook-.*\"} | json | CorrelationId=\"$correlation_id\"",
            "refId": "A"
          }
        ]
      },
      {
        "id": 6,
        "title": "Performance Metrics from Logs",
        "type": "graph",
        "targets": [
          {
            "expr": "quantile_over_time(0.95, {service_name=~\"sportsbook-.*\"} | json | ElapsedMs != \"\" | unwrap ElapsedMs [5m])",
            "legendFormat": "95th percentile"
          },
          {
            "expr": "quantile_over_time(0.50, {service_name=~\"sportsbook-.*\"} | json | ElapsedMs != \"\" | unwrap ElapsedMs [5m])", 
            "legendFormat": "50th percentile"
          }
        ]
      }
    ],
    "templating": {
      "list": [
        {
          "name": "correlation_id",
          "type": "textbox",
          "label": "Correlation ID"
        },
        {
          "name": "service_name",
          "type": "query", 
          "query": "label_values(service_name)",
          "multi": true,
          "includeAll": true
        }
      ]
    },
    "time": {
      "from": "now-1h",
      "to": "now"
    },
    "refresh": "30s"
  }
}
```

## Implementation Roadmap

### Phase 1: Foundation (Week 1)
1. ✅ Add Serilog.Sinks.Grafana.Loki NuGet packages
2. ✅ Configure basic Loki sink in both Host and API projects  
3. ✅ Set up development Docker Compose with Loki
4. ✅ Implement correlation ID middleware
5. ✅ Add sensitive data masking formatter

### Phase 2: Production Readiness (Week 2)
1. ✅ Deploy Loki StatefulSet in Kubernetes
2. ✅ Configure S3 backend for production
3. ✅ Implement circuit breaker and fallback logging
4. ✅ Set up Promtail for log shipping
5. ✅ Configure retention policies per environment

### Phase 3: Advanced Features (Week 3)
1. ✅ Implement adaptive log sampling
2. ✅ Set up structured business event logging
3. ✅ Configure Orleans-specific enrichers
4. ✅ Create comprehensive Grafana dashboards
5. ✅ Implement log-based alerting rules

### Phase 4: Optimization (Week 4)
1. ✅ Performance testing and tuning
2. ✅ Security audit and hardening  
3. ✅ Documentation and runbooks
4. ✅ Team training and handover
5. ✅ Production rollout planning

## Monitoring and Observability

### Key Metrics to Track
- **Log Ingestion Rate**: Logs per second by service
- **Log Processing Latency**: Time from log generation to Loki storage
- **Storage Growth**: Daily log volume and retention compliance
- **Query Performance**: Dashboard and search response times
- **Error Rates**: Failed log ingestion and processing errors
- **Business Events**: Betting, odds, wallet transaction volumes

### Success Criteria
- [ ] ✅ All logs centralized in Loki across dev/staging/prod
- [ ] ✅ Sub-second correlation between metrics and logs
- [ ] ✅ Zero sensitive data exposure in logs
- [ ] ✅ <5% performance impact on application throughput
- [ ] ✅ 99.9% log ingestion success rate
- [ ] ✅ Full distributed trace correlation across Orleans grains

## Cost Optimization

### Storage Optimization
- Implement intelligent log sampling based on volume and severity
- Use compression and efficient encoding (Snappy, LZ4)
- Archive old logs to cheaper storage tiers (S3 Glacier)
- Implement log deduplication for repeated events

### Infrastructure Optimization
- Right-size Loki instances based on actual usage patterns
- Use spot instances for non-critical log processing
- Implement auto-scaling based on log volume
- Optimize network bandwidth with regional deployments

## Conclusion

This comprehensive Loki logging architecture provides SportsbookLite with enterprise-grade centralized logging that scales with business growth while maintaining security, performance, and observability requirements. The phased implementation approach ensures minimal disruption to existing operations while progressively enhancing logging capabilities.

The architecture addresses all critical requirements:
- **Distributed Grain Logging**: Comprehensive Orleans lifecycle and communication logging
- **Correlation**: Full request tracing across microservices and grains  
- **Security**: Sensitive data masking and access controls
- **Performance**: Asynchronous logging with circuit breakers and sampling
- **Scalability**: Multi-replica Loki with load balancing and storage sharding
- **Multi-Environment**: Tailored configurations for dev, staging, and production

Key benefits include improved troubleshooting capabilities, proactive issue detection, compliance with data protection requirements, and enhanced operational visibility across the entire distributed system.