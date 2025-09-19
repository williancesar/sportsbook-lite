# Sportsbook-Lite Monitoring & Observability

## Overview

The Sportsbook-Lite project now includes a comprehensive monitoring and observability stack using **Prometheus**, **Grafana**, and **AlertManager**. This implementation provides complete visibility into the Orleans distributed system, API performance, and business metrics.

## ✅ What Has Been Implemented

### 1. **Metrics Infrastructure**
- ✅ Prometheus metrics collection configured for Orleans Silo and API
- ✅ Custom business metrics for betting operations
- ✅ Orleans-specific grain and cluster metrics
- ✅ .NET runtime metrics (GC, threads, memory)
- ✅ Infrastructure metrics (PostgreSQL, Redis, Pulsar)

### 2. **Visualization**
- ✅ 3 production-ready Grafana dashboards:
  - **Orleans Overview**: Cluster health, grain performance, silo status
  - **Business Metrics**: Betting KPIs, revenue, user activity
  - **API Performance**: Request rates, latencies, error tracking
- ✅ Real-time metrics with 15-second refresh intervals
- ✅ Interactive filtering with dashboard variables

### 3. **Alerting**
- ✅ 40+ pre-configured alert rules
- ✅ Multi-channel routing (Email, Slack, PagerDuty)
- ✅ Severity-based escalation
- ✅ Comprehensive runbooks for each alert

### 4. **Deployment Configurations**
- ✅ Docker Compose for local development
- ✅ Kubernetes manifests for production
- ✅ Service discovery for dynamic Orleans silos
- ✅ Persistent storage for metrics and dashboards

### 5. **Documentation**
- ✅ 30,000+ word comprehensive guide
- ✅ Architecture documentation
- ✅ C# implementation patterns
- ✅ DevOps infrastructure guides
- ✅ Dashboard design specifications

## 🚀 Quick Start

### Local Development (5 minutes)

```bash
# 1. Start the monitoring stack
docker-compose -f docker/docker-compose.monitoring.yml up -d

# 2. Start the application
dotnet run --project src/SportsbookLite.Host &
dotnet run --project src/SportsbookLite.Api

# 3. Generate test metrics
./scripts/monitoring/generate-test-metrics.sh

# 4. Access dashboards
open http://localhost:3000  # Grafana (admin/admin)
```

### Production Deployment

```bash
# Deploy to Kubernetes
kubectl apply -f k8s/monitoring/

# Verify deployment
kubectl get pods -n monitoring
```

## 📊 Available Metrics

### Orleans Metrics
- `orleans_grain_activations_total` - Grain lifecycle tracking
- `orleans_grain_method_duration_seconds` - Method performance
- `orleans_cluster_membership` - Cluster health
- `orleans_silo_status` - Silo availability

### Business Metrics
- `sportsbook_bets_placed_total` - Betting volume
- `sportsbook_bet_amount` - Bet size distribution
- `sportsbook_settlement_duration_seconds` - Settlement performance
- `sportsbook_wallet_balance` - User wallet tracking
- `sportsbook_active_events` - Live event monitoring

### API Metrics
- `http_requests_total` - Request counting
- `http_request_duration_seconds` - Latency tracking
- `api_health_status` - Service health
- `dotnet_*` - Runtime metrics (GC, memory, threads)

## 🎯 Key Features

### For Developers
- Grain-level performance monitoring
- Method execution tracing
- Error rate tracking
- Resource usage visibility

### For Operations
- Cluster health monitoring
- Alert-based incident response
- Capacity planning metrics
- SLA compliance tracking

### For Business
- Real-time betting analytics
- Revenue and margin tracking
- User engagement metrics
- Event popularity analysis

## 📁 Project Structure

```
monitoring/
├── src/SportsbookLite.Infrastructure/Metrics/
│   ├── BusinessMetrics.cs      # Business KPI metrics
│   └── OrleansMetrics.cs       # Orleans-specific metrics
├── docker/monitoring/
│   ├── prometheus/              # Prometheus configuration
│   ├── grafana/                 # Grafana dashboards
│   └── alertmanager/            # Alert routing
├── k8s/monitoring/              # Kubernetes manifests
├── scripts/monitoring/          # Utility scripts
└── docs/agent-work/             # Detailed documentation
```

## 🔧 Configuration

### Environment Variables

```bash
# Orleans Silo
METRICS_ENABLED=true
METRICS_PORT=9090

# API
ASPNETCORE_METRICS_ENABLED=true
METRICS_ENDPOINT=/metrics
```

### Custom Metrics Usage

```csharp
// Record a bet placement
BusinessMetrics.RecordBetPlacement(
    status: "success",
    eventType: "match",
    sport: "football",
    marketType: "winner",
    amount: 100m,
    currency: "USD"
);

// Track grain performance
using (var timer = OrleansMetrics.GrainMethodDuration
    .WithLabels("BetGrain", "PlaceBet", "success")
    .NewTimer())
{
    await ProcessBetAsync();
}
```

## 📈 Dashboards

### Orleans Overview
![Orleans Dashboard](docs/images/orleans-dashboard.png)
- Cluster membership status
- Grain activation rates
- Method latency percentiles
- Inter-silo messaging

### Business Metrics
![Business Dashboard](docs/images/business-dashboard.png)
- Betting volume and revenue
- Settlement performance
- Top events and markets
- User activity patterns

### API Performance
![API Dashboard](docs/images/api-dashboard.png)
- Request rates by endpoint
- Response time distribution
- Error rate tracking
- Resource utilization

## 🚨 Alerting

### Critical Alerts
- Orleans cluster down
- High bet failure rate (>5%)
- Database connection exhaustion
- Service unavailability

### Warning Alerts
- High API latency (P99 >2s)
- Memory pressure (>80%)
- Disk space low (<20%)
- Pulsar lag increasing

## 🛠️ Troubleshooting

### Common Issues

**No metrics appearing:**
```bash
# Check metric endpoints
curl http://localhost:9090/metrics  # Orleans
curl http://localhost:5000/metrics  # API

# Verify Prometheus targets
curl http://localhost:9091/targets
```

**High memory usage:**
```bash
# Check metric cardinality
curl http://localhost:9091/api/v1/label/__name__/values | wc -l

# Review high cardinality metrics
curl http://localhost:9091/api/v1/status/tsdb
```

## 📚 Documentation

- [Complete Monitoring Guide](docs/agent-work/monitoring-complete-guide.md) - Comprehensive 30,000+ word guide
- [Architecture Plan](docs/agent-work/monitoring-architecture-plan.md) - System design and architecture
- [C# Implementation](docs/agent-work/metrics-implementation-csharp.md) - Code patterns and examples
- [DevOps Infrastructure](docs/agent-work/monitoring-infrastructure-devops.md) - Deployment configurations
- [Dashboard Designs](docs/agent-work/grafana-dashboard-designs.md) - Dashboard specifications

## 🔄 Next Steps

1. **Test Locally**: Run the monitoring stack and generate test metrics
2. **Customize Dashboards**: Modify dashboards for your specific needs
3. **Configure Alerts**: Adjust thresholds based on your SLAs
4. **Deploy to Production**: Use Kubernetes manifests for production deployment
5. **Monitor and Iterate**: Continuously improve based on insights

## 🤝 Contributing

To add new metrics:
1. Define metrics in `BusinessMetrics.cs` or `OrleansMetrics.cs`
2. Instrument your code to record metrics
3. Update Prometheus scrape configuration if needed
4. Create or update Grafana dashboards
5. Add corresponding alerts if applicable

## 📞 Support

For monitoring-related issues:
1. Check the [Troubleshooting Guide](docs/agent-work/monitoring-complete-guide.md#troubleshooting-guide)
2. Review application logs
3. Check Prometheus/Grafana logs
4. Contact the platform team

---

**Monitoring Stack Version**: 1.0.0  
**Last Updated**: December 2024  
**Maintained By**: Platform Team