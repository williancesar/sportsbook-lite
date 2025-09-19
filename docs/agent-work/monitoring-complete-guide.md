# Sportsbook-Lite Monitoring & Observability Complete Guide

## Table of Contents
1. [Quick Start (5-Minute Setup)](#quick-start)
2. [Architecture Overview](#architecture-overview)
3. [Installation Guide](#installation-guide)
4. [Configuration Reference](#configuration-reference)
5. [Metrics Catalog](#metrics-catalog)
6. [Dashboard User Guide](#dashboard-user-guide)
7. [Alerting Playbook](#alerting-playbook)
8. [Troubleshooting Guide](#troubleshooting-guide)
9. [Performance Tuning](#performance-tuning)
10. [Best Practices](#best-practices)

---

## Quick Start (5-Minute Setup) {#quick-start}

Get monitoring running in your local development environment in 5 minutes:

### Prerequisites
```bash
# Ensure Docker and Docker Compose are installed
docker --version
docker-compose --version

# Clone the repository (if not already done)
cd /home/willian/Repos/sportsbook-lite
```

### Step 1: Start the Monitoring Stack
```bash
# Start all monitoring services
docker-compose -f docker/docker-compose.yml -f docker/docker-compose.monitoring.yml up -d

# Verify services are running
docker-compose -f docker/docker-compose.monitoring.yml ps
```

### Step 2: Access Monitoring Interfaces
- **Grafana**: http://localhost:3000 (admin/admin)
- **Prometheus**: http://localhost:9091
- **AlertManager**: http://localhost:9093
- **Orleans Silo Metrics**: http://localhost:9090/metrics
- **API Metrics**: http://localhost:5000/metrics

### Step 3: Verify Metrics Collection
```bash
# Check Prometheus targets
curl http://localhost:9091/api/v1/targets | jq '.data.activeTargets[].health'

# Test a metric query
curl -G http://localhost:9091/api/v1/query \
  --data-urlencode 'query=up' | jq '.data.result[].value[1]'
```

### Step 4: View Dashboards
1. Open Grafana at http://localhost:3000
2. Navigate to Dashboards → Browse
3. Open "Orleans Overview" dashboard
4. You should see real-time metrics flowing

### Step 5: Generate Test Load
```bash
# Run test script to generate metrics
./scripts/monitoring/generate-test-metrics.sh

# Or use curl to hit API endpoints
for i in {1..100}; do
  curl -X POST http://localhost:5000/api/v1/bets \
    -H "Content-Type: application/json" \
    -d '{"amount": 50, "eventId": "test-001"}'
  sleep 0.1
done
```

---

## Architecture Overview {#architecture-overview}

### Component Architecture
```
┌─────────────────────────────────────────────────────────────┐
│                     Grafana (Port 3000)                      │
│                  Dashboards & Visualization                  │
└────────────────────────┬────────────────────────────────────┘
                         │ Queries
┌────────────────────────▼────────────────────────────────────┐
│                   Prometheus (Port 9091)                     │
│                    Time-Series Database                      │
└──┬───────────┬────────────┬───────────┬──────────┬─────────┘
   │           │            │           │          │
   │ Scrape    │ Scrape     │ Scrape    │ Scrape   │ Scrape
   │ :9090     │ :5000      │ :9187     │ :9121     │ :9100
┌──▼───────┐ ┌─▼────────┐ ┌▼────────┐ ┌▼──────┐ ┌─▼────────┐
│ Orleans  │ │   API    │ │Postgres │ │ Redis │ │   Node   │
│  Silo    │ │Endpoints │ │Exporter │ │Export │ │ Exporter │
└──────────┘ └──────────┘ └─────────┘ └───────┘ └──────────┘
```

### Data Flow
1. **Metrics Generation**: Applications expose metrics via HTTP endpoints
2. **Collection**: Prometheus scrapes metrics at configured intervals
3. **Storage**: Prometheus stores metrics in time-series database
4. **Visualization**: Grafana queries Prometheus for dashboard data
5. **Alerting**: AlertManager processes alerts from Prometheus rules

### Orleans Integration
- **Grain Metrics**: Automatic instrumentation via `GrainInstrumentationFilter`
- **Silo Metrics**: Built-in Orleans telemetry consumers
- **Cluster Health**: Membership and connectivity monitoring

---

## Installation Guide {#installation-guide}

### Development Environment

#### Using Docker Compose
```bash
# 1. Navigate to project root
cd /home/willian/Repos/sportsbook-lite

# 2. Start infrastructure services
docker-compose -f docker/docker-compose.yml up -d postgres redis pulsar

# 3. Start monitoring stack
docker-compose -f docker/docker-compose.monitoring.yml up -d

# 4. Start application services
docker-compose -f docker/docker-compose.yml up -d orleans-silo sportsbook-api

# 5. Verify all services
docker-compose ps
```

#### Manual Installation
```bash
# Install Prometheus
wget https://github.com/prometheus/prometheus/releases/download/v2.47.0/prometheus-2.47.0.linux-amd64.tar.gz
tar xvf prometheus-2.47.0.linux-amd64.tar.gz
cd prometheus-2.47.0.linux-amd64
./prometheus --config.file=/path/to/prometheus.yml

# Install Grafana
wget https://dl.grafana.com/enterprise/release/grafana-enterprise-10.2.0.linux-amd64.tar.gz
tar -zxvf grafana-enterprise-10.2.0.linux-amd64.tar.gz
cd grafana-10.2.0
./bin/grafana-server
```

### Production Environment (Kubernetes)

#### Prerequisites
```bash
# Install Helm
curl https://raw.githubusercontent.com/helm/helm/main/scripts/get-helm-3 | bash

# Add Prometheus community charts
helm repo add prometheus-community https://prometheus-community.github.io/helm-charts
helm repo add grafana https://grafana.github.io/helm-charts
helm repo update
```

#### Deploy Monitoring Stack
```bash
# Create monitoring namespace
kubectl create namespace monitoring

# Install Prometheus Operator
helm install prometheus prometheus-community/kube-prometheus-stack \
  --namespace monitoring \
  --values k8s/monitoring/prometheus-values.yaml

# Install additional exporters
kubectl apply -f k8s/monitoring/exporters/

# Verify deployment
kubectl get pods -n monitoring
```

#### Configure Service Monitors
```yaml
# k8s/monitoring/servicemonitors/orleans-silo.yaml
apiVersion: monitoring.coreos.com/v1
kind: ServiceMonitor
metadata:
  name: orleans-silo
  namespace: monitoring
spec:
  selector:
    matchLabels:
      app: orleans-silo
  endpoints:
  - port: metrics
    interval: 15s
    path: /metrics
```

---

## Configuration Reference {#configuration-reference}

### Environment Variables

#### Orleans Silo
```bash
# Monitoring configuration
METRICS_ENABLED=true
METRICS_PORT=9090
PROMETHEUS_PUSHGATEWAY_URL=http://pushgateway:9091

# Telemetry settings
ORLEANS_TELEMETRY_CONSUMERS=Prometheus
ORLEANS_DASHBOARD_PORT=8081
```

#### API Service
```bash
# Metrics configuration
ASPNETCORE_METRICS_ENABLED=true
METRICS_ENDPOINT=/metrics
METRICS_PORT=5000

# Health check settings
HEALTHCHECK_ENABLED=true
HEALTHCHECK_ENDPOINT=/health
```

### Prometheus Configuration
```yaml
# prometheus.yml
global:
  scrape_interval: 15s
  evaluation_interval: 15s

scrape_configs:
  - job_name: 'orleans-silo'
    static_configs:
      - targets: ['orleans-silo:9090']
    metric_relabel_configs:
      - source_labels: [__name__]
        regex: 'orleans_.*'
        action: keep

  - job_name: 'sportsbook-api'
    static_configs:
      - targets: ['sportsbook-api:5000']
    metrics_path: '/metrics'
```

### Grafana Data Source
```json
{
  "name": "Prometheus",
  "type": "prometheus",
  "url": "http://prometheus:9090",
  "access": "proxy",
  "isDefault": true
}
```

---

## Metrics Catalog {#metrics-catalog}

### Orleans Metrics

| Metric Name | Type | Description | Labels |
|------------|------|-------------|--------|
| `orleans_grain_activations_total` | Counter | Total grain activations | grain_type, silo |
| `orleans_grain_deactivations_total` | Counter | Total grain deactivations | grain_type, silo, reason |
| `orleans_grain_method_duration_seconds` | Histogram | Grain method execution time | grain_type, method, status |
| `orleans_grain_method_calls_total` | Counter | Total grain method calls | grain_type, method, status |
| `orleans_active_grains` | Gauge | Number of active grains | grain_type, silo |
| `orleans_silo_status` | Gauge | Silo status (1=active, 0=inactive) | silo_address, cluster_id |
| `orleans_cluster_membership` | Gauge | Number of silos in cluster | status, cluster_id |

### Business Metrics

| Metric Name | Type | Description | Labels |
|------------|------|-------------|--------|
| `sportsbook_bets_placed_total` | Counter | Total bets placed | status, event_type, sport, market_type |
| `sportsbook_bet_amount` | Histogram | Distribution of bet amounts | currency, event_type, sport |
| `sportsbook_bets_settled_total` | Counter | Total bets settled | result, event_type, sport |
| `sportsbook_settlement_duration_seconds` | Histogram | Settlement processing time | event_type, batch_size |
| `sportsbook_odds_changes_total` | Counter | Total odds changes | market_id, event_type, trigger |
| `sportsbook_current_odds` | Gauge | Current odds values | market_id, selection, event_id |
| `sportsbook_wallet_balance` | Gauge | Current wallet balance | user_id, currency |
| `sportsbook_wallet_transactions_total` | Counter | Total wallet transactions | type, status, currency |
| `sportsbook_active_events` | Gauge | Number of active events | sport, competition, status |

### API Metrics

| Metric Name | Type | Description | Labels |
|------------|------|-------------|--------|
| `http_requests_total` | Counter | Total HTTP requests | method, endpoint, status_code |
| `http_request_duration_seconds` | Histogram | Request duration | method, endpoint |
| `http_requests_in_flight` | Gauge | Current requests being processed | method |
| `api_health_status` | Gauge | API health (1=healthy, 0=unhealthy) | - |

### Infrastructure Metrics

| Metric Name | Type | Description | Labels |
|------------|------|-------------|--------|
| `pg_up` | Gauge | PostgreSQL availability | - |
| `pg_database_size_bytes` | Gauge | Database size | database |
| `redis_up` | Gauge | Redis availability | - |
| `redis_connected_clients` | Gauge | Connected Redis clients | - |
| `node_cpu_seconds_total` | Counter | CPU usage | cpu, mode |
| `node_memory_MemAvailable_bytes` | Gauge | Available memory | - |

---

## Dashboard User Guide {#dashboard-user-guide}

### Available Dashboards

#### 1. Orleans Overview
**Purpose**: Monitor Orleans cluster health and grain performance

**Key Panels**:
- **Cluster Health**: Overall cluster status and silo membership
- **Grain Activations**: Rate of grain activations/deactivations
- **Method Performance**: P50/P95/P99 latencies for grain methods
- **Active Grains**: Current active grain count by type
- **Message Flow**: Inter-silo communication metrics

**Usage Tips**:
- Use time range selector to focus on specific incidents
- Filter by grain_type to investigate specific grain performance
- Check activation failures during high load periods

#### 2. Business Metrics
**Purpose**: Track business KPIs and betting operations

**Key Panels**:
- **Betting Volume**: Total bets and monetary volume
- **Success Rate**: Bet placement success/failure ratios
- **Settlement Performance**: Average settlement times
- **Odds Volatility**: Frequency of odds changes
- **Top Events**: Most popular events by bet count
- **Revenue Metrics**: Gross revenue and profit margins

**Usage Tips**:
- Compare weekday vs weekend patterns
- Monitor settlement delays during peak events
- Track odds stability for risk management

#### 3. API Performance
**Purpose**: Monitor API health and performance

**Key Panels**:
- **Request Rate**: Requests per second by endpoint
- **Response Times**: P50/P95/P99 latencies
- **Error Rate**: 4xx and 5xx error percentages
- **Endpoint Performance**: Slowest endpoints
- **Current Load**: Active concurrent requests

**Usage Tips**:
- Set alerts for error rate spikes
- Identify slow endpoints for optimization
- Monitor during deployments for issues

### Dashboard Navigation

#### Time Range Selection
```
Last 5 minutes  → Real-time monitoring
Last 1 hour     → Recent performance
Last 24 hours   → Daily patterns
Last 7 days     → Weekly trends
Custom range    → Incident investigation
```

#### Variables and Filters
- **Cluster**: Filter by Orleans cluster ID
- **Silo**: Filter by specific silo instance
- **Event Type**: Filter business metrics by event type
- **Endpoint**: Filter API metrics by endpoint

#### Drill-Down Capabilities
1. Click on panel title → View → Edit to modify queries
2. Click on legend items to isolate specific series
3. Use Explore view for ad-hoc queries
4. Link to logs for detailed investigation

---

## Alerting Playbook {#alerting-playbook}

### Critical Alerts

#### ALERT: Orleans Cluster Down
**Trigger**: No active silos for >2 minutes
**Impact**: Complete service outage
**Response**:
```bash
# 1. Check silo status
kubectl get pods -l app=orleans-silo

# 2. Check recent logs
kubectl logs -l app=orleans-silo --tail=100

# 3. Restart failed silos
kubectl rollout restart statefulset/orleans-silo

# 4. Monitor cluster reformation
watch kubectl get pods -l app=orleans-silo
```

#### ALERT: High Bet Failure Rate
**Trigger**: Bet failure rate >5% for 5 minutes
**Impact**: Revenue loss, poor user experience
**Response**:
```bash
# 1. Check error patterns
curl -G http://prometheus:9090/api/v1/query \
  --data-urlencode 'query=rate(sportsbook_bets_placed_total{status="failed"}[5m])'

# 2. Check database connectivity
kubectl exec -it postgres-0 -- pg_isready

# 3. Review recent deployments
kubectl rollout history deployment/sportsbook-api

# 4. Scale up if load-related
kubectl scale deployment/sportsbook-api --replicas=5
```

#### ALERT: Database Connection Pool Exhausted
**Trigger**: Available connections <10%
**Impact**: Service degradation, timeouts
**Response**:
```bash
# 1. Check current connections
kubectl exec -it postgres-0 -- psql -U sportsbook -c \
  "SELECT count(*) FROM pg_stat_activity;"

# 2. Kill idle connections
kubectl exec -it postgres-0 -- psql -U sportsbook -c \
  "SELECT pg_terminate_backend(pid) FROM pg_stat_activity 
   WHERE state = 'idle' AND state_change < now() - interval '10 minutes';"

# 3. Increase pool size (temporary)
kubectl set env deployment/orleans-silo MAX_POOL_SIZE=200

# 4. Investigate connection leaks
kubectl logs -l app=orleans-silo --since=1h | grep -i "connection"
```

### Warning Alerts

#### ALERT: High API Latency
**Trigger**: P99 latency >2s for 10 minutes
**Impact**: Poor user experience
**Response**:
```bash
# 1. Identify slow endpoints
curl -G http://prometheus:9090/api/v1/query \
  --data-urlencode 'query=histogram_quantile(0.99, http_request_duration_seconds_bucket)'

# 2. Check resource usage
kubectl top pods -l app=sportsbook-api

# 3. Enable detailed logging
kubectl set env deployment/sportsbook-api LOG_LEVEL=Debug

# 4. Profile if needed
dotnet-trace collect --process-id $(pidof dotnet) --duration 00:00:30
```

#### ALERT: Memory Pressure
**Trigger**: Memory usage >80% for 15 minutes
**Impact**: Potential OOM kills
**Response**:
```bash
# 1. Check memory usage
kubectl top pods --sort-by=memory

# 2. Trigger garbage collection
kubectl exec -it orleans-silo-0 -- kill -USR1 1

# 3. Check for memory leaks
kubectl exec -it orleans-silo-0 -- dotnet-dump collect

# 4. Scale horizontally if needed
kubectl scale statefulset/orleans-silo --replicas=5
```

---

## Troubleshooting Guide {#troubleshooting-guide}

### Common Issues

#### Prometheus Not Scraping Metrics
**Symptoms**: No data in Grafana dashboards
**Diagnosis**:
```bash
# Check Prometheus targets
curl http://localhost:9091/targets

# Test metric endpoint directly
curl http://orleans-silo:9090/metrics

# Check Prometheus logs
docker logs prometheus
```
**Solution**:
1. Verify network connectivity
2. Check firewall rules
3. Ensure metrics endpoints are exposed
4. Validate Prometheus configuration

#### Grafana Dashboard Shows "No Data"
**Symptoms**: Panels display "No data" message
**Diagnosis**:
```bash
# Test data source
curl -X POST http://localhost:3000/api/datasources/1/query \
  -H "Content-Type: application/json" \
  -d '{"queries":[{"expr":"up"}]}'

# Check time range
# Ensure correct time zone settings
```
**Solution**:
1. Verify data source configuration
2. Check query syntax
3. Adjust time range
4. Clear browser cache

#### High Cardinality Metrics
**Symptoms**: Prometheus memory usage growing rapidly
**Diagnosis**:
```bash
# Check cardinality
curl http://localhost:9091/api/v1/label/__name__/values | jq '. | length'

# Find high cardinality metrics
curl -G http://localhost:9091/api/v1/query \
  --data-urlencode 'query=prometheus_tsdb_symbol_table_size_bytes'
```
**Solution**:
1. Review label usage
2. Implement metric_relabel_configs
3. Drop unnecessary labels
4. Use recording rules for aggregation

#### Orleans Grain Metrics Missing
**Symptoms**: No grain-specific metrics appearing
**Diagnosis**:
```bash
# Check grain filter registration
grep -i "GrainInstrumentationFilter" /app/logs/*.log

# Verify Orleans telemetry
curl http://orleans-silo:8081/DashboardCounters
```
**Solution**:
1. Ensure GrainInstrumentationFilter is registered
2. Check Orleans telemetry configuration
3. Verify grain inheritance from BaseInstrumentedGrain
4. Restart Orleans silo

---

## Performance Tuning {#performance-tuning}

### Prometheus Optimization

#### Storage Configuration
```yaml
# prometheus.yml
storage.tsdb.retention.time: 30d
storage.tsdb.retention.size: 50GB
storage.tsdb.wal-compression: true
```

#### Query Optimization
```yaml
# Recording rules for expensive queries
groups:
  - name: aggregations
    interval: 30s
    rules:
      - record: job:orleans_grain_method_duration:p99
        expr: histogram_quantile(0.99, rate(orleans_grain_method_duration_seconds_bucket[5m]))
```

### Grafana Optimization

#### Dashboard Best Practices
1. **Limit Panel Count**: Keep <20 panels per dashboard
2. **Use Variables**: Reduce duplicate queries
3. **Set Refresh Intervals**: Avoid "auto" refresh
4. **Cache Results**: Enable query caching

#### Query Optimization
```promql
# Bad: Queries all data
sum(rate(http_requests_total[5m]))

# Good: Pre-filters data
sum(rate(http_requests_total{job="api"}[5m]))
```

### Metric Collection Optimization

#### Reduce Cardinality
```csharp
// Bad: User ID as label (high cardinality)
WalletBalance.WithLabels(userId, currency).Set(balance);

// Good: Aggregate by currency only
WalletBalanceTotal.WithLabels(currency).Inc(balance);
```

#### Batch Operations
```csharp
// Use batch updates for multiple metrics
using (var timer = GrainMethodDuration.NewTimer())
{
    // Perform operation
    await ProcessBetAsync();
    
    // Update multiple metrics together
    BetsPlaced.Inc();
    BetAmount.Observe(amount);
    ActiveBets.Inc();
}
```

### Resource Allocation

#### Container Resources
```yaml
# Prometheus
resources:
  requests:
    memory: 2Gi
    cpu: 1
  limits:
    memory: 8Gi
    cpu: 4

# Grafana
resources:
  requests:
    memory: 512Mi
    cpu: 100m
  limits:
    memory: 2Gi
    cpu: 1
```

---

## Best Practices {#best-practices}

### Metric Design

#### DO's
✅ Use descriptive metric names
✅ Keep cardinality under control
✅ Use appropriate metric types
✅ Document metrics thoroughly
✅ Version metric changes

#### DON'Ts
❌ Use unbounded labels (user IDs, request IDs)
❌ Create metrics in hot paths without caching
❌ Mix metric types (counter vs gauge)
❌ Ignore metric lifecycle
❌ Forget error handling

### Dashboard Design

#### Principles
1. **Purpose-Driven**: Each dashboard serves specific audience
2. **Progressive Disclosure**: Overview → Details
3. **Consistent Layout**: Similar panels in same positions
4. **Clear Naming**: Self-explanatory panel titles
5. **Actionable Insights**: Include remediation hints

#### Layout Guidelines
```
┌─────────────────────────────────────┐
│         Executive Summary           │
├──────────┬──────────┬───────────────┤
│  KPI 1   │  KPI 2   │    KPI 3      │
├──────────┴──────────┴───────────────┤
│        Time Series Trends           │
├─────────────────────────────────────┤
│        Detailed Breakdown           │
└─────────────────────────────────────┘
```

### Alerting Strategy

#### Alert Quality
1. **Actionable**: Clear response procedure
2. **Urgent**: Requires immediate attention
3. **Unique**: No duplicate alerts
4. **Tested**: Validated in staging
5. **Documented**: Runbook available

#### Alert Fatigue Prevention
- Set appropriate thresholds
- Use alert grouping
- Implement silence periods
- Regular alert review
- Remove obsolete alerts

### Security Considerations

#### Access Control
```yaml
# Grafana teams and permissions
- team: developers
  dashboards: [view, edit]
  datasources: [view]
  
- team: operations
  dashboards: [admin]
  datasources: [admin]
```

#### Sensitive Data
- Never expose PII in metrics
- Aggregate user data
- Use hashed identifiers
- Implement data retention policies
- Audit metric access

### Maintenance Schedule

#### Daily
- Check critical alerts
- Review error rates
- Verify data collection

#### Weekly
- Review dashboard usage
- Check cardinality growth
- Update documentation
- Test alert responses

#### Monthly
- Analyze metric trends
- Optimize slow queries
- Review and tune alerts
- Update runbooks
- Capacity planning

---

## Appendix

### Useful Commands

#### Prometheus Queries
```promql
# Top 5 slowest grain methods
topk(5, histogram_quantile(0.99, rate(orleans_grain_method_duration_seconds_bucket[5m])))

# Bet success rate
rate(sportsbook_bets_placed_total{status="success"}[5m]) / rate(sportsbook_bets_placed_total[5m])

# Memory usage percentage
100 * (1 - node_memory_MemAvailable_bytes / node_memory_MemTotal_bytes)
```

#### Grafana API
```bash
# Export dashboard
curl -H "Authorization: Bearer $GRAFANA_TOKEN" \
  http://localhost:3000/api/dashboards/uid/orleans-overview > dashboard.json

# Create snapshot
curl -X POST -H "Authorization: Bearer $GRAFANA_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"dashboard": {...}, "expires": 3600}' \
  http://localhost:3000/api/snapshots
```

#### Troubleshooting
```bash
# Check metric cardinality
curl -s http://localhost:9091/api/v1/label/__name__/values | jq -r '.data[]' | while read metric; do
  echo -n "$metric: "
  curl -s http://localhost:9091/api/v1/query?query=$metric | jq '.data.result | length'
done | sort -t: -k2 -rn | head -20

# Find memory usage by metric
curl -s http://localhost:9091/api/v1/status/tsdb | jq '.data.seriesCountByMetricName' | head -20
```

### References

- [Prometheus Documentation](https://prometheus.io/docs/)
- [Grafana Documentation](https://grafana.com/docs/)
- [Orleans Monitoring](https://dotnet.github.io/orleans/docs/host/monitoring/)
- [PromQL Examples](https://prometheus.io/docs/prometheus/latest/querying/examples/)
- [Grafana Best Practices](https://grafana.com/docs/grafana/latest/best-practices/)

---

## Support

For issues or questions:
1. Check the troubleshooting guide
2. Review logs in `/var/log/monitoring/`
3. Contact the platform team
4. Create an issue in the repository

---

*Last Updated: 2024*
*Version: 1.0.0*