# Sportsbook-Lite Grafana Dashboards

This directory contains production-ready Grafana dashboard JSON files for monitoring the Sportsbook-Lite distributed betting system.

## Dashboard Overview

### 1. Orleans Overview (`orleans-overview.json`)
**Primary Focus**: Microsoft Orleans cluster and grain performance monitoring

**Key Panels**:
- **Cluster Health**: Real-time silo status and cluster membership
- **Grain Metrics**: Activation rates, method call performance, and active grain counts
- **Resource Usage**: CPU and memory consumption per silo
- **Inter-Silo Communication**: Message passing rates and patterns
- **Performance**: Method execution times with 95th/50th percentile breakdowns

**Variables**:
- `cluster_id`: Filter by Orleans cluster
- `silo_id`: Multi-select silo instances

**Alerts/Thresholds**:
- Active silos: Red < 1, Yellow = 1, Green ≥ 3
- CPU usage: Yellow > 70%, Red > 90%
- Memory usage: Yellow > 70%, Red > 85%

### 2. Business Metrics (`business-metrics.json`)
**Primary Focus**: Business KPIs and betting operations

**Key Panels**:
- **Daily KPIs**: Bets placed, volume, active events, and users (24h/1h periods)
- **Real-time Rates**: Bet placement, volume, odds updates by event type
- **Event Management**: Live event status table with volume breakdowns
- **Success Metrics**: Bet success rate, processing times
- **Financial**: Total customer wallet balance, transaction rates
- **Distribution Analysis**: Pie charts showing bet distribution by event types

**Variables**:
- `event_type`: Filter by sports/event categories
- `transaction_type`: Filter wallet transaction types

**Business Intelligence**:
- Revenue tracking and volume analysis
- Customer engagement metrics
- Event popularity insights
- Risk management indicators

### 3. API Performance (`api-performance.json`)
**Primary Focus**: FastEndpoints API health and performance

**Key Panels**:
- **Health Overview**: Success rate, response times, request rate, error rate
- **Endpoint Analysis**: Performance breakdown by API endpoint
- **Status Codes**: HTTP response distribution and error tracking  
- **System Resources**: .NET-specific metrics (GC, thread pools, memory)
- **Load Distribution**: Request patterns and throughput analysis

**Variables**:
- `endpoint`: Multi-select API endpoints
- `instance`: Multi-select API server instances

**Performance Thresholds**:
- Success rate: Red < 95%, Yellow < 99%, Green ≥ 99%
- Response time: Green < 100ms, Yellow < 500ms, Red ≥ 500ms
- CPU usage: Yellow > 70%, Red > 90%

## Metrics Expected

### Orleans Metrics (via Orleans.TelemetryConsumers.Prometheus)
```
orleans_cluster_membership_active_silos
orleans_grain_activations_total
orleans_grain_method_calls_total
orleans_grain_method_duration_seconds_bucket
orleans_grain_active_count
orleans_silo_status
orleans_silo_cpu_usage_percent
orleans_silo_memory_usage_percent
orleans_messaging_sent_messages_total
orleans_messaging_received_messages_total
```

### Business Metrics (Custom Application Metrics)
```
sportsbook_bets_placed_total{event_type, status}
sportsbook_bet_amount_total{event_type}
sportsbook_active_events
sportsbook_active_users_1h
sportsbook_odds_updates_total{event_type}
sportsbook_wallet_transactions_total{transaction_type}
sportsbook_bet_failures_total
sportsbook_bets_attempted_total
sportsbook_bet_duration_seconds_bucket
sportsbook_wallet_balance_total
sportsbook_event_info{event_id, event_name, event_type, event_status}
```

### API Metrics (ASP.NET Core + FastEndpoints)
```
http_requests_total{endpoint, status_code, method}
http_request_duration_seconds_bucket{endpoint}
process_cpu_seconds_total
process_working_set_bytes
dotnet_collection_count_total{generation}
dotnet_threadpool_num_threads
dotnet_threadpool_queue_length
dotnet_threadpool_throughput_per_second
```

## Setup Instructions

### 1. Dashboard Provisioning
Dashboards are automatically provisioned when Grafana starts via the `dashboards.yml` configuration:

```yaml
# docker/monitoring/grafana/provisioning/dashboards.yml
apiVersion: 1
providers:
  - name: 'Sportsbook Dashboards'
    orgId: 1
    folder: 'Sportsbook-Lite'
    type: file
    options:
      path: /etc/grafana/provisioning/dashboards
```

### 2. Docker Compose Integration
Ensure your `docker-compose.monitoring.yml` includes the dashboard volume:

```yaml
services:
  grafana:
    image: grafana/grafana:latest
    volumes:
      - ./monitoring/grafana/provisioning:/etc/grafana/provisioning
      - ./monitoring/grafana/dashboards:/etc/grafana/provisioning/dashboards
```

### 3. Prometheus Data Source
Dashboards expect a Prometheus data source named `prometheus`. Configure in:
`docker/monitoring/grafana/provisioning/datasources/prometheus.yml`

## Customization

### Adding New Panels
1. Clone the JSON file
2. Generate new panel IDs (increment from highest existing)
3. Add appropriate Prometheus queries
4. Set thresholds and colors based on SLA requirements

### Custom Variables
Add template variables in the `templating.list` section:
```json
{
  "name": "custom_filter",
  "type": "query",
  "query": "label_values(metric_name, label_name)",
  "refresh": 1
}
```

### Alerting Integration
These dashboards are compatible with Grafana alerting. Key alert rules to consider:
- Orleans cluster health (silo count)
- API error rates > 5%
- Response times > 95th percentile thresholds
- Business metric anomalies (sudden drops in bet volume)

## Performance Considerations

### Query Optimization
- All queries use appropriate time ranges (`[5m]` for rates)
- Instant queries used for tables to reduce load
- Histogram quantiles pre-calculated for performance

### Refresh Rates
- Default: 30-second refresh (production appropriate)
- Consider reducing to 10s for development environments
- Increase to 1m for historical analysis

### Variable Scope
- Variables use label_values() for dynamic population
- Multi-select enabled where appropriate for comparative analysis
- `$__all` option provided for overview perspectives

## Monitoring Best Practices

### Dashboard Organization
1. **Orleans Overview**: Infrastructure and platform health first
2. **Business Metrics**: Customer-facing KPIs and revenue
3. **API Performance**: User experience and technical SLAs

### Key Monitoring Workflows
1. **Incident Response**: Start with Orleans Overview for cluster health
2. **Business Review**: Use Business Metrics for daily/weekly reporting  
3. **Performance Optimization**: API Performance for bottleneck identification

### Alert Strategy
- **Critical**: Cluster health, API availability (< 99%)
- **Warning**: Performance degradation, unusual business patterns
- **Info**: Capacity planning metrics, trend analysis

## Troubleshooting

### Missing Data
1. Verify Prometheus scraping configuration
2. Check application metrics exposition endpoints
3. Ensure Orleans telemetry consumers are registered
4. Validate label consistency in custom metrics

### Performance Issues
1. Reduce query time ranges if dashboards load slowly
2. Use recording rules for complex calculations
3. Consider separate dashboards for detailed analysis
4. Implement proper data retention policies

### Dashboard Updates
- Dashboards support live editing when `allowUiUpdates: true`
- Export JSON after modifications for version control
- Test changes in development environment first
- Consider using Grafana API for programmatic updates

## Integration with CI/CD

These dashboards can be automatically deployed as part of your infrastructure:

```bash
# Validate dashboard JSON syntax
jq empty *.json

# Deploy via Grafana API
curl -X POST \
  -H "Content-Type: application/json" \
  -d @orleans-overview.json \
  http://admin:admin@localhost:3000/api/dashboards/db
```

## Version Compatibility

- **Grafana**: 9.0+ (uses latest panel types and features)
- **Prometheus**: 2.30+ (histogram quantile functions)
- **Orleans**: 8.0+ (telemetry consumer metrics)
- **ASP.NET Core**: 8.0+ (built-in metrics)