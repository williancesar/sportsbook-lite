# Loki Implementation Context Document

## Current State Analysis

### Existing Logging Setup
- **Framework**: Serilog is already configured in both Host and API projects
- **Current Sinks**: Console sink only
- **Log Levels**: Information (default), Warning (Microsoft/Orleans)
- **Enrichment**: FromLogContext
- **Format**: Timestamp, Level, SourceContext, Message with structured logging

### Projects Using Serilog
1. **SportsbookLite.Host** (Orleans Silo)
   - Location: `/src/SportsbookLite.Host/Program.cs`
   - Configuration: Hardcoded in Program.cs
   - Metrics: Prometheus on port 9090

2. **SportsbookLite.Api** (FastEndpoints API)
   - Location: `/src/SportsbookLite.Api/Program.cs`
   - Configuration: ReadFrom.Configuration (appsettings.json)
   - Metrics: Prometheus on /metrics endpoint

### Existing Infrastructure
- **Monitoring Stack**: Prometheus + Grafana already configured
- **Containerization**: Docker and Docker Compose present
- **Orchestration**: Kubernetes manifests available
- **Database**: PostgreSQL
- **Messaging**: Apache Pulsar
- **Clustering**: Redis for Orleans

## Loki Integration Requirements

### Package Information
- **NuGet Package**: Serilog.Sinks.Grafana.Loki
- **Latest Version**: 8.3.1 (as of 2024)
- **Compatibility**: .NET 9 compatible via .NET Standard
- **Default Formatter**: LokiJsonTextFormatter (JSON payloads for Loki v2)

### Key Features Needed
1. **HTTP Transport**: Send logs to Loki via HTTP API
2. **Batching**: Efficient batching of log entries
3. **Compression**: Optional gzip compression support
4. **Labels**: Proper labeling for filtering (service, environment, version)
5. **Local Development**: Docker Compose setup for Loki
6. **Production**: Kubernetes deployment configuration

### Configuration Requirements
1. **Environment-specific URLs**:
   - Development: http://localhost:3100
   - Production: http://loki:3100 (Kubernetes service)

2. **Labels to Include**:
   - service_name (sportsbook-host, sportsbook-api)
   - environment (development, staging, production)
   - version (from assembly)
   - hostname
   - orleans_cluster_id
   - orleans_service_id

3. **Performance Considerations**:
   - Batch size configuration
   - Flush interval
   - Queue limit
   - HTTP timeout

## Implementation Tasks

### Phase 1: Package Installation
- Add Serilog.Sinks.Grafana.Loki NuGet package to both projects
- Version: 8.3.1

### Phase 2: Configuration Updates
- Update appsettings.json for all environments
- Add Loki sink configuration
- Configure labels and formatting

### Phase 3: Docker Setup
- Add Loki container to docker-compose.yml
- Configure Loki with local storage
- Set up Grafana datasource

### Phase 4: Kubernetes Deployment
- Create Loki deployment manifest
- Configure persistent storage
- Set up service and ingress

### Phase 5: Grafana Integration
- Configure Loki datasource in Grafana
- Create log dashboards
- Set up log alerts

## Agent Collaboration Points

### Backend Architect
- Design log aggregation architecture
- Define log retention policies
- Plan scaling strategy for Loki

### C# Pro
- Implement custom formatters if needed
- Configure structured logging best practices
- Add correlation IDs for distributed tracing

### DevOps
- Docker and Kubernetes configurations
- Network policies and security
- Backup and retention strategies

### Documentation
- Update README with logging instructions
- Create operational runbook
- Document troubleshooting procedures

## Questions to Address
1. Log retention period requirements?
2. Storage backend for Loki (filesystem, S3, Azure Blob)?
3. Authentication/authorization for Loki endpoints?
4. Alert rules for critical log patterns?
5. Integration with existing Orleans telemetry?

## Success Criteria
- [ ] Logs visible in Grafana from all environments
- [ ] Proper filtering by service and environment
- [ ] Correlation between metrics and logs
- [ ] No performance degradation
- [ ] Local development experience maintained
- [ ] Production-ready configuration