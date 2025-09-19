#!/bin/bash
set -euo pipefail

# Sportsbook-Lite Monitoring Local Deployment Script

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
DOCKER_DIR="$PROJECT_ROOT/docker"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
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

info() {
    echo -e "${BLUE}[$(date +'%Y-%m-%d %H:%M:%S')] INFO: $1${NC}"
}

# Check prerequisites
command -v docker >/dev/null 2>&1 || error "Docker is required but not installed"
command -v docker-compose >/dev/null 2>&1 || error "Docker Compose is required but not installed"

# Change to docker directory
cd "$DOCKER_DIR"

# Function to wait for service to be ready
wait_for_service() {
    local service_name=$1
    local health_url=$2
    local max_attempts=30
    local attempt=0
    
    log "Waiting for $service_name to be ready..."
    
    while [ $attempt -lt $max_attempts ]; do
        if curl -f -s "$health_url" >/dev/null 2>&1; then
            log "$service_name is ready!"
            return 0
        fi
        
        attempt=$((attempt + 1))
        echo -n "."
        sleep 5
    done
    
    error "$service_name failed to start within expected time"
}

# Check if main sportsbook network exists
if ! docker network ls --format "{{.Name}}" | grep -q "^sportsbook-network$"; then
    warn "Main sportsbook network not found. Creating network..."
    docker network create sportsbook-network --driver bridge --subnet 172.20.0.0/16
fi

log "Starting monitoring infrastructure locally..."

# Start monitoring stack
log "Starting monitoring services..."
docker-compose -f docker-compose.monitoring.yml up -d

# Wait for services to be ready
log "Waiting for services to start..."
sleep 10

# Check service status
log "Checking service health..."
docker-compose -f docker-compose.monitoring.yml ps

# Wait for key services
wait_for_service "Prometheus" "http://localhost:9090/-/healthy"
wait_for_service "Grafana" "http://localhost:3000/api/health"
wait_for_service "AlertManager" "http://localhost:9093/-/healthy"

log "Monitoring infrastructure deployed successfully!"

# Display access information
echo
info "=== Monitoring Services Access Information ==="
info "Prometheus:    http://localhost:9090"
info "Grafana:       http://localhost:3000 (admin/admin123)"
info "AlertManager:  http://localhost:9093"
info "Node Exporter: http://localhost:9100/metrics"
info "cAdvisor:      http://localhost:8084"
info "=============================================="

# Display helpful commands
echo
info "=== Useful Commands ==="
info "View logs:           docker-compose -f docker-compose.monitoring.yml logs -f [service]"
info "Stop monitoring:     docker-compose -f docker-compose.monitoring.yml down"
info "Restart service:     docker-compose -f docker-compose.monitoring.yml restart [service]"
info "Scale service:       docker-compose -f docker-compose.monitoring.yml up -d --scale [service]=2"
info "====================="

log "Local monitoring deployment completed!"