# Monitoring Architecture Plan for Sportsbook-Lite

## Table of Contents
1. [Executive Summary](#executive-summary)
2. [High-Level Architecture Overview](#high-level-architecture-overview)
3. [Component Interactions and Data Flow](#component-interactions-and-data-flow)
4. [Metrics Collection Strategy](#metrics-collection-strategy)
5. [Dashboard Hierarchy and Organization](#dashboard-hierarchy-and-organization)
6. [Alerting Architecture and Escalation](#alerting-architecture-and-escalation)
7. [Multi-Environment Considerations](#multi-environment-considerations)
8. [Security and Compliance](#security-and-compliance)
9. [Performance and Scalability](#performance-and-scalability)
10. [Disaster Recovery](#disaster-recovery)
11. [Integration Points](#integration-points)
12. [Implementation Roadmap](#implementation-roadmap)

## Executive Summary

This document outlines a comprehensive monitoring architecture for Sportsbook-Lite, a distributed system built on Microsoft Orleans, FastEndpoints, PostgreSQL, Redis, and Apache Pulsar. The monitoring solution uses Prometheus for metrics collection, Grafana for visualization, and AlertManager for alerting, providing complete observability across all system components.

### Key Design Principles
- **Cloud-Native**: Container-ready with Kubernetes integration
- **Multi-Layered**: Infrastructure, platform, application, and business metrics
- **Scalable**: Designed to handle high-frequency betting operations
- **Reliable**: 99.9% uptime target for monitoring infrastructure
- **Secure**: Zero-trust model with encrypted communications

## High-Level Architecture Overview

### Architecture Components

```mermaid
graph TB
    subgraph "Application Layer"
        API[FastEndpoints API]
        Orleans[Orleans Silos]
        Worker[Background Workers]
    end
    
    subgraph "Data Layer"
        PG[(PostgreSQL)]
        Redis[(Redis)]
        Pulsar[Apache Pulsar]
    end
    
    subgraph "Infrastructure Layer"
        K8s[Kubernetes]
        LB[Load Balancer]
        Ingress[Ingress Controller]
    end
    
    subgraph "Monitoring Stack"
        Prom[Prometheus]
        Graf[Grafana]
        Alert[AlertManager]
        Jaeger[Jaeger Tracing]
        Loki[Loki Logs]
    end
    
    subgraph "External Systems"
        PD[PagerDuty]
        Slack[Slack]
        Email[Email SMTP]
    end
    
    API --> Prom
    Orleans --> Prom
    Worker --> Prom
    PG --> Prom
    Redis --> Prom
    Pulsar --> Prom
    K8s --> Prom
    
    Prom --> Graf
    Prom --> Alert
    Alert --> PD
    Alert --> Slack
    Alert --> Email
    
    API --> Jaeger
    Orleans --> Jaeger
    API --> Loki
    Orleans --> Loki
```

### Core Components

| Component | Purpose | High Availability | Retention |
|-----------|---------|------------------|-----------|
| **Prometheus** | Metrics collection and storage | 3 replicas, clustered | 30 days local, 1 year remote |
| **Grafana** | Visualization and dashboards | 2 replicas, shared DB | N/A (stateless) |
| **AlertManager** | Alert routing and notification | 3 replicas, clustered | 5 days |
| **Jaeger** | Distributed tracing | Elasticsearch backend | 7 days traces, 1 day spans |
| **Loki** | Log aggregation | 3 replicas, S3 storage | 30 days |

## Component Interactions and Data Flow

### Data Collection Flow

1. **Application Metrics**: Custom metrics exported via `/metrics` endpoints
2. **Infrastructure Metrics**: Node Exporter, cAdvisor, kube-state-metrics
3. **Service Discovery**: Kubernetes service discovery for dynamic targets
4. **Data Ingestion**: Prometheus scrapes metrics every 15 seconds
5. **Storage**: Local TSDB with remote storage for long-term retention
6. **Querying**: PromQL queries via Grafana and AlertManager

### Network Architecture

```yaml
# Network flow diagram
Monitoring Namespace:
  - prometheus.monitoring.svc.cluster.local:9090
  - grafana.monitoring.svc.cluster.local:3000
  - alertmanager.monitoring.svc.cluster.local:9093

Application Namespaces:
  - orleans-silo.sportsbook.svc.cluster.local:11111
  - api.sportsbook.svc.cluster.local:8080
  - postgres.sportsbook.svc.cluster.local:5432
```

## Metrics Collection Strategy

### Orleans Grain Metrics

#### Custom Metrics Implementation
```csharp
// Orleans grain with custom metrics
public sealed class BetGrain : Grain, IBetGrain
{
    private static readonly Counter BetsPlaced = Metrics
        .CreateCounter("sportsbook_bets_placed_total", 
                      "Total bets placed", 
                      new[] { "outcome", "market_type" });
    
    private static readonly Histogram BetProcessingTime = Metrics
        .CreateHistogram("sportsbook_bet_processing_duration_seconds",
                        "Bet processing duration",
                        new[] { "grain_type" });
    
    private static readonly Gauge ActiveBets = Metrics
        .CreateGauge("sportsbook_active_bets",
                    "Currently active bets",
                    new[] { "market_id" });

    public async Task<BetResult> PlaceBetAsync(PlaceBetRequest request)
    {
        using var timer = BetProcessingTime.WithLabels("bet_grain").NewTimer();
        
        try
        {
            var result = await ProcessBetInternalAsync(request);
            BetsPlaced.WithLabels(result.Outcome, request.MarketType).Inc();
            ActiveBets.WithLabels(request.MarketId).Inc();
            return result;
        }
        catch (Exception ex)
        {
            BetsPlaced.WithLabels("error", request.MarketType).Inc();
            throw;
        }
    }
}
```

#### Orleans System Metrics
- **Grain Activation Rate**: `orleans_grain_activations_per_second`
- **Grain Deactivation Rate**: `orleans_grain_deactivations_per_second`
- **Message Processing Rate**: `orleans_messages_processed_per_second`
- **Silo Memory Usage**: `orleans_silo_memory_usage_bytes`
- **Grain Directory Size**: `orleans_grain_directory_entries`
- **Reminder Statistics**: `orleans_reminders_active_count`

### FastEndpoints API Metrics

#### HTTP Metrics
```csharp
// Custom metrics middleware for FastEndpoints
public class MetricsMiddleware : IMiddleware
{
    private static readonly Counter HttpRequests = Metrics
        .CreateCounter("sportsbook_http_requests_total",
                      "Total HTTP requests",
                      new[] { "method", "endpoint", "status_code" });
    
    private static readonly Histogram HttpDuration = Metrics
        .CreateHistogram("sportsbook_http_request_duration_seconds",
                        "HTTP request duration",
                        new[] { "method", "endpoint" });

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var stopwatch = Stopwatch.StartNew();
        var endpoint = context.Request.Path.Value ?? "unknown";
        var method = context.Request.Method;
        
        try
        {
            await next(context);
        }
        finally
        {
            stopwatch.Stop();
            var statusCode = context.Response.StatusCode.ToString();
            
            HttpRequests.WithLabels(method, endpoint, statusCode).Inc();
            HttpDuration.WithLabels(method, endpoint)
                       .Observe(stopwatch.Elapsed.TotalSeconds);
        }
    }
}
```

#### Business Logic Metrics
- **Bet Placement Rate**: `sportsbook_bets_placed_per_second`
- **Wallet Transaction Rate**: `sportsbook_wallet_transactions_per_second`
- **Event Updates**: `sportsbook_event_updates_per_second`
- **Odds Changes**: `sportsbook_odds_updates_per_second`
- **User Sessions**: `sportsbook_active_user_sessions`

### Infrastructure Metrics

#### PostgreSQL Metrics
```yaml
# PostgreSQL Exporter Configuration
postgres_exporter:
  datasource_name: "postgresql://metrics_user:password@postgres:5432/sportsbook?sslmode=disable"
  custom_queries:
    - name: "sportsbook_bet_counts"
      query: "SELECT status, COUNT(*) as count FROM bets GROUP BY status"
      metrics:
        - count:
            usage: "GAUGE"
            description: "Number of bets by status"
```

Key PostgreSQL Metrics:
- **Connection Pool**: `pg_stat_database_numbackends`
- **Query Performance**: `pg_stat_statements_mean_time`
- **Lock Waits**: `pg_locks_count`
- **Table Sizes**: `pg_relation_size_bytes`
- **Index Usage**: `pg_stat_user_indexes_idx_scan`

#### Redis Metrics
- **Memory Usage**: `redis_memory_used_bytes`
- **Connected Clients**: `redis_connected_clients`
- **Commands per Second**: `redis_instantaneous_ops_per_sec`
- **Keyspace Hits/Misses**: `redis_keyspace_hits_total`
- **Replication Lag**: `redis_master_repl_offset`

#### Apache Pulsar Metrics
- **Message Rate In**: `pulsar_rate_in`
- **Message Rate Out**: `pulsar_rate_out`
- **Storage Size**: `pulsar_storage_size`
- **Subscription Backlog**: `pulsar_subscription_back_log`
- **Consumer Lag**: `pulsar_consumer_lag`

### Kubernetes Infrastructure Metrics
- **Pod Resource Usage**: CPU, memory, network I/O
- **Node Health**: CPU, memory, disk usage
- **Deployment Status**: Ready replicas, rollout status
- **Service Mesh**: Request rate, error rate, latency (if using Istio)

## Dashboard Hierarchy and Organization

### Dashboard Categories

#### Executive Dashboards
1. **Business Overview**
   - Revenue metrics
   - User engagement
   - System health summary
   - SLA compliance

2. **Operational Summary**
   - Service availability
   - Error rates
   - Performance trends
   - Capacity utilization

#### Engineering Dashboards
1. **Orleans Cluster Health**
   - Silo status and membership
   - Grain activation patterns
   - Message throughput
   - Memory and CPU usage

2. **API Performance**
   - Request/response metrics
   - Error rates by endpoint
   - Authentication success rates
   - Rate limiting effectiveness

3. **Data Layer Performance**
   - Database query performance
   - Connection pool utilization
   - Cache hit rates
   - Message queue health

#### Platform Dashboards
1. **Kubernetes Cluster**
   - Node health and capacity
   - Pod resource consumption
   - Network performance
   - Storage utilization

2. **Infrastructure Services**
   - Load balancer performance
   - DNS resolution times
   - Certificate expiration
   - Network latency

### Dashboard Standards

#### Design Guidelines
```json
{
  "layout": {
    "grid": "24 columns",
    "panel_height": "standardized heights (6, 8, 12 units)",
    "spacing": "consistent 8px margins"
  },
  "colors": {
    "success": "#73BF69",
    "warning": "#FADE2A", 
    "critical": "#F2495C",
    "info": "#5794F2"
  },
  "thresholds": {
    "response_time": {
      "good": "< 100ms",
      "warning": "100-500ms",
      "critical": "> 500ms"
    },
    "error_rate": {
      "good": "< 0.1%",
      "warning": "0.1-1%", 
      "critical": "> 1%"
    }
  }
}
```

#### Drill-down Hierarchy
1. **Level 1**: High-level service status
2. **Level 2**: Service-specific metrics
3. **Level 3**: Component-level details
4. **Level 4**: Raw logs and traces

## Alerting Architecture and Escalation

### Alert Severity Levels

| Severity | Response Time | Escalation | Notification Channels |
|----------|---------------|------------|----------------------|
| **P0 - Critical** | Immediate | 5 minutes | PagerDuty, Phone, SMS, Slack |
| **P1 - High** | 15 minutes | 30 minutes | PagerDuty, Slack, Email |
| **P2 - Medium** | 2 hours | 4 hours | Slack, Email |
| **P3 - Low** | Next business day | N/A | Email, Ticket |

### Core Alerts

#### Orleans System Alerts
```yaml
# AlertManager rules for Orleans
groups:
- name: orleans.rules
  rules:
  - alert: OrleansGrainActivationHigh
    expr: rate(orleans_grain_activations_total[5m]) > 1000
    for: 2m
    labels:
      severity: warning
      service: orleans
    annotations:
      summary: "High grain activation rate detected"
      description: "Grain activation rate is {{ $value }} per second"

  - alert: OrleansSiloDown
    expr: up{job="orleans-silo"} == 0
    for: 30s
    labels:
      severity: critical
      service: orleans
    annotations:
      summary: "Orleans Silo is down"
      description: "Silo {{ $labels.instance }} has been down for 30 seconds"

  - alert: OrleansMemoryHigh
    expr: orleans_silo_memory_usage_bytes / orleans_silo_memory_limit_bytes > 0.8
    for: 5m
    labels:
      severity: warning
      service: orleans
    annotations:
      summary: "Orleans Silo memory usage high"
      description: "Memory usage is {{ $value | humanizePercentage }}"
```

#### Business Logic Alerts
```yaml
- alert: BetProcessingLatencyHigh
  expr: histogram_quantile(0.95, rate(sportsbook_bet_processing_duration_seconds_bucket[5m])) > 1.0
  for: 3m
  labels:
    severity: warning
    service: betting
  annotations:
    summary: "Bet processing latency is high"
    description: "95th percentile latency is {{ $value }}s"

- alert: WalletBalanceInconsistency
  expr: abs(sportsbook_wallet_balance_calculated - sportsbook_wallet_balance_cached) > 100
  for: 1m
  labels:
    severity: critical
    service: wallet
  annotations:
    summary: "Wallet balance inconsistency detected"
    description: "Balance difference is {{ $value }}"
```

#### Infrastructure Alerts
```yaml
- alert: PostgreSQLConnectionPoolExhausted
  expr: pg_stat_database_numbackends / pg_settings_max_connections > 0.8
  for: 2m
  labels:
    severity: critical
    service: database
  annotations:
    summary: "PostgreSQL connection pool nearly exhausted"

- alert: RedisMemoryHigh
  expr: redis_memory_used_bytes / redis_config_maxmemory > 0.9
  for: 5m
  labels:
    severity: warning
    service: cache
```

### Escalation Configuration
```yaml
# AlertManager configuration
global:
  smtp_smarthost: 'smtp.company.com:587'
  smtp_from: 'monitoring@sportsbook.com'
  pagerduty_url: 'https://events.pagerduty.com/v2/enqueue'

route:
  group_by: ['alertname', 'severity']
  group_wait: 30s
  group_interval: 5m
  repeat_interval: 4h
  receiver: 'default'
  routes:
  - match:
      severity: critical
    receiver: 'pagerduty-critical'
    group_wait: 10s
    repeat_interval: 1m
  - match:
      severity: warning
    receiver: 'slack-ops'
    repeat_interval: 1h

receivers:
- name: 'pagerduty-critical'
  pagerduty_configs:
  - service_key: 'your-pagerduty-service-key'
    description: '{{ .GroupLabels.alertname }} - {{ .CommonAnnotations.summary }}'
    
- name: 'slack-ops'
  slack_configs:
  - api_url: 'https://hooks.slack.com/services/...'
    channel: '#sportsbook-alerts'
    title: 'Alert: {{ .GroupLabels.alertname }}'
```

## Multi-Environment Considerations

### Environment-Specific Configuration

#### Development Environment
- **Retention**: 7 days
- **Scrape Interval**: 30 seconds
- **Alert Routing**: Slack only
- **Resource Limits**: 2 CPU, 4GB RAM per component

#### Staging Environment
- **Retention**: 14 days
- **Scrape Interval**: 15 seconds
- **Alert Routing**: Slack + Email
- **Resource Limits**: 4 CPU, 8GB RAM per component
- **Load Testing**: Synthetic transaction monitoring

#### Production Environment
- **Retention**: 30 days local, 1 year remote
- **Scrape Interval**: 15 seconds
- **Alert Routing**: Full escalation chain
- **Resource Limits**: Auto-scaling, dedicated nodes
- **High Availability**: 3+ replicas, multi-AZ deployment

### Environment Isolation
```yaml
# Namespace separation
environments:
  dev:
    namespace: "monitoring-dev"
    datasource: "prometheus-dev.monitoring-dev.svc.cluster.local"
  staging:
    namespace: "monitoring-staging"  
    datasource: "prometheus-staging.monitoring-staging.svc.cluster.local"
  prod:
    namespace: "monitoring-prod"
    datasource: "prometheus-prod.monitoring-prod.svc.cluster.local"
```

## Security and Compliance

### Authentication and Authorization

#### Multi-Tenant Security Model
```yaml
# RBAC Configuration
apiVersion: rbac.authorization.k8s.io/v1
kind: ClusterRole
metadata:
  name: prometheus-monitoring
rules:
- apiGroups: [""]
  resources: ["nodes", "pods", "services", "endpoints"]
  verbs: ["get", "list", "watch"]
- apiGroups: ["apps"]
  resources: ["deployments", "daemonsets", "replicasets"]
  verbs: ["get", "list", "watch"]
```

#### Grafana Security Configuration
```yaml
grafana:
  auth:
    generic_oauth:
      enabled: true
      client_id: sportsbook-grafana
      client_secret: ${OAUTH_CLIENT_SECRET}
      scopes: openid profile email groups
      auth_url: https://auth.company.com/oauth2/authorize
      token_url: https://auth.company.com/oauth2/token
      api_url: https://auth.company.com/oauth2/userinfo
      
  security:
    admin_user: admin
    admin_password: ${GRAFANA_ADMIN_PASSWORD}
    secret_key: ${GRAFANA_SECRET_KEY}
    disable_gravatar: true
    cookie_secure: true
    cookie_samesite: strict
```

### Data Protection

#### Encryption in Transit
- **TLS 1.3**: All inter-service communication
- **mTLS**: Service mesh integration
- **Certificate Rotation**: Automatic via cert-manager

#### Encryption at Rest
- **Prometheus**: Encrypted volumes
- **Grafana**: Encrypted database backend
- **Secrets**: Kubernetes secrets with envelope encryption

#### Data Retention and Privacy
```yaml
# Retention policies
retention_policies:
  metrics:
    high_resolution: "6 hours"    # 15-second intervals
    medium_resolution: "7 days"   # 1-minute intervals  
    low_resolution: "30 days"     # 5-minute intervals
    long_term: "1 year"          # 1-hour intervals
  
  logs:
    application: "30 days"
    audit: "7 years"
    debug: "3 days"
  
  traces:
    detailed: "24 hours"
    sampled: "7 days"
```

### Compliance Requirements

#### Audit Logging
```yaml
# Audit configuration
audit:
  enabled: true
  retention: "7 years"
  events:
    - configuration_changes
    - user_access
    - alert_modifications
    - dashboard_exports
    - query_executions
```

#### Regulatory Compliance
- **SOC 2 Type II**: Comprehensive logging and access controls
- **PCI DSS**: Payment data isolation (if applicable)
- **GDPR**: Personal data anonymization and retention
- **ISO 27001**: Information security management

## Performance and Scalability

### Scaling Strategy

#### Horizontal Scaling Configuration
```yaml
# Prometheus scaling
prometheus:
  replicas: 3
  sharding:
    enabled: true
    shards: 2
  federation:
    enabled: true
    
  retention:
    time: 30d
    size: 100GB
  
  resources:
    limits:
      cpu: 4
      memory: 8Gi
    requests:
      cpu: 2
      memory: 4Gi
      
  storage:
    class: fast-ssd
    size: 200Gi
    
# Grafana scaling  
grafana:
  replicas: 2
  database:
    type: postgres
    host: postgres-ha.monitoring.svc.cluster.local
  
  resources:
    limits:
      cpu: 1
      memory: 2Gi
    requests:
      cpu: 500m
      memory: 1Gi
```

#### Performance Optimization

##### Query Optimization
```promql
# Optimized queries using recording rules
groups:
- name: sportsbook.rules
  interval: 30s
  rules:
  - record: sportsbook:bet_rate_5m
    expr: rate(sportsbook_bets_placed_total[5m])
    
  - record: sportsbook:error_rate_5m  
    expr: rate(sportsbook_http_requests_total{status_code=~"5.."}[5m]) / rate(sportsbook_http_requests_total[5m])
    
  - record: sportsbook:response_time_p95_5m
    expr: histogram_quantile(0.95, rate(sportsbook_http_request_duration_seconds_bucket[5m]))
```

##### Storage Optimization
```yaml
# Remote storage configuration
remote_write:
- url: "https://cortex.company.com/api/prom/push"
  write_relabel_configs:
  - source_labels: [__name__]
    regex: '(sportsbook_.*|orleans_.*)'
    action: keep
  
  queue_config:
    capacity: 10000
    max_samples_per_send: 2000
    batch_send_deadline: 5s
    min_backoff: 30ms
    max_backoff: 100ms
```

### Capacity Planning

#### Resource Requirements by Load
| Daily Active Users | Prometheus Storage | Grafana Replicas | Alert Volume |
|--------------------|-------------------|------------------|--------------|
| < 10K | 50GB | 1 | < 100/day |
| 10K - 100K | 200GB | 2 | 100-500/day |
| 100K - 1M | 500GB | 3 | 500-2K/day |
| > 1M | 1TB+ | 5+ | 2K+/day |

#### Auto-scaling Configuration
```yaml
# HorizontalPodAutoscaler
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: grafana-hpa
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: grafana
  minReplicas: 2
  maxReplicas: 10
  metrics:
  - type: Resource
    resource:
      name: cpu
      target:
        type: Utilization
        averageUtilization: 70
  - type: Resource
    resource:
      name: memory
      target:
        type: Utilization
        averageUtilization: 80
```

## Disaster Recovery

### Backup and Recovery Strategy

#### Backup Components
1. **Prometheus Data**
   - WAL segments: Continuous backup to S3
   - TSDB blocks: Daily snapshots
   - Configuration: Git-based versioning

2. **Grafana Configuration**
   - Dashboard definitions: API-based export
   - Data sources: Configuration as code
   - User data: Database backup

3. **AlertManager Configuration**
   - Rules and routes: Git-based versioning
   - Notification history: Log aggregation

#### Recovery Procedures
```bash
#!/bin/bash
# Prometheus disaster recovery script

# Restore from backup
aws s3 sync s3://monitoring-backups/prometheus-data/ /prometheus/data/

# Start Prometheus with recovered data
kubectl scale deployment prometheus --replicas=1

# Verify data integrity
kubectl exec -it prometheus-0 -- promtool tsdb analyze /prometheus/data

# Restore Grafana dashboards
for dashboard in dashboards/*.json; do
  curl -X POST \
    -H "Authorization: Bearer $GRAFANA_API_KEY" \
    -H "Content-Type: application/json" \
    -d @"$dashboard" \
    http://grafana:3000/api/dashboards/db
done
```

#### Recovery Time Objectives (RTO) and Recovery Point Objectives (RPO)
| Component | RTO | RPO | Recovery Method |
|-----------|-----|-----|----------------|
| Prometheus | 15 minutes | 5 minutes | Automated restore from S3 |
| Grafana | 5 minutes | 1 hour | Database restore + config sync |
| AlertManager | 2 minutes | Real-time | Config management deployment |
| Jaeger | 30 minutes | 1 hour | Elasticsearch cluster restore |

### Multi-Region Strategy
```yaml
# Multi-region deployment
regions:
  primary:
    region: us-west-2
    prometheus_retention: 30d
    full_monitoring_stack: true
    
  secondary:
    region: us-east-1  
    prometheus_retention: 7d
    monitoring_stack: read_only
    
  disaster_recovery:
    region: eu-west-1
    prometheus_retention: 90d
    monitoring_stack: cold_standby
```

## Integration Points

### External System Integrations

#### PagerDuty Integration
```yaml
# PagerDuty service configuration
pagerduty:
  integration_key: ${PAGERDUTY_INTEGRATION_KEY}
  escalation_policy_id: "P123ABC"
  
  services:
    - name: "Sportsbook Critical"
      escalation_policy: "immediate"
      urgency: "high"
    - name: "Sportsbook Warning"  
      escalation_policy: "business-hours"
      urgency: "low"
```

#### Slack Integration
```yaml
# Slack webhook configuration
slack:
  channels:
    critical: "#sportsbook-critical"
    warning: "#sportsbook-alerts"
    info: "#sportsbook-info"
  
  webhook_urls:
    critical: "https://hooks.slack.com/services/T00000000/B00000000/XXXXXXXXXXXXXXXXXXXXXXXX"
    general: "https://hooks.slack.com/services/T00000000/B00000001/YYYYYYYYYYYYYYYYYYYYYYYY"
```

#### JIRA Integration
```yaml
# JIRA ticket creation for P3 alerts
jira:
  url: "https://company.atlassian.net"
  project: "SPORTSBOOK"
  issue_type: "Bug"
  
  automation:
    create_ticket: true
    severity_mapping:
      P3: "Low"
      P2: "Medium" 
      P1: "High"
      P0: "Critical"
```

### CI/CD Integration

#### GitOps Workflow
```yaml
# GitHub Actions integration
name: Deploy Monitoring Stack
on:
  push:
    paths:
      - 'monitoring/**'
      
jobs:
  deploy:
    runs-on: ubuntu-latest
    steps:
    - uses: actions/checkout@v3
    
    - name: Validate Prometheus Config
      run: promtool check config monitoring/prometheus/prometheus.yml
      
    - name: Validate Alert Rules
      run: promtool check rules monitoring/prometheus/rules/*.yml
      
    - name: Deploy to Kubernetes
      run: |
        kubectl apply -f monitoring/k8s/
        kubectl rollout status deployment/prometheus
        kubectl rollout status deployment/grafana
```

#### Configuration Management
```yaml
# ArgoCD application
apiVersion: argoproj.io/v1alpha1
kind: Application
metadata:
  name: monitoring-stack
spec:
  project: default
  source:
    repoURL: https://github.com/company/sportsbook-monitoring
    targetRevision: HEAD
    path: k8s
  destination:
    server: https://kubernetes.default.svc
    namespace: monitoring
  syncPolicy:
    automated:
      prune: true
      selfHeal: true
```

### API Integration

#### Monitoring API Endpoints
```csharp
// Health check endpoint integration
[HttpGet("/health")]
public async Task<IActionResult> Health()
{
    var healthChecks = new[]
    {
        await CheckOrleansCluster(),
        await CheckDatabase(),
        await CheckRedis(),
        await CheckPulsar()
    };
    
    var overallHealth = healthChecks.All(h => h.IsHealthy) 
        ? "healthy" : "unhealthy";
        
    return Ok(new
    {
        status = overallHealth,
        checks = healthChecks,
        timestamp = DateTimeOffset.UtcNow
    });
}

// Metrics endpoint
[HttpGet("/metrics")]
public IActionResult Metrics()
{
    return Ok(Metrics.DefaultRegistry.CollectorRegistry
        .CollectAll()
        .ToPrometheusText());
}
```

## Implementation Roadmap

### Phase 1: Foundation (Weeks 1-2)
- [ ] Deploy Prometheus cluster
- [ ] Deploy Grafana with basic dashboards
- [ ] Configure AlertManager with basic alerts
- [ ] Set up basic Kubernetes monitoring
- [ ] Implement health check endpoints

### Phase 2: Application Monitoring (Weeks 3-4)
- [ ] Instrument Orleans grains with custom metrics
- [ ] Implement FastEndpoints monitoring
- [ ] Add PostgreSQL and Redis monitoring
- [ ] Create business logic dashboards
- [ ] Configure initial alerting rules

### Phase 3: Advanced Features (Weeks 5-6)
- [ ] Deploy Jaeger for distributed tracing
- [ ] Implement Loki for log aggregation
- [ ] Add Apache Pulsar monitoring
- [ ] Create advanced dashboards
- [ ] Set up external integrations (PagerDuty, Slack)

### Phase 4: Production Readiness (Weeks 7-8)
- [ ] Implement high availability configuration
- [ ] Set up remote storage
- [ ] Configure backup and disaster recovery
- [ ] Performance optimization and tuning
- [ ] Security hardening and compliance

### Phase 5: Operations and Optimization (Weeks 9-10)
- [ ] Load testing and capacity planning
- [ ] Advanced alerting and escalation
- [ ] Custom business metrics
- [ ] Documentation and runbooks
- [ ] Team training and handover

### Success Criteria
- [ ] 99.9% monitoring system uptime
- [ ] < 15 second alert response time for critical issues
- [ ] Complete visibility into all system components
- [ ] Automated incident response and escalation
- [ ] Comprehensive disaster recovery procedures

### Cost Estimation

#### Infrastructure Costs (Monthly)
| Component | Development | Production | Notes |
|-----------|-------------|------------|-------|
| Prometheus (3 replicas) | $150 | $800 | Including storage |
| Grafana (2 replicas) | $50 | $200 | Plus database costs |
| AlertManager (3 replicas) | $30 | $150 | Minimal resource usage |
| Jaeger | $100 | $500 | Elasticsearch backend |
| Loki | $75 | $400 | S3 storage costs |
| **Total** | **$405** | **$2,050** | Per environment |

#### Operational Costs (Monthly)
- PagerDuty: $25 per user
- External monitoring: $200
- SSL certificates: $50
- **Total operational**: $275

This comprehensive monitoring architecture provides enterprise-grade observability for the Sportsbook-Lite system, ensuring reliable operations and rapid incident response while maintaining security and compliance requirements.