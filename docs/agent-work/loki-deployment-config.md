# Grafana Loki Deployment Configuration Guide

## Overview

This guide provides comprehensive deployment configurations for Grafana Loki in the SportsbookLite infrastructure. Loki serves as our centralized log aggregation system, collecting logs from Orleans Silo, FastEndpoints API, and other infrastructure components.

## Architecture

```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   Applications  │    │    Promtail     │    │      Loki       │
│                 │───▶│  (Log Shipper)  │───▶│ (Log Storage)   │
│ • Orleans Silo  │    │                 │    │                 │
│ • API           │    │                 │    │                 │
│ • Infrastructure│    │                 │    │                 │
└─────────────────┘    └─────────────────┘    └─────────────────┘
                                                        │
                                                        ▼
                                                ┌─────────────────┐
                                                │     Grafana     │
                                                │  (Visualization)│
                                                └─────────────────┘
```

## Local Development Setup (Docker Compose)

### 1. Start the Logging Stack

```bash
# Start with monitoring profile to include Grafana and Prometheus
docker-compose -f docker/docker-compose.yml --profile monitoring up -d loki promtail grafana prometheus

# Verify services are running
docker-compose ps
```

### 2. Service URLs

- **Loki**: http://localhost:3100
  - Health: http://localhost:3100/ready
  - Metrics: http://localhost:3100/metrics

- **Grafana**: http://localhost:3000
  - Username: admin
  - Password: admin123

- **Prometheus**: http://localhost:9090

### 3. Configuration Files

| File | Purpose | Location |
|------|---------|----------|
| `loki-config.yml` | Loki configuration | `/docker/loki-config.yml` |
| `promtail-config.yml` | Promtail configuration | `/docker/promtail-config.yml` |
| `prometheus.yml` | Prometheus configuration | `/docker/prometheus/prometheus.yml` |
| `datasources.yml` | Grafana datasources | `/docker/grafana/provisioning/datasources/` |

### 4. Volume Mounts

```yaml
volumes:
  loki_data:          # Loki chunks and index storage
  promtail_positions: # Promtail read positions
  grafana_data:       # Grafana dashboards and settings
  prometheus_data:    # Prometheus metrics storage
  orleans_logs:       # Orleans application logs
  api_logs:          # API application logs
```

## Production Deployment (Kubernetes)

### 1. Prerequisites

```bash
# Ensure namespace exists
kubectl apply -f k8s/namespace.yaml

# Verify storage class is available
kubectl get storageclass
```

### 2. Deploy Loki

```bash
# Deploy Loki with persistent storage
kubectl apply -f k8s/loki-deployment.yaml

# Verify deployment
kubectl get statefulset loki -n sportsbook-lite
kubectl get pods -l app.kubernetes.io/name=loki -n sportsbook-lite
```

### 3. Deploy Promtail

```bash
# Deploy Promtail DaemonSet
kubectl apply -f k8s/promtail-deployment.yaml

# Verify DaemonSet
kubectl get daemonset promtail -n sportsbook-lite
kubectl get pods -l app.kubernetes.io/name=promtail -n sportsbook-lite
```

### 4. Apply Network Policies

```bash
# Apply security policies
kubectl apply -f k8s/loki-network-policies.yaml

# Verify network policies
kubectl get networkpolicy -n sportsbook-lite
```

### 5. Update Monitoring Configuration

```bash
# Apply updated monitoring rules
kubectl apply -f k8s/monitoring.yaml

# Verify ServiceMonitors
kubectl get servicemonitor -n sportsbook-lite
```

## Configuration Details

### Loki Configuration

#### Local Development
- **Storage**: Local filesystem
- **Retention**: 7 days (168h)
- **Ingestion Rate**: 16MB/s
- **Replication Factor**: 1

#### Production
- **Storage**: S3-compatible (configurable)
- **Retention**: 90 days (2160h)
- **Ingestion Rate**: 64MB/s
- **Replication Factor**: 3 (for HA)

### Promtail Configuration

#### Log Sources
1. **Orleans Silo Logs**
   - Path: `/var/log/orleans/*.log`
   - Format: JSON (Serilog)
   - Labels: service_name, environment, orleans_cluster_id

2. **API Logs**
   - Path: `/var/log/api/*.log`
   - Format: JSON (Serilog)
   - Labels: service_name, http_method, http_status

3. **Container Logs** (Kubernetes)
   - Path: `/var/log/pods/*/*.log`
   - Format: Docker JSON
   - Labels: namespace, pod, container

### Resource Requirements

#### Loki

| Environment | CPU | Memory | Storage |
|-------------|-----|--------|---------|
| Development | 200m | 512Mi | 5Gi |
| Production | 500m-1000m | 1Gi-2Gi | 50Gi+ |

#### Promtail

| Environment | CPU | Memory | Storage |
|-------------|-----|--------|---------|
| Development | 50m | 64Mi | - |
| Production | 100m-200m | 128Mi-256Mi | - |

## Monitoring and Alerting

### Key Metrics

1. **Loki Ingestion Rate**: `rate(loki_ingester_samples_received_total[5m])`
2. **Storage Usage**: `loki_ingester_chunks_stored_total`
3. **Query Performance**: `loki_request_duration_seconds`
4. **Error Rate**: `rate(loki_request_duration_seconds_count{status_code=~"5.."}[5m])`

### Alert Rules

| Alert | Threshold | Duration | Severity |
|-------|-----------|----------|----------|
| LokiDown | Up == 0 | 1m | Critical |
| LokiHighIngestionRate | > 10,000 samples/s | 5m | Warning |
| LokiDiskSpaceUsage | > 80% | 5m | Warning |
| PromtailDown | Up == 0 | 2m | Warning |

### Dashboard Queries

```logql
# View all Orleans logs
{job="orleans_silo"}

# Filter by log level
{job="orleans_silo"} |= "level" |~ "(?i)(error|warning)"

# API request logs with status codes
{job="sportsbook_api"} |= "HTTP" | json | http_status > 400

# Correlation tracing
{job=~"orleans_silo|sportsbook_api"} |= "correlation_id=12345"

# Performance queries
{job="sportsbook_api"} | json | response_time_ms > 1000
```

## Backup and Disaster Recovery

### Backup Strategy

#### Development
```bash
# Backup Loki data
docker run --rm -v sportsbook-lite_loki_data:/source:ro \
  -v $(pwd)/backups:/backup alpine \
  tar czf /backup/loki-backup-$(date +%Y%m%d).tar.gz -C /source .
```

#### Production
```bash
# Using S3 storage backend (automatic)
# Configure lifecycle policies for:
# - Standard storage: 30 days
# - Infrequent Access: 90 days  
# - Glacier: 1 year+

# Manual backup of configuration
kubectl get configmap loki-config -n sportsbook-lite -o yaml > loki-config-backup.yaml
```

### Disaster Recovery

#### RTO/RPO Targets
- **RTO (Recovery Time Objective)**: 15 minutes
- **RPO (Recovery Point Objective)**: 5 minutes

#### Recovery Procedures

1. **Loki Failure**
   ```bash
   # Scale down StatefulSet
   kubectl scale statefulset loki --replicas=0 -n sportsbook-lite
   
   # Restore PVC from backup if needed
   kubectl delete pvc storage-loki-0 -n sportsbook-lite
   # Create new PVC from backup snapshot
   
   # Scale up StatefulSet
   kubectl scale statefulset loki --replicas=1 -n sportsbook-lite
   ```

2. **Data Corruption**
   ```bash
   # Stop ingestion
   kubectl scale daemonset promtail --replicas=0 -n sportsbook-lite
   
   # Restore from backup
   # Re-enable ingestion
   kubectl scale daemonset promtail --replicas=1 -n sportsbook-lite
   ```

## Troubleshooting Common Issues

### Issue: Loki Not Starting

**Symptoms**: 
- Pod in CrashLoopBackOff
- "permission denied" errors

**Solution**:
```bash
# Check permissions
kubectl logs loki-0 -n sportsbook-lite

# Fix volume permissions
kubectl patch statefulset loki -n sportsbook-lite -p '
{
  "spec": {
    "template": {
      "spec": {
        "securityContext": {
          "runAsUser": 10001,
          "runAsGroup": 10001,
          "fsGroup": 10001
        }
      }
    }
  }
}'
```

### Issue: High Memory Usage

**Symptoms**:
- Loki OOMKilled
- Slow query performance

**Solution**:
```bash
# Increase memory limits
kubectl patch statefulset loki -n sportsbook-lite -p '
{
  "spec": {
    "template": {
      "spec": {
        "containers": [
          {
            "name": "loki",
            "resources": {
              "limits": {"memory": "4Gi"},
              "requests": {"memory": "2Gi"}
            }
          }
        ]
      }
    }
  }
}'

# Reduce chunk size in config
# chunk_target_size: 512576  # Reduce from 1048576
```

### Issue: Promtail Not Collecting Logs

**Symptoms**:
- No logs in Grafana
- Promtail metrics show 0 entries

**Solution**:
```bash
# Check Promtail logs
kubectl logs -l app.kubernetes.io/name=promtail -n sportsbook-lite

# Verify file permissions
kubectl exec -it daemonset/promtail -n sportsbook-lite -- ls -la /var/log/pods

# Check service discovery
kubectl exec -it daemonset/promtail -n sportsbook-lite -- \
  curl http://localhost:3101/targets
```

### Issue: Logs Not Appearing in Grafana

**Symptoms**:
- Loki datasource configured
- No log data visible

**Solution**:
```bash
# Test Loki API directly
kubectl port-forward svc/loki 3100:3100 -n sportsbook-lite
curl "http://localhost:3100/loki/api/v1/labels"

# Check log retention
curl "http://localhost:3100/loki/api/v1/query_range?query={job=\"orleans_silo\"}&start=$(date -d '1 hour ago' -u +%s)000000000&end=$(date -u +%s)000000000"

# Verify Grafana datasource
# Grafana -> Configuration -> Data Sources -> Loki -> Test
```

### Issue: Storage Running Out of Space

**Symptoms**:
- Disk usage alerts
- Write failures

**Solution**:
```bash
# Check current usage
kubectl exec loki-0 -n sportsbook-lite -- df -h /loki

# Clean old data manually
kubectl exec loki-0 -n sportsbook-lite -- find /loki/chunks -mtime +7 -delete

# Update retention policy
kubectl patch configmap loki-config -n sportsbook-lite -p '
{
  "data": {
    "loki.yml": "...\ntable_manager:\n  retention_period: 72h\n..."
  }
}'

# Restart Loki to apply changes
kubectl rollout restart statefulset loki -n sportsbook-lite
```

### Issue: Poor Query Performance

**Symptoms**:
- Slow dashboard loading
- Query timeouts

**Solution**:
```bash
# Check query complexity
{job="orleans_silo"} |= "specific_string" # Use specific filters

# Avoid broad regex
{job="orleans_silo"} |~ ".*error.*" # Slow
{job="orleans_silo"} |= "error"    # Fast

# Enable caching (Redis-based)
# Update loki-config.yml:
query_range:
  results_cache:
    cache:
      redis:
        endpoint: redis:6379
```

## Performance Tuning

### Loki Optimizations

```yaml
# High-performance configuration
limits_config:
  ingestion_rate_mb: 128          # Increase for high-volume
  ingestion_burst_size_mb: 256
  max_concurrent_tail_requests: 50
  per_stream_rate_limit: 20MB
  
chunk_store_config:
  chunk_cache_config:
    embedded_cache:
      enabled: true
      max_size_mb: 2048            # Increase cache size

frontend:
  max_outstanding_per_tenant: 4096 # Increase parallelism
```

### Promtail Optimizations

```yaml
# Batch configuration
clients:
  - url: http://loki:3100/loki/api/v1/push
    batchwait: 500ms              # Reduce latency
    batchsize: 2097152            # Increase batch size
    timeout: 30s                  # Increase timeout
```

## Security Considerations

### Network Policies

- **Loki**: Only accessible by Promtail and Grafana
- **Promtail**: Can reach Loki and Kubernetes API
- **Default Deny**: All other traffic blocked

### Authentication (Optional)

```yaml
# Enable multi-tenancy
auth_enabled: true

# Configure authentication
server:
  http_listen_port: 3100
  grpc_listen_port: 9096
  
# Add authentication middleware
# This requires additional setup with auth proxy
```

### Data Encryption

```yaml
# TLS configuration
server:
  http_tls_config:
    cert_file: /etc/loki/tls/cert.pem
    key_file: /etc/loki/tls/key.pem
  grpc_tls_config:
    cert_file: /etc/loki/tls/cert.pem
    key_file: /etc/loki/tls/key.pem
```

## Scaling Guidelines

### Horizontal Scaling

```bash
# Scale Loki for high availability
kubectl patch statefulset loki -n sportsbook-lite -p '{"spec":{"replicas":3}}'

# Update memberlist configuration for clustering
# See loki-config-production.yml for cluster setup
```

### Vertical Scaling

```bash
# Increase resources based on load
kubectl patch statefulset loki -n sportsbook-lite -p '
{
  "spec": {
    "template": {
      "spec": {
        "containers": [
          {
            "name": "loki",
            "resources": {
              "limits": {"cpu": "2000m", "memory": "4Gi"},
              "requests": {"cpu": "1000m", "memory": "2Gi"}
            }
          }
        ]
      }
    }
  }
}'
```

## Maintenance Tasks

### Weekly

1. **Check disk usage**: `kubectl exec loki-0 -n sportsbook-lite -- df -h`
2. **Review log retention**: Ensure old logs are being purged
3. **Monitor alert rules**: Check for persistent alerts

### Monthly

1. **Update containers**: Check for security updates
2. **Backup configurations**: Export ConfigMaps and manifests
3. **Review performance**: Analyze query patterns and optimize

### Quarterly

1. **Capacity planning**: Project storage and compute needs
2. **Disaster recovery testing**: Verify backup/restore procedures
3. **Security audit**: Review access patterns and permissions

## Integration with CI/CD

### Automated Deployment

```yaml
# .github/workflows/deploy-loki.yml
name: Deploy Loki
on:
  push:
    paths: ['k8s/loki-*.yaml', 'docker/loki-*.yml']

jobs:
  deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - name: Deploy to Kubernetes
        run: |
          kubectl apply -f k8s/loki-deployment.yaml
          kubectl rollout status statefulset/loki -n sportsbook-lite
```

### Health Checks

```bash
#!/bin/bash
# health-check.sh

# Check Loki health
if ! curl -f http://loki:3100/ready; then
  echo "Loki health check failed"
  exit 1
fi

# Check Promtail targets
TARGETS=$(curl -s http://promtail:3101/targets | jq '.activeTargets | length')
if [ "$TARGETS" -eq 0 ]; then
  echo "No active Promtail targets"
  exit 1
fi

echo "All logging services healthy"
```

## Conclusion

This deployment guide provides a complete setup for Grafana Loki in both development and production environments. The configuration supports:

- **High availability** with clustering and replication
- **Security** with network policies and optional authentication
- **Performance** optimization for high-volume logging
- **Monitoring** with comprehensive metrics and alerting
- **Disaster recovery** with backup and restore procedures

For additional support or customization, refer to the [official Loki documentation](https://grafana.com/docs/loki/) or contact the DevOps team.