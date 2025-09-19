# Monitoring Infrastructure DevOps - Sportsbook-Lite

Complete DevOps infrastructure setup for Prometheus and Grafana monitoring in the Sportsbook-Lite distributed Orleans application.

## Table of Contents

1. [Architecture Overview](#architecture-overview)
2. [Docker Compose for Local Development](#docker-compose-for-local-development)
3. [Prometheus Configuration](#prometheus-configuration)
4. [Kubernetes Production Deployment](#kubernetes-production-deployment)
5. [AlertManager Configuration](#alertmanager-configuration)
6. [Service Discovery](#service-discovery)
7. [Persistent Storage](#persistent-storage)
8. [Network and Security Policies](#network-and-security-policies)
9. [Backup and Restore Procedures](#backup-and-restore-procedures)
10. [CI/CD Integration](#cicd-integration)
11. [Environment-Specific Configurations](#environment-specific-configurations)
12. [Deployment Commands](#deployment-commands)

## Architecture Overview

The monitoring infrastructure consists of:

- **Prometheus**: Metrics collection and time-series database
- **Grafana**: Visualization and dashboards
- **AlertManager**: Alert routing and notification
- **Node Exporter**: Host metrics collection
- **cAdvisor**: Container metrics collection
- **Orleans Metrics**: Custom Orleans grain and silo metrics
- **Redis Exporter**: Redis metrics collection
- **PostgreSQL Exporter**: Database metrics collection
- **Pulsar Exporter**: Message broker metrics collection

### Monitoring Stack Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                    Monitoring Infrastructure                     │
├─────────────────────────────────────────────────────────────────┤
│ Grafana (Visualization) ←→ Prometheus (Metrics) ←→ AlertManager │
│         ↑                          ↑                     ↑     │
│    Dashboards              Service Discovery           Alerts   │
└─────────────────────┬───────────────────────────────────────────┘
                      │
┌─────────────────────┼───────────────────────────────────────────┐
│             Application Layer                                   │
├─────────────────────┼───────────────────────────────────────────┤
│ Orleans API ←→ Orleans Silo ←→ Pulsar ←→ Redis ←→ PostgreSQL   │
│     ↓              ↓              ↓       ↓         ↓          │
│   /metrics     /metrics      /metrics  /metrics  /metrics      │
└─────────────────────────────────────────────────────────────────┘
```

## Docker Compose for Local Development

### Main Docker Compose with Monitoring

Create `docker/docker-compose.monitoring.yml`:

```yaml
version: '3.8'

services:
  # Prometheus - Metrics Collection
  prometheus:
    image: prom/prometheus:v2.48.0
    container_name: sportsbook-prometheus
    restart: unless-stopped
    command:
      - '--config.file=/etc/prometheus/prometheus.yml'
      - '--storage.tsdb.path=/prometheus'
      - '--storage.tsdb.retention.time=30d'
      - '--storage.tsdb.retention.size=10GB'
      - '--web.console.libraries=/etc/prometheus/console_libraries'
      - '--web.console.templates=/etc/prometheus/consoles'
      - '--web.enable-lifecycle'
      - '--web.enable-admin-api'
      - '--log.level=info'
    ports:
      - "9090:9090"
    volumes:
      - ./monitoring/prometheus/prometheus.yml:/etc/prometheus/prometheus.yml:ro
      - ./monitoring/prometheus/alerts:/etc/prometheus/alerts:ro
      - prometheus_data:/prometheus
    depends_on:
      - node-exporter
      - cadvisor
    networks:
      - sportsbook-network
      - monitoring-network
    healthcheck:
      test: ["CMD", "wget", "--no-verbose", "--tries=1", "--spider", "http://localhost:9090/-/healthy"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 30s

  # Grafana - Visualization
  grafana:
    image: grafana/grafana:10.2.0
    container_name: sportsbook-grafana
    restart: unless-stopped
    environment:
      - GF_SECURITY_ADMIN_PASSWORD=admin123
      - GF_USERS_ALLOW_SIGN_UP=false
      - GF_INSTALL_PLUGINS=grafana-piechart-panel,grafana-worldmap-panel
      - GF_RENDERING_SERVER_URL=http://renderer:8081/render
      - GF_RENDERING_CALLBACK_URL=http://grafana:3000/
      - GF_LOG_FILTERS=rendering:debug
      - GF_FEATURE_TOGGLES_ENABLE=publicDashboards
      - GF_ANALYTICS_REPORTING_ENABLED=false
      - GF_ANALYTICS_CHECK_FOR_UPDATES=false
    ports:
      - "3000:3000"
    volumes:
      - grafana_data:/var/lib/grafana
      - ./monitoring/grafana/provisioning:/etc/grafana/provisioning:ro
      - ./monitoring/grafana/dashboards:/var/lib/grafana/dashboards:ro
    depends_on:
      - prometheus
    networks:
      - monitoring-network
    healthcheck:
      test: ["CMD-SHELL", "wget --no-verbose --tries=1 --spider http://localhost:3000/api/health || exit 1"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 30s

  # AlertManager - Alert Management
  alertmanager:
    image: prom/alertmanager:v0.26.0
    container_name: sportsbook-alertmanager
    restart: unless-stopped
    command:
      - '--config.file=/etc/alertmanager/alertmanager.yml'
      - '--storage.path=/alertmanager'
      - '--web.external-url=http://localhost:9093'
      - '--cluster.advertise-address=0.0.0.0:9093'
    ports:
      - "9093:9093"
    volumes:
      - ./monitoring/alertmanager/alertmanager.yml:/etc/alertmanager/alertmanager.yml:ro
      - alertmanager_data:/alertmanager
    networks:
      - monitoring-network
    healthcheck:
      test: ["CMD", "wget", "--no-verbose", "--tries=1", "--spider", "http://localhost:9093/-/healthy"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 30s

  # Node Exporter - Host Metrics
  node-exporter:
    image: prom/node-exporter:v1.7.0
    container_name: sportsbook-node-exporter
    restart: unless-stopped
    command:
      - '--path.rootfs=/host'
      - '--collector.filesystem.mount-points-exclude=^/(sys|proc|dev|host|etc)($$|/)'
    ports:
      - "9100:9100"
    volumes:
      - /:/host:ro,rslave
    networks:
      - monitoring-network
    healthcheck:
      test: ["CMD", "wget", "--no-verbose", "--tries=1", "--spider", "http://localhost:9100/metrics"]
      interval: 30s
      timeout: 10s
      retries: 3

  # cAdvisor - Container Metrics
  cadvisor:
    image: gcr.io/cadvisor/cadvisor:v0.47.0
    container_name: sportsbook-cadvisor
    restart: unless-stopped
    privileged: true
    devices:
      - /dev/kmsg:/dev/kmsg
    volumes:
      - /:/rootfs:ro
      - /var/run:/var/run:ro
      - /sys:/sys:ro
      - /var/lib/docker:/var/lib/docker:ro
      - /cgroup:/cgroup:ro
    ports:
      - "8080:8080"
    networks:
      - monitoring-network
    healthcheck:
      test: ["CMD", "wget", "--no-verbose", "--tries=1", "--spider", "http://localhost:8080/healthz"]
      interval: 30s
      timeout: 10s
      retries: 3

  # Redis Exporter - Redis Metrics
  redis-exporter:
    image: oliver006/redis_exporter:v1.55.0
    container_name: sportsbook-redis-exporter
    restart: unless-stopped
    environment:
      - REDIS_ADDR=redis://redis:6379
      - REDIS_PASSWORD=dev123
    ports:
      - "9121:9121"
    depends_on:
      - redis
    networks:
      - sportsbook-network
      - monitoring-network
    healthcheck:
      test: ["CMD", "wget", "--no-verbose", "--tries=1", "--spider", "http://localhost:9121/metrics"]
      interval: 30s
      timeout: 10s
      retries: 3

  # PostgreSQL Exporter - Database Metrics
  postgres-exporter:
    image: prometheuscommunity/postgres-exporter:v0.15.0
    container_name: sportsbook-postgres-exporter
    restart: unless-stopped
    environment:
      - DATA_SOURCE_NAME=postgresql://dev:dev123@postgres:5432/sportsbook?sslmode=disable
      - PG_EXPORTER_EXTEND_QUERY_PATH=/etc/postgres_exporter/queries.yaml
    ports:
      - "9187:9187"
    volumes:
      - ./monitoring/postgres-exporter/queries.yaml:/etc/postgres_exporter/queries.yaml:ro
    depends_on:
      - postgres
    networks:
      - sportsbook-network
      - monitoring-network
    healthcheck:
      test: ["CMD", "wget", "--no-verbose", "--tries=1", "--spider", "http://localhost:9187/metrics"]
      interval: 30s
      timeout: 10s
      retries: 3

  # Grafana Image Renderer
  renderer:
    image: grafana/grafana-image-renderer:3.8.4
    container_name: sportsbook-grafana-renderer
    restart: unless-stopped
    environment:
      - ENABLE_METRICS=true
    ports:
      - "8081:8081"
    networks:
      - monitoring-network

# Additional volumes for monitoring data
volumes:
  prometheus_data:
    driver: local
  grafana_data:
    driver: local
  alertmanager_data:
    driver: local

# Additional network for monitoring isolation
networks:
  monitoring-network:
    driver: bridge
    ipam:
      config:
        - subnet: 172.21.0.0/16

# External networks from main compose
networks:
  sportsbook-network:
    external: true
```

### Override for Development

Create `docker/docker-compose.monitoring.override.yml`:

```yaml
version: '3.8'

services:
  # Development overrides for monitoring services
  grafana:
    environment:
      - GF_SECURITY_ADMIN_PASSWORD=admin  # Simpler password for dev
      - GF_LOG_LEVEL=debug
      - GF_EXPLORE_ENABLED=true
      - GF_ALERTING_ENABLED=false  # Disable alerting in dev
    volumes:
      - ./monitoring/grafana/dev-dashboards:/var/lib/grafana/dev-dashboards:ro

  prometheus:
    command:
      - '--config.file=/etc/prometheus/prometheus.yml'
      - '--storage.tsdb.path=/prometheus'
      - '--storage.tsdb.retention.time=7d'  # Shorter retention for dev
      - '--storage.tsdb.retention.size=1GB'
      - '--web.console.libraries=/etc/prometheus/console_libraries'
      - '--web.console.templates=/etc/prometheus/consoles'
      - '--web.enable-lifecycle'
      - '--web.enable-admin-api'
      - '--log.level=debug'

  alertmanager:
    volumes:
      - ./monitoring/alertmanager/dev-alertmanager.yml:/etc/alertmanager/alertmanager.yml:ro
```

## Prometheus Configuration

### Main Prometheus Configuration

Create `docker/monitoring/prometheus/prometheus.yml`:

```yaml
# Prometheus configuration for Sportsbook-Lite monitoring
global:
  scrape_interval: 15s
  scrape_timeout: 10s
  evaluation_interval: 30s
  external_labels:
    cluster: 'sportsbook-lite'
    environment: 'development'

# Rule files for alerting
rule_files:
  - "alerts/*.yml"

# Alertmanager configuration
alerting:
  alertmanagers:
    - static_configs:
        - targets:
          - alertmanager:9093

# Scrape configuration
scrape_configs:
  # Prometheus self-monitoring
  - job_name: 'prometheus'
    static_configs:
      - targets: ['localhost:9090']
    scrape_interval: 30s
    metrics_path: /metrics

  # Node Exporter - Host metrics
  - job_name: 'node-exporter'
    static_configs:
      - targets: ['node-exporter:9100']
    scrape_interval: 15s
    metrics_path: /metrics
    relabel_configs:
      - source_labels: [__address__]
        target_label: instance
        replacement: 'sportsbook-host'

  # cAdvisor - Container metrics
  - job_name: 'cadvisor'
    static_configs:
      - targets: ['cadvisor:8080']
    scrape_interval: 15s
    metrics_path: /metrics

  # Orleans Silo - Custom application metrics
  - job_name: 'orleans-silo'
    static_configs:
      - targets: ['orleans-silo:8080']
    scrape_interval: 10s
    metrics_path: /metrics
    relabel_configs:
      - source_labels: [__address__]
        target_label: orleans_service
        replacement: 'silo'
    metric_relabel_configs:
      - source_labels: [__name__]
        regex: 'orleans_(.+)'
        target_label: __name__
        replacement: 'sportsbook_orleans_${1}'

  # Orleans API - API metrics
  - job_name: 'sportsbook-api'
    static_configs:
      - targets: ['sportsbook-api:8080']
    scrape_interval: 10s
    metrics_path: /metrics
    relabel_configs:
      - source_labels: [__address__]
        target_label: orleans_service
        replacement: 'api'
    metric_relabel_configs:
      - source_labels: [__name__]
        regex: 'aspnetcore_(.+)'
        target_label: __name__
        replacement: 'sportsbook_api_${1}'

  # Redis metrics
  - job_name: 'redis'
    static_configs:
      - targets: ['redis-exporter:9121']
    scrape_interval: 15s
    metrics_path: /metrics
    relabel_configs:
      - source_labels: [__address__]
        target_label: redis_instance
        replacement: 'sportsbook-redis'

  # PostgreSQL metrics
  - job_name: 'postgres'
    static_configs:
      - targets: ['postgres-exporter:9187']
    scrape_interval: 15s
    metrics_path: /metrics
    relabel_configs:
      - source_labels: [__address__]
        target_label: postgres_instance
        replacement: 'sportsbook-postgres'

  # Pulsar metrics (if Pulsar exposes Prometheus metrics)
  - job_name: 'pulsar'
    static_configs:
      - targets: ['pulsar:8080']
    scrape_interval: 30s
    metrics_path: /metrics
    relabel_configs:
      - source_labels: [__address__]
        target_label: pulsar_instance
        replacement: 'sportsbook-pulsar'

  # Service Discovery for Orleans Silos (Kubernetes)
  - job_name: 'orleans-silo-discovery'
    kubernetes_sd_configs:
      - role: pod
        namespaces:
          names:
            - sportsbook-lite
    relabel_configs:
      - source_labels: [__meta_kubernetes_pod_label_app]
        action: keep
        regex: orleans-silo
      - source_labels: [__meta_kubernetes_pod_annotation_prometheus_io_scrape]
        action: keep
        regex: true
      - source_labels: [__meta_kubernetes_pod_annotation_prometheus_io_path]
        action: replace
        target_label: __metrics_path__
        regex: (.+)
      - source_labels: [__address__, __meta_kubernetes_pod_annotation_prometheus_io_port]
        action: replace
        regex: ([^:]+)(?::\d+)?;(\d+)
        replacement: $1:$2
        target_label: __address__
      - action: labelmap
        regex: __meta_kubernetes_pod_label_(.+)
      - source_labels: [__meta_kubernetes_namespace]
        action: replace
        target_label: kubernetes_namespace
      - source_labels: [__meta_kubernetes_pod_name]
        action: replace
        target_label: kubernetes_pod_name
      - source_labels: [__meta_kubernetes_pod_node_name]
        action: replace
        target_label: kubernetes_node_name

# Remote write configuration for long-term storage (optional)
# remote_write:
#   - url: "https://prometheus.example.com/api/v1/write"
#     basic_auth:
#       username: "user"
#       password: "pass"
```

### Alerting Rules

Create `docker/monitoring/prometheus/alerts/sportsbook-alerts.yml`:

```yaml
groups:
  - name: sportsbook.orleans
    interval: 30s
    rules:
      # Orleans Silo Health
      - alert: OrleansSiloDown
        expr: up{job="orleans-silo"} == 0
        for: 1m
        labels:
          severity: critical
          service: orleans
        annotations:
          summary: "Orleans Silo is down"
          description: "Orleans Silo {{ $labels.instance }} has been down for more than 1 minute."

      # High grain activation failures
      - alert: HighGrainActivationFailures
        expr: increase(sportsbook_orleans_grain_activations_failed_total[5m]) > 10
        for: 2m
        labels:
          severity: warning
          service: orleans
        annotations:
          summary: "High grain activation failures"
          description: "{{ $value }} grain activations failed in the last 5 minutes on {{ $labels.instance }}"

      # High request latency
      - alert: HighOrleansSiloLatency
        expr: histogram_quantile(0.95, sportsbook_orleans_request_duration_seconds_bucket) > 1.0
        for: 2m
        labels:
          severity: warning
          service: orleans
        annotations:
          summary: "High Orleans request latency"
          description: "95th percentile latency is {{ $value }}s on {{ $labels.instance }}"

      # Memory usage
      - alert: HighOrleansSiloMemoryUsage
        expr: process_resident_memory_bytes{job="orleans-silo"} / 1024 / 1024 / 1024 > 2.0
        for: 5m
        labels:
          severity: warning
          service: orleans
        annotations:
          summary: "High Orleans Silo memory usage"
          description: "Orleans Silo {{ $labels.instance }} is using {{ $value }}GB of memory"

  - name: sportsbook.api
    interval: 30s
    rules:
      # API Health
      - alert: SportsbookAPIDown
        expr: up{job="sportsbook-api"} == 0
        for: 1m
        labels:
          severity: critical
          service: api
        annotations:
          summary: "Sportsbook API is down"
          description: "Sportsbook API {{ $labels.instance }} has been down for more than 1 minute."

      # High error rate
      - alert: HighAPIErrorRate
        expr: rate(sportsbook_api_requests_total{status=~"5.."}[5m]) / rate(sportsbook_api_requests_total[5m]) * 100 > 5
        for: 2m
        labels:
          severity: warning
          service: api
        annotations:
          summary: "High API error rate"
          description: "API error rate is {{ $value }}% on {{ $labels.instance }}"

      # High response time
      - alert: HighAPIResponseTime
        expr: histogram_quantile(0.95, sportsbook_api_request_duration_seconds_bucket) > 2.0
        for: 2m
        labels:
          severity: warning
          service: api
        annotations:
          summary: "High API response time"
          description: "95th percentile response time is {{ $value }}s on {{ $labels.instance }}"

  - name: sportsbook.infrastructure
    interval: 30s
    rules:
      # PostgreSQL
      - alert: PostgreSQLDown
        expr: up{job="postgres"} == 0
        for: 1m
        labels:
          severity: critical
          service: postgres
        annotations:
          summary: "PostgreSQL is down"
          description: "PostgreSQL database {{ $labels.instance }} is down"

      - alert: PostgreSQLHighConnections
        expr: pg_stat_activity_count / pg_settings_max_connections * 100 > 80
        for: 5m
        labels:
          severity: warning
          service: postgres
        annotations:
          summary: "PostgreSQL high connection usage"
          description: "PostgreSQL is using {{ $value }}% of max connections"

      # Redis
      - alert: RedisDown
        expr: up{job="redis"} == 0
        for: 1m
        labels:
          severity: critical
          service: redis
        annotations:
          summary: "Redis is down"
          description: "Redis instance {{ $labels.instance }} is down"

      - alert: RedisHighMemoryUsage
        expr: redis_memory_used_bytes / redis_memory_max_bytes * 100 > 90
        for: 5m
        labels:
          severity: warning
          service: redis
        annotations:
          summary: "Redis high memory usage"
          description: "Redis is using {{ $value }}% of available memory"

      # System resources
      - alert: HighCPUUsage
        expr: 100 - (avg(rate(node_cpu_seconds_total{mode="idle"}[5m])) * 100) > 80
        for: 5m
        labels:
          severity: warning
          service: system
        annotations:
          summary: "High CPU usage"
          description: "CPU usage is {{ $value }}% on {{ $labels.instance }}"

      - alert: HighMemoryUsage
        expr: (1 - (node_memory_MemAvailable_bytes / node_memory_MemTotal_bytes)) * 100 > 85
        for: 5m
        labels:
          severity: warning
          service: system
        annotations:
          summary: "High memory usage"
          description: "Memory usage is {{ $value }}% on {{ $labels.instance }}"

      - alert: LowDiskSpace
        expr: (1 - (node_filesystem_avail_bytes / node_filesystem_size_bytes)) * 100 > 85
        for: 5m
        labels:
          severity: warning
          service: system
        annotations:
          summary: "Low disk space"
          description: "Disk usage is {{ $value }}% on {{ $labels.instance }}"
```

## Kubernetes Production Deployment

### Monitoring Namespace

Create `k8s/monitoring/namespace.yaml`:

```yaml
apiVersion: v1
kind: Namespace
metadata:
  name: monitoring
  labels:
    name: monitoring
    app.kubernetes.io/name: monitoring
    app.kubernetes.io/part-of: sportsbook-lite
```

### Prometheus ConfigMap

Create `k8s/monitoring/prometheus-config.yaml`:

```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: prometheus-config
  namespace: monitoring
  labels:
    app.kubernetes.io/name: prometheus
    app.kubernetes.io/part-of: sportsbook-lite
data:
  prometheus.yml: |
    global:
      scrape_interval: 15s
      scrape_timeout: 10s
      evaluation_interval: 30s
      external_labels:
        cluster: 'sportsbook-lite-prod'
        environment: 'production'

    rule_files:
      - "/etc/prometheus/alerts/*.yml"

    alerting:
      alertmanagers:
        - kubernetes_sd_configs:
            - role: pod
              namespaces:
                names:
                  - monitoring
          relabel_configs:
            - source_labels: [__meta_kubernetes_pod_label_app]
              action: keep
              regex: alertmanager

    scrape_configs:
      - job_name: 'prometheus'
        static_configs:
          - targets: ['localhost:9090']

      - job_name: 'kubernetes-apiservers'
        kubernetes_sd_configs:
          - role: endpoints
            namespaces:
              names:
                - default
        scheme: https
        tls_config:
          ca_file: /var/run/secrets/kubernetes.io/serviceaccount/ca.crt
          insecure_skip_verify: true
        bearer_token_file: /var/run/secrets/kubernetes.io/serviceaccount/token
        relabel_configs:
          - source_labels: [__meta_kubernetes_namespace, __meta_kubernetes_service_name, __meta_kubernetes_endpoint_port_name]
            action: keep
            regex: default;kubernetes;https

      - job_name: 'kubernetes-nodes'
        kubernetes_sd_configs:
          - role: node
        scheme: https
        tls_config:
          ca_file: /var/run/secrets/kubernetes.io/serviceaccount/ca.crt
          insecure_skip_verify: true
        bearer_token_file: /var/run/secrets/kubernetes.io/serviceaccount/token
        relabel_configs:
          - action: labelmap
            regex: __meta_kubernetes_node_label_(.+)
          - target_label: __address__
            replacement: kubernetes.default.svc:443
          - source_labels: [__meta_kubernetes_node_name]
            regex: (.+)
            target_label: __metrics_path__
            replacement: /api/v1/nodes/${1}/proxy/metrics

      - job_name: 'kubernetes-cadvisor'
        kubernetes_sd_configs:
          - role: node
        scheme: https
        tls_config:
          ca_file: /var/run/secrets/kubernetes.io/serviceaccount/ca.crt
          insecure_skip_verify: true
        bearer_token_file: /var/run/secrets/kubernetes.io/serviceaccount/token
        relabel_configs:
          - action: labelmap
            regex: __meta_kubernetes_node_label_(.+)
          - target_label: __address__
            replacement: kubernetes.default.svc:443
          - source_labels: [__meta_kubernetes_node_name]
            regex: (.+)
            target_label: __metrics_path__
            replacement: /api/v1/nodes/${1}/proxy/metrics/cadvisor

      - job_name: 'kubernetes-service-endpoints'
        kubernetes_sd_configs:
          - role: endpoints
        relabel_configs:
          - source_labels: [__meta_kubernetes_service_annotation_prometheus_io_scrape]
            action: keep
            regex: true
          - source_labels: [__meta_kubernetes_service_annotation_prometheus_io_scheme]
            action: replace
            target_label: __scheme__
            regex: (https?)
          - source_labels: [__meta_kubernetes_service_annotation_prometheus_io_path]
            action: replace
            target_label: __metrics_path__
            regex: (.+)
          - source_labels: [__address__, __meta_kubernetes_service_annotation_prometheus_io_port]
            action: replace
            target_label: __address__
            regex: ([^:]+)(?::\d+)?;(\d+)
            replacement: $1:$2
          - action: labelmap
            regex: __meta_kubernetes_service_label_(.+)
          - source_labels: [__meta_kubernetes_namespace]
            action: replace
            target_label: kubernetes_namespace
          - source_labels: [__meta_kubernetes_service_name]
            action: replace
            target_label: kubernetes_name

      - job_name: 'orleans-silo'
        kubernetes_sd_configs:
          - role: pod
            namespaces:
              names:
                - sportsbook-lite
        relabel_configs:
          - source_labels: [__meta_kubernetes_pod_label_app]
            action: keep
            regex: orleans-silo
          - source_labels: [__meta_kubernetes_pod_annotation_prometheus_io_scrape]
            action: keep
            regex: true
          - source_labels: [__meta_kubernetes_pod_annotation_prometheus_io_path]
            action: replace
            target_label: __metrics_path__
            regex: (.+)
          - source_labels: [__address__, __meta_kubernetes_pod_annotation_prometheus_io_port]
            action: replace
            regex: ([^:]+)(?::\d+)?;(\d+)
            replacement: $1:$2
            target_label: __address__
          - action: labelmap
            regex: __meta_kubernetes_pod_label_(.+)
          - source_labels: [__meta_kubernetes_namespace]
            action: replace
            target_label: kubernetes_namespace
          - source_labels: [__meta_kubernetes_pod_name]
            action: replace
            target_label: kubernetes_pod_name

      - job_name: 'sportsbook-api'
        kubernetes_sd_configs:
          - role: pod
            namespaces:
              names:
                - sportsbook-lite
        relabel_configs:
          - source_labels: [__meta_kubernetes_pod_label_app]
            action: keep
            regex: sportsbook-api
          - source_labels: [__meta_kubernetes_pod_annotation_prometheus_io_scrape]
            action: keep
            regex: true
          - source_labels: [__meta_kubernetes_pod_annotation_prometheus_io_path]
            action: replace
            target_label: __metrics_path__
            regex: (.+)
          - source_labels: [__address__, __meta_kubernetes_pod_annotation_prometheus_io_port]
            action: replace
            regex: ([^:]+)(?::\d+)?;(\d+)
            replacement: $1:$2
            target_label: __address__
          - action: labelmap
            regex: __meta_kubernetes_pod_label_(.+)
          - source_labels: [__meta_kubernetes_namespace]
            action: replace
            target_label: kubernetes_namespace
          - source_labels: [__meta_kubernetes_pod_name]
            action: replace
            target_label: kubernetes_pod_name
---
apiVersion: v1
kind: ConfigMap
metadata:
  name: prometheus-alerts
  namespace: monitoring
data:
  sportsbook-alerts.yml: |
    # Same content as the alerts file above, but formatted for ConfigMap
    groups:
      - name: sportsbook.orleans
        interval: 30s
        rules:
          - alert: OrleansSiloDown
            expr: up{job="orleans-silo"} == 0
            for: 1m
            labels:
              severity: critical
              service: orleans
            annotations:
              summary: "Orleans Silo is down"
              description: "Orleans Silo {{ $labels.instance }} has been down for more than 1 minute."
          # ... (rest of alerts as above)
```

### Prometheus Deployment

Create `k8s/monitoring/prometheus-deployment.yaml`:

```yaml
apiVersion: rbac.authorization.k8s.io/v1
kind: ClusterRole
metadata:
  name: prometheus
rules:
  - apiGroups: [""]
    resources:
      - nodes
      - nodes/proxy
      - services
      - endpoints
      - pods
    verbs: ["get", "list", "watch"]
  - apiGroups:
      - extensions
    resources:
      - ingresses
    verbs: ["get", "list", "watch"]
  - nonResourceURLs: ["/metrics"]
    verbs: ["get"]
---
apiVersion: v1
kind: ServiceAccount
metadata:
  name: prometheus
  namespace: monitoring
---
apiVersion: rbac.authorization.k8s.io/v1
kind: ClusterRoleBinding
metadata:
  name: prometheus
roleRef:
  apiGroup: rbac.authorization.k8s.io
  kind: ClusterRole
  name: prometheus
subjects:
  - kind: ServiceAccount
    name: prometheus
    namespace: monitoring
---
apiVersion: apps/v1
kind: StatefulSet
metadata:
  name: prometheus
  namespace: monitoring
  labels:
    app: prometheus
    app.kubernetes.io/name: prometheus
    app.kubernetes.io/part-of: sportsbook-lite
spec:
  serviceName: prometheus
  replicas: 1
  selector:
    matchLabels:
      app: prometheus
  template:
    metadata:
      labels:
        app: prometheus
      annotations:
        prometheus.io/scrape: "true"
        prometheus.io/port: "9090"
    spec:
      serviceAccountName: prometheus
      containers:
        - name: prometheus
          image: prom/prometheus:v2.48.0
          args:
            - '--config.file=/etc/prometheus/prometheus.yml'
            - '--storage.tsdb.path=/prometheus/'
            - '--storage.tsdb.retention.time=30d'
            - '--storage.tsdb.retention.size=50GB'
            - '--web.console.libraries=/etc/prometheus/console_libraries'
            - '--web.console.templates=/etc/prometheus/consoles'
            - '--web.enable-lifecycle'
            - '--web.enable-admin-api'
            - '--log.level=info'
          ports:
            - containerPort: 9090
              name: http
          resources:
            requests:
              cpu: 500m
              memory: 2Gi
            limits:
              cpu: 2000m
              memory: 4Gi
          volumeMounts:
            - name: prometheus-config
              mountPath: /etc/prometheus/
            - name: prometheus-alerts
              mountPath: /etc/prometheus/alerts/
            - name: prometheus-storage
              mountPath: /prometheus/
          livenessProbe:
            httpGet:
              path: /-/healthy
              port: 9090
            initialDelaySeconds: 30
            timeoutSeconds: 30
          readinessProbe:
            httpGet:
              path: /-/ready
              port: 9090
            initialDelaySeconds: 30
            timeoutSeconds: 30
      volumes:
        - name: prometheus-config
          configMap:
            name: prometheus-config
        - name: prometheus-alerts
          configMap:
            name: prometheus-alerts
  volumeClaimTemplates:
    - metadata:
        name: prometheus-storage
      spec:
        accessModes: ["ReadWriteOnce"]
        resources:
          requests:
            storage: 100Gi
        storageClassName: fast-ssd
---
apiVersion: v1
kind: Service
metadata:
  name: prometheus
  namespace: monitoring
  labels:
    app: prometheus
  annotations:
    prometheus.io/scrape: "true"
    prometheus.io/port: "9090"
spec:
  type: ClusterIP
  ports:
    - port: 9090
      targetPort: 9090
      protocol: TCP
      name: http
  selector:
    app: prometheus
---
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: prometheus
  namespace: monitoring
  annotations:
    kubernetes.io/ingress.class: nginx
    cert-manager.io/cluster-issuer: letsencrypt-prod
    nginx.ingress.kubernetes.io/auth-type: basic
    nginx.ingress.kubernetes.io/auth-secret: prometheus-basic-auth
    nginx.ingress.kubernetes.io/auth-realm: 'Authentication Required - Prometheus'
spec:
  tls:
    - hosts:
        - prometheus.sportsbook.example.com
      secretName: prometheus-tls
  rules:
    - host: prometheus.sportsbook.example.com
      http:
        paths:
          - path: /
            pathType: Prefix
            backend:
              service:
                name: prometheus
                port:
                  number: 9090
```

### Grafana Deployment

Create `k8s/monitoring/grafana-deployment.yaml`:

```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: grafana-config
  namespace: monitoring
data:
  grafana.ini: |
    [analytics]
    check_for_updates = true
    reporting_enabled = false
    [grafana_net]
    url = https://grafana.net
    [log]
    mode = console
    [paths]
    data = /var/lib/grafana/
    logs = /var/log/grafana
    plugins = /var/lib/grafana/plugins
    provisioning = /etc/grafana/provisioning
    [security]
    admin_user = admin
    admin_password = ${GF_SECURITY_ADMIN_PASSWORD}
    [server]
    http_port = 3000
    [feature_toggles]
    enable = publicDashboards
---
apiVersion: v1
kind: ConfigMap
metadata:
  name: grafana-datasources
  namespace: monitoring
data:
  datasources.yaml: |
    apiVersion: 1
    datasources:
      - name: Prometheus
        type: prometheus
        url: http://prometheus:9090
        access: proxy
        isDefault: true
        editable: true
      - name: AlertManager
        type: alertmanager
        url: http://alertmanager:9093
        access: proxy
        editable: true
---
apiVersion: v1
kind: Secret
metadata:
  name: grafana-credentials
  namespace: monitoring
type: Opaque
data:
  admin-password: YWRtaW4xMjM=  # admin123 base64 encoded
---
apiVersion: apps/v1
kind: Deployment
metadata:
  name: grafana
  namespace: monitoring
  labels:
    app: grafana
    app.kubernetes.io/name: grafana
    app.kubernetes.io/part-of: sportsbook-lite
spec:
  replicas: 1
  selector:
    matchLabels:
      app: grafana
  template:
    metadata:
      labels:
        app: grafana
      annotations:
        prometheus.io/scrape: "true"
        prometheus.io/port: "3000"
    spec:
      containers:
        - name: grafana
          image: grafana/grafana:10.2.0
          ports:
            - containerPort: 3000
              name: http
          env:
            - name: GF_SECURITY_ADMIN_PASSWORD
              valueFrom:
                secretKeyRef:
                  name: grafana-credentials
                  key: admin-password
            - name: GF_USERS_ALLOW_SIGN_UP
              value: "false"
            - name: GF_INSTALL_PLUGINS
              value: "grafana-piechart-panel,grafana-worldmap-panel,camptocamp-prometheus-alertmanager-datasource"
          resources:
            requests:
              cpu: 100m
              memory: 256Mi
            limits:
              cpu: 500m
              memory: 1Gi
          volumeMounts:
            - name: grafana-config
              mountPath: /etc/grafana/grafana.ini
              subPath: grafana.ini
            - name: grafana-datasources
              mountPath: /etc/grafana/provisioning/datasources/
            - name: grafana-storage
              mountPath: /var/lib/grafana
          livenessProbe:
            httpGet:
              path: /api/health
              port: 3000
            initialDelaySeconds: 30
            timeoutSeconds: 30
          readinessProbe:
            httpGet:
              path: /api/health
              port: 3000
            initialDelaySeconds: 30
            timeoutSeconds: 30
      volumes:
        - name: grafana-config
          configMap:
            name: grafana-config
        - name: grafana-datasources
          configMap:
            name: grafana-datasources
        - name: grafana-storage
          persistentVolumeClaim:
            claimName: grafana-storage
---
apiVersion: v1
kind: PersistentVolumeClaim
metadata:
  name: grafana-storage
  namespace: monitoring
spec:
  accessModes:
    - ReadWriteOnce
  resources:
    requests:
      storage: 10Gi
  storageClassName: fast-ssd
---
apiVersion: v1
kind: Service
metadata:
  name: grafana
  namespace: monitoring
  labels:
    app: grafana
  annotations:
    prometheus.io/scrape: "true"
    prometheus.io/port: "3000"
spec:
  type: ClusterIP
  ports:
    - port: 3000
      targetPort: 3000
      protocol: TCP
      name: http
  selector:
    app: grafana
---
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: grafana
  namespace: monitoring
  annotations:
    kubernetes.io/ingress.class: nginx
    cert-manager.io/cluster-issuer: letsencrypt-prod
    nginx.ingress.kubernetes.io/ssl-redirect: "true"
spec:
  tls:
    - hosts:
        - grafana.sportsbook.example.com
      secretName: grafana-tls
  rules:
    - host: grafana.sportsbook.example.com
      http:
        paths:
          - path: /
            pathType: Prefix
            backend:
              service:
                name: grafana
                port:
                  number: 3000
```

## AlertManager Configuration

### AlertManager ConfigMap and Deployment

Create `k8s/monitoring/alertmanager-deployment.yaml`:

```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: alertmanager-config
  namespace: monitoring
data:
  alertmanager.yml: |
    global:
      smtp_smarthost: 'smtp.example.com:587'
      smtp_from: 'alerts@sportsbook.example.com'
      smtp_auth_username: 'alerts@sportsbook.example.com'
      smtp_auth_password: 'your-smtp-password'
      slack_api_url: 'https://hooks.slack.com/services/YOUR/SLACK/WEBHOOK'

    templates:
      - '/etc/alertmanager/templates/*.tmpl'

    route:
      group_by: ['alertname', 'cluster', 'service']
      group_wait: 10s
      group_interval: 10s
      repeat_interval: 1h
      receiver: 'default'
      routes:
        - match:
            severity: critical
          receiver: 'critical-alerts'
          group_wait: 5s
          group_interval: 5s
          repeat_interval: 30m
        - match:
            service: orleans
          receiver: 'orleans-alerts'
        - match:
            service: api
          receiver: 'api-alerts'
        - match:
            service: postgres
          receiver: 'database-alerts'
        - match:
            service: redis
          receiver: 'cache-alerts'

    receivers:
      - name: 'default'
        email_configs:
          - to: 'dev-team@sportsbook.example.com'
            subject: 'Sportsbook Alert: {{ .GroupLabels.alertname }}'
            body: |
              {{ range .Alerts }}
              Alert: {{ .Annotations.summary }}
              Description: {{ .Annotations.description }}
              Details:
              {{ range .Labels.SortedPairs }} - {{ .Name }} = {{ .Value }}
              {{ end }}
              {{ end }}

      - name: 'critical-alerts'
        email_configs:
          - to: 'oncall@sportsbook.example.com'
            subject: 'CRITICAL: Sportsbook Alert - {{ .GroupLabels.alertname }}'
            body: |
              🚨 CRITICAL ALERT 🚨
              
              {{ range .Alerts }}
              Alert: {{ .Annotations.summary }}
              Description: {{ .Annotations.description }}
              Severity: {{ .Labels.severity }}
              Service: {{ .Labels.service }}
              
              Details:
              {{ range .Labels.SortedPairs }} - {{ .Name }} = {{ .Value }}
              {{ end }}
              {{ end }}
        slack_configs:
          - channel: '#alerts-critical'
            username: 'AlertManager'
            title: 'Critical Alert: {{ .GroupLabels.alertname }}'
            text: |
              {{ range .Alerts }}
              🚨 {{ .Annotations.summary }}
              {{ .Annotations.description }}
              Service: {{ .Labels.service }}
              {{ end }}
            color: 'danger'

      - name: 'orleans-alerts'
        slack_configs:
          - channel: '#orleans-alerts'
            username: 'Orleans Monitor'
            title: 'Orleans Alert: {{ .GroupLabels.alertname }}'
            text: |
              {{ range .Alerts }}
              ⚠️ {{ .Annotations.summary }}
              {{ .Annotations.description }}
              Silo: {{ .Labels.instance }}
              {{ end }}
            color: 'warning'

      - name: 'api-alerts'
        slack_configs:
          - channel: '#api-alerts'
            username: 'API Monitor'
            title: 'API Alert: {{ .GroupLabels.alertname }}'
            text: |
              {{ range .Alerts }}
              🔌 {{ .Annotations.summary }}
              {{ .Annotations.description }}
              Instance: {{ .Labels.instance }}
              {{ end }}
            color: 'warning'

      - name: 'database-alerts'
        email_configs:
          - to: 'dba@sportsbook.example.com'
            subject: 'Database Alert: {{ .GroupLabels.alertname }}'
            body: |
              Database Alert Detected:
              
              {{ range .Alerts }}
              Alert: {{ .Annotations.summary }}
              Description: {{ .Annotations.description }}
              Database: {{ .Labels.postgres_instance }}
              {{ end }}
        slack_configs:
          - channel: '#database-alerts'
            username: 'Database Monitor'
            title: 'Database Alert: {{ .GroupLabels.alertname }}'
            color: 'warning'

      - name: 'cache-alerts'
        slack_configs:
          - channel: '#infrastructure-alerts'
            username: 'Infrastructure Monitor'
            title: 'Redis Alert: {{ .GroupLabels.alertname }}'
            color: 'warning'

    inhibit_rules:
      - source_match:
          severity: 'critical'
        target_match:
          severity: 'warning'
        equal: ['alertname', 'instance']
---
apiVersion: apps/v1
kind: Deployment
metadata:
  name: alertmanager
  namespace: monitoring
  labels:
    app: alertmanager
    app.kubernetes.io/name: alertmanager
    app.kubernetes.io/part-of: sportsbook-lite
spec:
  replicas: 1
  selector:
    matchLabels:
      app: alertmanager
  template:
    metadata:
      labels:
        app: alertmanager
      annotations:
        prometheus.io/scrape: "true"
        prometheus.io/port: "9093"
    spec:
      containers:
        - name: alertmanager
          image: prom/alertmanager:v0.26.0
          args:
            - '--config.file=/etc/alertmanager/alertmanager.yml'
            - '--storage.path=/alertmanager'
            - '--web.external-url=https://alertmanager.sportsbook.example.com'
            - '--cluster.advertise-address=0.0.0.0:9093'
          ports:
            - containerPort: 9093
              name: http
          resources:
            requests:
              cpu: 100m
              memory: 128Mi
            limits:
              cpu: 500m
              memory: 512Mi
          volumeMounts:
            - name: alertmanager-config
              mountPath: /etc/alertmanager/
            - name: alertmanager-storage
              mountPath: /alertmanager
          livenessProbe:
            httpGet:
              path: /-/healthy
              port: 9093
            initialDelaySeconds: 30
            timeoutSeconds: 30
          readinessProbe:
            httpGet:
              path: /-/ready
              port: 9093
            initialDelaySeconds: 30
            timeoutSeconds: 30
      volumes:
        - name: alertmanager-config
          configMap:
            name: alertmanager-config
        - name: alertmanager-storage
          persistentVolumeClaim:
            claimName: alertmanager-storage
---
apiVersion: v1
kind: PersistentVolumeClaim
metadata:
  name: alertmanager-storage
  namespace: monitoring
spec:
  accessModes:
    - ReadWriteOnce
  resources:
    requests:
      storage: 5Gi
  storageClassName: fast-ssd
---
apiVersion: v1
kind: Service
metadata:
  name: alertmanager
  namespace: monitoring
  labels:
    app: alertmanager
  annotations:
    prometheus.io/scrape: "true"
    prometheus.io/port: "9093"
spec:
  type: ClusterIP
  ports:
    - port: 9093
      targetPort: 9093
      protocol: TCP
      name: http
  selector:
    app: alertmanager
```

## Service Discovery

### Kubernetes Service Discovery Configuration

The Prometheus configuration above includes comprehensive Kubernetes service discovery. Here are additional service discovery patterns:

Create `k8s/monitoring/service-discovery.yaml`:

```yaml
# ServiceMonitor CRD for Prometheus Operator (if using)
apiVersion: monitoring.coreos.com/v1
kind: ServiceMonitor
metadata:
  name: orleans-silo
  namespace: monitoring
  labels:
    app: orleans-silo
spec:
  selector:
    matchLabels:
      app: orleans-silo
  endpoints:
    - port: metrics
      path: /metrics
      interval: 15s
      scrapeTimeout: 10s
  namespaceSelector:
    matchNames:
      - sportsbook-lite
---
apiVersion: monitoring.coreos.com/v1
kind: ServiceMonitor
metadata:
  name: sportsbook-api
  namespace: monitoring
  labels:
    app: sportsbook-api
spec:
  selector:
    matchLabels:
      app: sportsbook-api
  endpoints:
    - port: metrics
      path: /metrics
      interval: 15s
      scrapeTimeout: 10s
  namespaceSelector:
    matchNames:
      - sportsbook-lite
---
# PodMonitor for dynamic Orleans silo discovery
apiVersion: monitoring.coreos.com/v1
kind: PodMonitor
metadata:
  name: orleans-silo-pods
  namespace: monitoring
  labels:
    app: orleans-silo
spec:
  selector:
    matchLabels:
      app: orleans-silo
  podMetricsEndpoints:
    - port: metrics
      path: /metrics
      interval: 15s
      scrapeTimeout: 10s
  namespaceSelector:
    matchNames:
      - sportsbook-lite
```

## Persistent Storage

### Storage Classes

Create `k8s/monitoring/storage-classes.yaml`:

```yaml
# Fast SSD storage class for monitoring data
apiVersion: storage.k8s.io/v1
kind: StorageClass
metadata:
  name: fast-ssd
  labels:
    app.kubernetes.io/name: storage
    app.kubernetes.io/part-of: sportsbook-lite
provisioner: kubernetes.io/aws-ebs  # Change based on your cloud provider
parameters:
  type: gp3
  iops: "3000"
  throughput: "125"
  encrypted: "true"
volumeBindingMode: WaitForFirstConsumer
allowVolumeExpansion: true
reclaimPolicy: Retain
---
# Standard storage for less critical data
apiVersion: storage.k8s.io/v1
kind: StorageClass
metadata:
  name: standard-hdd
  labels:
    app.kubernetes.io/name: storage
    app.kubernetes.io/part-of: sportsbook-lite
provisioner: kubernetes.io/aws-ebs
parameters:
  type: gp2
  encrypted: "true"
volumeBindingMode: WaitForFirstConsumer
allowVolumeExpansion: true
reclaimPolicy: Delete
```

### Backup Configuration

Create `k8s/monitoring/backup-job.yaml`:

```yaml
apiVersion: batch/v1
kind: CronJob
metadata:
  name: prometheus-backup
  namespace: monitoring
  labels:
    app: prometheus-backup
spec:
  schedule: "0 2 * * *"  # Daily at 2 AM
  jobTemplate:
    spec:
      template:
        spec:
          containers:
            - name: backup
              image: alpine:3.18
              command:
                - /bin/sh
                - -c
                - |
                  apk add --no-cache aws-cli
                  DATE=$(date +%Y%m%d-%H%M%S)
                  tar -czf /tmp/prometheus-backup-$DATE.tar.gz -C /prometheus .
                  aws s3 cp /tmp/prometheus-backup-$DATE.tar.gz s3://sportsbook-backups/prometheus/
                  # Keep only last 30 days of backups
                  aws s3api list-objects-v2 --bucket sportsbook-backups --prefix prometheus/ --query 'Contents[?LastModified<=`'$(date -d "30 days ago" --iso-8601)'`].Key' --output text | xargs -r aws s3 rm --recursive s3://sportsbook-backups/
              env:
                - name: AWS_ACCESS_KEY_ID
                  valueFrom:
                    secretKeyRef:
                      name: backup-credentials
                      key: aws-access-key-id
                - name: AWS_SECRET_ACCESS_KEY
                  valueFrom:
                    secretKeyRef:
                      name: backup-credentials
                      key: aws-secret-access-key
                - name: AWS_DEFAULT_REGION
                  value: us-west-2
              volumeMounts:
                - name: prometheus-data
                  mountPath: /prometheus
                  readOnly: true
          volumes:
            - name: prometheus-data
              persistentVolumeClaim:
                claimName: prometheus-storage-prometheus-0
          restartPolicy: OnFailure
---
apiVersion: batch/v1
kind: CronJob
metadata:
  name: grafana-backup
  namespace: monitoring
  labels:
    app: grafana-backup
spec:
  schedule: "0 3 * * *"  # Daily at 3 AM
  jobTemplate:
    spec:
      template:
        spec:
          containers:
            - name: backup
              image: alpine:3.18
              command:
                - /bin/sh
                - -c
                - |
                  apk add --no-cache aws-cli
                  DATE=$(date +%Y%m%d-%H%M%S)
                  tar -czf /tmp/grafana-backup-$DATE.tar.gz -C /var/lib/grafana .
                  aws s3 cp /tmp/grafana-backup-$DATE.tar.gz s3://sportsbook-backups/grafana/
              env:
                - name: AWS_ACCESS_KEY_ID
                  valueFrom:
                    secretKeyRef:
                      name: backup-credentials
                      key: aws-access-key-id
                - name: AWS_SECRET_ACCESS_KEY
                  valueFrom:
                    secretKeyRef:
                      name: backup-credentials
                      key: aws-secret-access-key
                - name: AWS_DEFAULT_REGION
                  value: us-west-2
              volumeMounts:
                - name: grafana-data
                  mountPath: /var/lib/grafana
                  readOnly: true
          volumes:
            - name: grafana-data
              persistentVolumeClaim:
                claimName: grafana-storage
          restartPolicy: OnFailure
```

## Network and Security Policies

Create `k8s/monitoring/network-policies.yaml`:

```yaml
# Allow Prometheus to scrape metrics from all services
apiVersion: networking.k8s.io/v1
kind: NetworkPolicy
metadata:
  name: prometheus-scraping
  namespace: monitoring
spec:
  podSelector:
    matchLabels:
      app: prometheus
  policyTypes:
    - Ingress
    - Egress
  ingress:
    - from:
        - namespaceSelector:
            matchLabels:
              name: monitoring
        - namespaceSelector:
            matchLabels:
              name: sportsbook-lite
      ports:
        - protocol: TCP
          port: 9090
  egress:
    - to: []  # Allow all outbound traffic for scraping
      ports:
        - protocol: TCP
          port: 53
        - protocol: UDP
          port: 53
    - to:
        - namespaceSelector:
            matchLabels:
              name: sportsbook-lite
      ports:
        - protocol: TCP
          port: 8080  # Orleans metrics
        - protocol: TCP
          port: 9100  # Node exporter
        - protocol: TCP
          port: 9121  # Redis exporter
        - protocol: TCP
          port: 9187  # PostgreSQL exporter
---
# Allow Grafana to access Prometheus and AlertManager
apiVersion: networking.k8s.io/v1
kind: NetworkPolicy
metadata:
  name: grafana-access
  namespace: monitoring
spec:
  podSelector:
    matchLabels:
      app: grafana
  policyTypes:
    - Ingress
    - Egress
  ingress:
    - from:
        - namespaceSelector:
            matchLabels:
              name: ingress-nginx
      ports:
        - protocol: TCP
          port: 3000
  egress:
    - to:
        - namespaceSelector:
            matchLabels:
              name: monitoring
        - podSelector:
            matchLabels:
              app: prometheus
      ports:
        - protocol: TCP
          port: 9090
    - to:
        - podSelector:
            matchLabels:
              app: alertmanager
      ports:
        - protocol: TCP
          port: 9093
---
# Allow AlertManager external communications
apiVersion: networking.k8s.io/v1
kind: NetworkPolicy
metadata:
  name: alertmanager-external
  namespace: monitoring
spec:
  podSelector:
    matchLabels:
      app: alertmanager
  policyTypes:
    - Ingress
    - Egress
  ingress:
    - from:
        - podSelector:
            matchLabels:
              app: prometheus
        - podSelector:
            matchLabels:
              app: grafana
      ports:
        - protocol: TCP
          port: 9093
  egress:
    - to: []  # Allow all outbound for SMTP/Slack webhooks
      ports:
        - protocol: TCP
          port: 587  # SMTP
        - protocol: TCP
          port: 443  # HTTPS for webhooks
        - protocol: TCP
          port: 80   # HTTP
```

### Security Policies

Create `k8s/monitoring/security-policies.yaml`:

```yaml
# Pod Security Policy for monitoring namespace
apiVersion: policy/v1beta1
kind: PodSecurityPolicy
metadata:
  name: monitoring-psp
spec:
  privileged: false
  allowPrivilegeEscalation: false
  requiredDropCapabilities:
    - ALL
  volumes:
    - 'configMap'
    - 'emptyDir'
    - 'projected'
    - 'secret'
    - 'downwardAPI'
    - 'persistentVolumeClaim'
  runAsUser:
    rule: 'MustRunAsNonRoot'
  seLinux:
    rule: 'RunAsAny'
  fsGroup:
    rule: 'RunAsAny'
---
# RBAC for monitoring namespace
apiVersion: rbac.authorization.k8s.io/v1
kind: Role
metadata:
  name: monitoring-role
  namespace: monitoring
rules:
  - apiGroups: [""]
    resources: ["pods", "services", "endpoints"]
    verbs: ["get", "list", "watch"]
  - apiGroups: ["apps"]
    resources: ["deployments", "statefulsets"]
    verbs: ["get", "list", "watch"]
---
apiVersion: rbac.authorization.k8s.io/v1
kind: RoleBinding
metadata:
  name: monitoring-rolebinding
  namespace: monitoring
subjects:
  - kind: ServiceAccount
    name: prometheus
    namespace: monitoring
  - kind: ServiceAccount
    name: grafana
    namespace: monitoring
roleRef:
  kind: Role
  name: monitoring-role
  apiGroup: rbac.authorization.k8s.io
```

## Backup and Restore Procedures

### Backup Scripts

Create `scripts/monitoring/backup.sh`:

```bash
#!/bin/bash
set -euo pipefail

# Sportsbook-Lite Monitoring Backup Script
# This script backs up Prometheus and Grafana data

BACKUP_DIR="/backups/monitoring"
DATE=$(date +%Y%m%d-%H%M%S)
S3_BUCKET="sportsbook-backups"
RETENTION_DAYS=30

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

log() {
    echo -e "${GREEN}[$(date +'%Y-%m-%d %H:%M:%S')] $1${NC}"
}

warn() {
    echo -e "${YELLOW}[$(date +'%Y-%m-%d %H:%M:%S')] WARNING: $1${NC}"
}

error() {
    echo -e "${RED}[$(date +'%Y-%m-%d %H:%M:%S')] ERROR: $1${NC}"
    exit 1
}

# Check prerequisites
command -v kubectl >/dev/null 2>&1 || error "kubectl is required but not installed"
command -v aws >/dev/null 2>&1 || error "aws CLI is required but not installed"

# Create backup directory
mkdir -p "$BACKUP_DIR"

backup_prometheus() {
    log "Starting Prometheus backup..."
    
    # Get Prometheus pod
    PROMETHEUS_POD=$(kubectl get pods -n monitoring -l app=prometheus -o jsonpath='{.items[0].metadata.name}')
    if [[ -z "$PROMETHEUS_POD" ]]; then
        error "No Prometheus pod found"
    fi
    
    log "Backing up Prometheus data from pod: $PROMETHEUS_POD"
    
    # Create snapshot
    kubectl exec -n monitoring "$PROMETHEUS_POD" -- promtool tsdb create-blocks-from /prometheus /tmp/snapshot
    
    # Copy snapshot
    kubectl cp "monitoring/$PROMETHEUS_POD:/tmp/snapshot" "$BACKUP_DIR/prometheus-$DATE" || error "Failed to copy Prometheus snapshot"
    
    # Create archive
    tar -czf "$BACKUP_DIR/prometheus-backup-$DATE.tar.gz" -C "$BACKUP_DIR" "prometheus-$DATE"
    rm -rf "$BACKUP_DIR/prometheus-$DATE"
    
    log "Prometheus backup completed: prometheus-backup-$DATE.tar.gz"
}

backup_grafana() {
    log "Starting Grafana backup..."
    
    # Export dashboards via API
    GRAFANA_URL="http://grafana.sportsbook.example.com"
    GRAFANA_API_KEY="${GRAFANA_API_KEY:-}"
    
    if [[ -n "$GRAFANA_API_KEY" ]]; then
        mkdir -p "$BACKUP_DIR/grafana-$DATE"
        
        # Get all dashboards
        DASHBOARDS=$(curl -s -H "Authorization: Bearer $GRAFANA_API_KEY" "$GRAFANA_URL/api/search?type=dash-db" | jq -r '.[].uid')
        
        for uid in $DASHBOARDS; do
            curl -s -H "Authorization: Bearer $GRAFANA_API_KEY" "$GRAFANA_URL/api/dashboards/uid/$uid" | jq '.dashboard' > "$BACKUP_DIR/grafana-$DATE/dashboard-$uid.json"
        done
        
        # Backup data sources
        curl -s -H "Authorization: Bearer $GRAFANA_API_KEY" "$GRAFANA_URL/api/datasources" > "$BACKUP_DIR/grafana-$DATE/datasources.json"
        
        # Create archive
        tar -czf "$BACKUP_DIR/grafana-backup-$DATE.tar.gz" -C "$BACKUP_DIR" "grafana-$DATE"
        rm -rf "$BACKUP_DIR/grafana-$DATE"
        
        log "Grafana backup completed: grafana-backup-$DATE.tar.gz"
    else
        warn "GRAFANA_API_KEY not set, skipping Grafana backup"
    fi
}

upload_to_s3() {
    log "Uploading backups to S3..."
    
    aws s3 cp "$BACKUP_DIR/prometheus-backup-$DATE.tar.gz" "s3://$S3_BUCKET/monitoring/prometheus/" || error "Failed to upload Prometheus backup"
    
    if [[ -f "$BACKUP_DIR/grafana-backup-$DATE.tar.gz" ]]; then
        aws s3 cp "$BACKUP_DIR/grafana-backup-$DATE.tar.gz" "s3://$S3_BUCKET/monitoring/grafana/" || error "Failed to upload Grafana backup"
    fi
    
    log "Upload completed"
}

cleanup_old_backups() {
    log "Cleaning up old backups (retention: $RETENTION_DAYS days)..."
    
    # Local cleanup
    find "$BACKUP_DIR" -name "*.tar.gz" -mtime +$RETENTION_DAYS -delete
    
    # S3 cleanup
    CUTOFF_DATE=$(date -d "$RETENTION_DAYS days ago" +%Y-%m-%d)
    aws s3api list-objects-v2 --bucket "$S3_BUCKET" --prefix "monitoring/" --query "Contents[?LastModified<='$CUTOFF_DATE'].Key" --output text | xargs -r aws s3 rm --recursive "s3://$S3_BUCKET/"
    
    log "Cleanup completed"
}

# Main execution
log "Starting monitoring backup process..."

backup_prometheus
backup_grafana
upload_to_s3
cleanup_old_backups

log "Backup process completed successfully!"
```

### Restore Script

Create `scripts/monitoring/restore.sh`:

```bash
#!/bin/bash
set -euo pipefail

# Sportsbook-Lite Monitoring Restore Script

BACKUP_DIR="/backups/monitoring"
S3_BUCKET="sportsbook-backups"
BACKUP_DATE="${1:-}"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

log() {
    echo -e "${GREEN}[$(date +'%Y-%m-%d %H:%M:%S')] $1${NC}"
}

warn() {
    echo -e "${YELLOW}[$(date +'%Y-%m-%d %H:%M:%S')] WARNING: $1${NC}"
}

error() {
    echo -e "${RED}[$(date +'%Y-%m-%d %H:%M:%S')] ERROR: $1${NC}"
    exit 1
}

usage() {
    echo "Usage: $0 <backup-date>"
    echo "Example: $0 20231201-140000"
    exit 1
}

[[ -z "$BACKUP_DATE" ]] && usage

# Check prerequisites
command -v kubectl >/dev/null 2>&1 || error "kubectl is required but not installed"
command -v aws >/dev/null 2>&1 || error "aws CLI is required but not installed"

mkdir -p "$BACKUP_DIR"

download_backups() {
    log "Downloading backups from S3..."
    
    aws s3 cp "s3://$S3_BUCKET/monitoring/prometheus/prometheus-backup-$BACKUP_DATE.tar.gz" "$BACKUP_DIR/" || error "Failed to download Prometheus backup"
    
    if aws s3 ls "s3://$S3_BUCKET/monitoring/grafana/grafana-backup-$BACKUP_DATE.tar.gz" >/dev/null 2>&1; then
        aws s3 cp "s3://$S3_BUCKET/monitoring/grafana/grafana-backup-$BACKUP_DATE.tar.gz" "$BACKUP_DIR/"
        log "Grafana backup downloaded"
    else
        warn "Grafana backup not found for date $BACKUP_DATE"
    fi
}

restore_prometheus() {
    log "Restoring Prometheus data..."
    
    # Scale down Prometheus
    kubectl scale statefulset prometheus -n monitoring --replicas=0
    kubectl wait --for=delete pod -l app=prometheus -n monitoring --timeout=300s
    
    # Extract backup
    cd "$BACKUP_DIR"
    tar -xzf "prometheus-backup-$BACKUP_DATE.tar.gz"
    
    # Get Prometheus PVC
    PROMETHEUS_PVC="prometheus-storage-prometheus-0"
    
    # Create restore job
    kubectl apply -f - <<EOF
apiVersion: batch/v1
kind: Job
metadata:
  name: prometheus-restore-$BACKUP_DATE
  namespace: monitoring
spec:
  template:
    spec:
      containers:
        - name: restore
          image: alpine:3.18
          command:
            - /bin/sh
            - -c
            - |
              rm -rf /prometheus/*
              cp -r /backup/* /prometheus/
              chown -R 65534:65534 /prometheus
          volumeMounts:
            - name: prometheus-data
              mountPath: /prometheus
            - name: backup-data
              mountPath: /backup
      volumes:
        - name: prometheus-data
          persistentVolumeClaim:
            claimName: $PROMETHEUS_PVC
        - name: backup-data
          hostPath:
            path: $BACKUP_DIR/prometheus-$BACKUP_DATE
      restartPolicy: Never
EOF
    
    # Wait for job completion
    kubectl wait --for=condition=complete job/prometheus-restore-$BACKUP_DATE -n monitoring --timeout=600s
    
    # Scale up Prometheus
    kubectl scale statefulset prometheus -n monitoring --replicas=1
    kubectl wait --for=condition=ready pod -l app=prometheus -n monitoring --timeout=300s
    
    # Cleanup
    kubectl delete job prometheus-restore-$BACKUP_DATE -n monitoring
    
    log "Prometheus restore completed"
}

restore_grafana() {
    log "Restoring Grafana data..."
    
    if [[ ! -f "$BACKUP_DIR/grafana-backup-$BACKUP_DATE.tar.gz" ]]; then
        warn "Grafana backup not available, skipping restore"
        return
    fi
    
    # Extract backup
    cd "$BACKUP_DIR"
    tar -xzf "grafana-backup-$BACKUP_DATE.tar.gz"
    
    # Restore via API (requires Grafana to be running)
    GRAFANA_URL="http://grafana.sportsbook.example.com"
    GRAFANA_API_KEY="${GRAFANA_API_KEY:-}"
    
    if [[ -n "$GRAFANA_API_KEY" ]]; then
        # Restore dashboards
        for dashboard_file in "$BACKUP_DIR/grafana-$BACKUP_DATE"/dashboard-*.json; do
            if [[ -f "$dashboard_file" ]]; then
                curl -X POST -H "Authorization: Bearer $GRAFANA_API_KEY" -H "Content-Type: application/json" -d "@$dashboard_file" "$GRAFANA_URL/api/dashboards/db"
            fi
        done
        
        log "Grafana dashboards restored"
    else
        warn "GRAFANA_API_KEY not set, manual dashboard restore required"
        log "Dashboard files available at: $BACKUP_DIR/grafana-$BACKUP_DATE/"
    fi
}

# Main execution
log "Starting monitoring restore process for backup date: $BACKUP_DATE"

download_backups
restore_prometheus
restore_grafana

log "Restore process completed successfully!"
```

## CI/CD Integration

### GitHub Actions Workflow

Create `.github/workflows/monitoring-deployment.yml`:

```yaml
name: Deploy Monitoring Infrastructure

on:
  push:
    paths:
      - 'k8s/monitoring/**'
      - 'docker/monitoring/**'
      - '.github/workflows/monitoring-deployment.yml'
    branches: [main, develop]
  pull_request:
    paths:
      - 'k8s/monitoring/**'
      - 'docker/monitoring/**'

env:
  REGISTRY: ghcr.io
  IMAGE_NAME: ${{ github.repository }}

jobs:
  validate-configs:
    name: Validate Monitoring Configurations
    runs-on: ubuntu-latest
    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Setup kubectl
        uses: azure/setup-kubectl@v3
        with:
          version: '1.28.0'

      - name: Validate Kubernetes manifests
        run: |
          # Validate all monitoring K8s manifests
          for file in k8s/monitoring/*.yaml; do
            echo "Validating $file"
            kubectl --dry-run=client apply -f "$file"
          done

      - name: Setup Prometheus
        run: |
          wget https://github.com/prometheus/prometheus/releases/download/v2.48.0/prometheus-2.48.0.linux-amd64.tar.gz
          tar xzf prometheus-2.48.0.linux-amd64.tar.gz
          sudo mv prometheus-2.48.0.linux-amd64/promtool /usr/local/bin/

      - name: Validate Prometheus config
        run: |
          promtool check config docker/monitoring/prometheus/prometheus.yml

      - name: Validate Alert rules
        run: |
          promtool check rules docker/monitoring/prometheus/alerts/*.yml

  deploy-dev:
    name: Deploy to Development
    runs-on: ubuntu-latest
    if: github.ref == 'refs/heads/develop'
    needs: validate-configs
    environment: development
    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Configure AWS credentials
        uses: aws-actions/configure-aws-credentials@v4
        with:
          aws-access-key-id: ${{ secrets.AWS_ACCESS_KEY_ID }}
          aws-secret-access-key: ${{ secrets.AWS_SECRET_ACCESS_KEY }}
          aws-region: us-west-2

      - name: Setup kubectl
        uses: azure/setup-kubectl@v3
        with:
          version: '1.28.0'

      - name: Configure kubectl
        run: |
          aws eks update-kubeconfig --region us-west-2 --name sportsbook-dev

      - name: Deploy monitoring namespace
        run: |
          kubectl apply -f k8s/monitoring/namespace.yaml

      - name: Deploy monitoring infrastructure
        run: |
          # Apply configurations
          kubectl apply -f k8s/monitoring/storage-classes.yaml
          kubectl apply -f k8s/monitoring/prometheus-config.yaml
          kubectl apply -f k8s/monitoring/prometheus-deployment.yaml
          kubectl apply -f k8s/monitoring/grafana-deployment.yaml
          kubectl apply -f k8s/monitoring/alertmanager-deployment.yaml
          kubectl apply -f k8s/monitoring/network-policies.yaml

      - name: Wait for deployments
        run: |
          kubectl wait --for=condition=available --timeout=600s deployment/grafana -n monitoring
          kubectl wait --for=condition=ready --timeout=600s pod -l app=prometheus -n monitoring
          kubectl wait --for=condition=available --timeout=600s deployment/alertmanager -n monitoring

      - name: Verify deployment
        run: |
          kubectl get pods -n monitoring
          kubectl get services -n monitoring

  deploy-prod:
    name: Deploy to Production
    runs-on: ubuntu-latest
    if: github.ref == 'refs/heads/main'
    needs: validate-configs
    environment: production
    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Configure AWS credentials
        uses: aws-actions/configure-aws-credentials@v4
        with:
          aws-access-key-id: ${{ secrets.AWS_ACCESS_KEY_ID_PROD }}
          aws-secret-access-key: ${{ secrets.AWS_SECRET_ACCESS_KEY_PROD }}
          aws-region: us-west-2

      - name: Setup kubectl
        uses: azure/setup-kubectl@v3
        with:
          version: '1.28.0'

      - name: Configure kubectl
        run: |
          aws eks update-kubeconfig --region us-west-2 --name sportsbook-prod

      - name: Deploy with blue-green strategy
        run: |
          # Create backup of current configuration
          kubectl get configmap prometheus-config -n monitoring -o yaml > prometheus-config-backup.yaml
          
          # Deploy new configuration
          kubectl apply -f k8s/monitoring/prometheus-config.yaml
          
          # Rolling update
          kubectl rollout restart statefulset/prometheus -n monitoring
          kubectl rollout restart deployment/grafana -n monitoring
          kubectl rollout restart deployment/alertmanager -n monitoring
          
          # Wait and verify
          kubectl rollout status statefulset/prometheus -n monitoring --timeout=600s
          kubectl rollout status deployment/grafana -n monitoring --timeout=600s
          kubectl rollout status deployment/alertmanager -n monitoring --timeout=600s

      - name: Smoke tests
        run: |
          # Wait for services to be ready
          sleep 60
          
          # Test Prometheus
          kubectl port-forward -n monitoring svc/prometheus 9090:9090 &
          sleep 10
          curl -f http://localhost:9090/-/healthy || exit 1
          
          # Test Grafana
          kubectl port-forward -n monitoring svc/grafana 3000:3000 &
          sleep 10
          curl -f http://localhost:3000/api/health || exit 1
          
          # Test AlertManager
          kubectl port-forward -n monitoring svc/alertmanager 9093:9093 &
          sleep 10
          curl -f http://localhost:9093/-/healthy || exit 1

      - name: Notification
        if: success()
        run: |
          echo "Monitoring infrastructure deployed successfully to production"
        # Add Slack notification here if needed
```

### Helm Chart (Optional)

Create `helm/monitoring/Chart.yaml`:

```yaml
apiVersion: v2
name: sportsbook-monitoring
description: Monitoring infrastructure for Sportsbook-Lite
type: application
version: 1.0.0
appVersion: "1.0"
dependencies:
  - name: prometheus
    version: 25.6.0
    repository: https://prometheus-community.github.io/helm-charts
  - name: grafana
    version: 7.0.8
    repository: https://grafana.github.io/helm-charts
  - name: alertmanager
    version: 1.7.0
    repository: https://prometheus-community.github.io/helm-charts
```

## Environment-Specific Configurations

### Development Environment

Create `environments/development/monitoring-values.yaml`:

```yaml
# Development environment overrides
global:
  environment: development
  retention:
    prometheus: 7d
    grafana: 30d
    alertmanager: 7d

prometheus:
  resources:
    requests:
      cpu: 100m
      memory: 512Mi
    limits:
      cpu: 1000m
      memory: 2Gi
  storage:
    size: 10Gi
  config:
    logLevel: debug
    scrapeInterval: 30s

grafana:
  resources:
    requests:
      cpu: 50m
      memory: 128Mi
    limits:
      cpu: 500m
      memory: 512Mi
  auth:
    anonymousEnabled: true  # For development only
  alerting:
    enabled: false

alertmanager:
  resources:
    requests:
      cpu: 50m
      memory: 64Mi
    limits:
      cpu: 200m
      memory: 256Mi
  config:
    # Use console receiver for development
    receivers:
      - name: default
        webhook_configs:
          - url: http://webhook-logger:8080/webhook
```

### Production Environment

Create `environments/production/monitoring-values.yaml`:

```yaml
# Production environment configuration
global:
  environment: production
  retention:
    prometheus: 30d
    grafana: 365d
    alertmanager: 30d

prometheus:
  replicas: 2  # HA setup
  resources:
    requests:
      cpu: 1000m
      memory: 4Gi
    limits:
      cpu: 4000m
      memory: 8Gi
  storage:
    size: 500Gi
    storageClass: fast-ssd
  config:
    logLevel: info
    scrapeInterval: 15s
    evaluationInterval: 30s

grafana:
  replicas: 2  # HA setup
  resources:
    requests:
      cpu: 500m
      memory: 1Gi
    limits:
      cpu: 2000m
      memory: 4Gi
  auth:
    anonymousEnabled: false
    ldapEnabled: true
  alerting:
    enabled: true
  plugins:
    - grafana-piechart-panel
    - grafana-worldmap-panel
    - camptocamp-prometheus-alertmanager-datasource

alertmanager:
  replicas: 3  # HA setup
  resources:
    requests:
      cpu: 200m
      memory: 512Mi
    limits:
      cpu: 1000m
      memory: 1Gi
  storage:
    size: 10Gi
    storageClass: fast-ssd

# Security policies
networkPolicies:
  enabled: true
  
podSecurityPolicies:
  enabled: true

# Backup configuration
backup:
  enabled: true
  schedule: "0 2 * * *"
  retention: 30d
  s3:
    bucket: sportsbook-backups-prod
    region: us-west-2
```

## Deployment Commands

### Local Development

```bash
# Start monitoring stack locally
cd docker
docker-compose -f docker-compose.yml -f docker-compose.monitoring.yml up -d

# Check services
docker-compose ps

# View logs
docker-compose logs -f prometheus
docker-compose logs -f grafana
docker-compose logs -f alertmanager

# Access services
# Prometheus: http://localhost:9090
# Grafana: http://localhost:3000 (admin/admin123)
# AlertManager: http://localhost:9093

# Stop monitoring stack
docker-compose -f docker-compose.yml -f docker-compose.monitoring.yml down
```

### Kubernetes Development

```bash
# Deploy to development cluster
kubectl apply -f k8s/monitoring/namespace.yaml
kubectl apply -f k8s/monitoring/storage-classes.yaml
kubectl apply -f k8s/monitoring/prometheus-config.yaml
kubectl apply -f k8s/monitoring/prometheus-deployment.yaml
kubectl apply -f k8s/monitoring/grafana-deployment.yaml
kubectl apply -f k8s/monitoring/alertmanager-deployment.yaml

# Check deployment
kubectl get pods -n monitoring
kubectl get services -n monitoring

# Port forward for local access
kubectl port-forward -n monitoring svc/prometheus 9090:9090
kubectl port-forward -n monitoring svc/grafana 3000:3000
kubectl port-forward -n monitoring svc/alertmanager 9093:9093
```

### Production Deployment

```bash
# Using Helm
helm upgrade --install sportsbook-monitoring ./helm/monitoring \
  --namespace monitoring \
  --create-namespace \
  -f environments/production/monitoring-values.yaml

# Using kubectl with kustomize
kubectl apply -k k8s/monitoring/overlays/production

# Verify deployment
kubectl get pods -n monitoring
kubectl logs -n monitoring deployment/grafana
kubectl logs -n monitoring statefulset/prometheus

# Check ingress
kubectl get ingress -n monitoring
```

### Backup and Restore

```bash
# Manual backup
./scripts/monitoring/backup.sh

# Manual restore
./scripts/monitoring/restore.sh 20231201-140000

# List available backups
aws s3 ls s3://sportsbook-backups/monitoring/prometheus/
aws s3 ls s3://sportsbook-backups/monitoring/grafana/
```

### Maintenance Operations

```bash
# Scale monitoring components
kubectl scale statefulset prometheus --replicas=2 -n monitoring
kubectl scale deployment grafana --replicas=2 -n monitoring
kubectl scale deployment alertmanager --replicas=3 -n monitoring

# Update configurations
kubectl rollout restart statefulset/prometheus -n monitoring
kubectl rollout restart deployment/grafana -n monitoring
kubectl rollout restart deployment/alertmanager -n monitoring

# Check rollout status
kubectl rollout status statefulset/prometheus -n monitoring
kubectl rollout status deployment/grafana -n monitoring
kubectl rollout status deployment/alertmanager -n monitoring

# View metrics and alerts
kubectl exec -n monitoring deployment/prometheus -- promtool query instant 'up'
kubectl exec -n monitoring deployment/alertmanager -- amtool alert query
```

This comprehensive monitoring infrastructure setup provides:

1. **Complete Docker Compose setup** for local development with all monitoring components
2. **Production-ready Kubernetes manifests** with proper resource limits, health checks, and security policies
3. **Comprehensive Prometheus configuration** with service discovery and Orleans-specific metrics
4. **AlertManager setup** with routing rules for different severity levels and notification channels
5. **Persistent storage configuration** with backup and restore procedures
6. **Network and security policies** to secure the monitoring infrastructure
7. **CI/CD integration** with GitHub Actions for automated deployment
8. **Environment-specific configurations** for development and production
9. **Operational procedures** for deployment, maintenance, and troubleshooting

The configuration is immediately usable and follows DevOps best practices for monitoring Orleans-based applications.