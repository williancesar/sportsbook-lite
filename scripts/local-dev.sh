#!/bin/bash
# Local development script for SportsbookLite
# Usage: ./local-dev.sh [action]
# Example: ./local-dev.sh start

set -e

# Configuration
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"

# Default values
ACTION="${1:-help}"
COMPOSE_FILE="$PROJECT_ROOT/docker/docker-compose.yml"
COMPOSE_OVERRIDE="$PROJECT_ROOT/docker/docker-compose.override.yml"

# Color codes for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Logging functions
log_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

log_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

log_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Check prerequisites
check_prerequisites() {
    log_info "Checking prerequisites..."
    
    # Check if Docker is installed and running
    if ! command -v docker &> /dev/null; then
        log_error "Docker is not installed"
        exit 1
    fi
    
    if ! docker info &> /dev/null; then
        log_error "Docker daemon is not running"
        exit 1
    fi
    
    # Check if docker-compose is available
    if command -v docker-compose &> /dev/null; then
        DOCKER_COMPOSE="docker-compose"
    elif docker compose version &> /dev/null; then
        DOCKER_COMPOSE="docker compose"
    else
        log_error "Docker Compose is not available"
        exit 1
    fi
    
    # Check if .NET is installed
    if ! command -v dotnet &> /dev/null; then
        log_warning ".NET is not installed - building images will use Docker only"
    fi
    
    log_success "Prerequisites check passed"
}

# Start all services
start_services() {
    log_info "Starting SportsbookLite services..."
    
    # Pull latest images
    $DOCKER_COMPOSE -f "$COMPOSE_FILE" -f "$COMPOSE_OVERRIDE" pull
    
    # Build and start services
    $DOCKER_COMPOSE -f "$COMPOSE_FILE" -f "$COMPOSE_OVERRIDE" up -d --build
    
    log_success "Services started successfully"
    
    # Wait for services to be ready
    wait_for_services
    
    # Show service URLs
    show_service_urls
}

# Stop all services
stop_services() {
    log_info "Stopping SportsbookLite services..."
    
    $DOCKER_COMPOSE -f "$COMPOSE_FILE" -f "$COMPOSE_OVERRIDE" down
    
    log_success "Services stopped successfully"
}

# Restart all services
restart_services() {
    log_info "Restarting SportsbookLite services..."
    
    stop_services
    start_services
}

# Show service status
show_status() {
    log_info "Service Status:"
    
    $DOCKER_COMPOSE -f "$COMPOSE_FILE" -f "$COMPOSE_OVERRIDE" ps
}

# Show service logs
show_logs() {
    local service="${2:-}"
    
    if [ -n "$service" ]; then
        log_info "Showing logs for $service..."
        $DOCKER_COMPOSE -f "$COMPOSE_FILE" -f "$COMPOSE_OVERRIDE" logs -f "$service"
    else
        log_info "Showing logs for all services..."
        $DOCKER_COMPOSE -f "$COMPOSE_FILE" -f "$COMPOSE_OVERRIDE" logs -f
    fi
}

# Wait for services to be ready
wait_for_services() {
    log_info "Waiting for services to be ready..."
    
    # Wait for PostgreSQL
    log_info "Waiting for PostgreSQL..."
    wait_for_service "postgres" "pg_isready -U dev -d sportsbook" 60
    
    # Wait for Redis
    log_info "Waiting for Redis..."
    wait_for_service "redis" "redis-cli ping" 30
    
    # Wait for Pulsar
    log_info "Waiting for Pulsar..."
    wait_for_service "pulsar" "bin/pulsar-admin brokers healthcheck" 120
    
    # Wait for Orleans Silo
    log_info "Waiting for Orleans Silo..."
    wait_for_url "http://localhost:30000/health" 120
    
    # Wait for API
    log_info "Waiting for API..."
    wait_for_url "http://localhost:5000/health" 60
    
    log_success "All services are ready"
}

# Wait for a specific service to be ready
wait_for_service() {
    local service="$1"
    local check_command="$2"
    local timeout="$3"
    local counter=0
    
    while [ $counter -lt $timeout ]; do
        if $DOCKER_COMPOSE -f "$COMPOSE_FILE" -f "$COMPOSE_OVERRIDE" exec -T "$service" $check_command &> /dev/null; then
            log_success "$service is ready"
            return 0
        fi
        
        sleep 2
        counter=$((counter + 2))
    done
    
    log_error "$service failed to start within ${timeout} seconds"
    return 1
}

# Wait for URL to be accessible
wait_for_url() {
    local url="$1"
    local timeout="$2"
    local counter=0
    
    while [ $counter -lt $timeout ]; do
        if curl -f -s "$url" > /dev/null 2>&1; then
            log_success "Service at $url is ready"
            return 0
        fi
        
        sleep 2
        counter=$((counter + 2))
    done
    
    log_error "Service at $url failed to start within ${timeout} seconds"
    return 1
}

# Show service URLs
show_service_urls() {
    echo ""
    log_info "Service URLs:"
    echo "=================================="
    echo "API:                 http://localhost:5000"
    echo "API Health:          http://localhost:5000/health"
    echo "Orleans Dashboard:   http://localhost:8081/dashboard"
    echo "Orleans Health:      http://localhost:30000/health"
    echo "Swagger UI:          http://localhost:5000/swagger"
    echo ""
    echo "Database:"
    echo "PostgreSQL:          localhost:5432 (user: dev, db: sportsbook)"
    echo "Adminer:             http://localhost:8082"
    echo ""
    echo "Message Broker:"
    echo "Pulsar Admin:        http://localhost:8080"
    echo "Pulsar Manager:      http://localhost:9527"
    echo ""
    echo "Cache:"
    echo "Redis:               localhost:6379"
    echo "Redis Commander:     http://localhost:8083"
    echo "=================================="
}

# Run health checks
run_health_checks() {
    log_info "Running health checks..."
    
    if [ -x "$PROJECT_ROOT/scripts/health-check.sh" ]; then
        "$PROJECT_ROOT/scripts/health-check.sh" all development
    else
        log_warning "Health check script not found or not executable"
        
        # Basic health checks
        curl -f http://localhost:5000/health || log_error "API health check failed"
        curl -f http://localhost:30000/health || log_error "Orleans health check failed"
    fi
}

# Build and test application
build_and_test() {
    log_info "Building and testing application..."
    
    if command -v dotnet &> /dev/null; then
        # Restore dependencies
        log_info "Restoring dependencies..."
        dotnet restore "$PROJECT_ROOT"
        
        # Build solution
        log_info "Building solution..."
        dotnet build "$PROJECT_ROOT" --configuration Debug --no-restore
        
        # Run tests
        log_info "Running tests..."
        dotnet test "$PROJECT_ROOT" --configuration Debug --no-build --verbosity minimal
        
        log_success "Build and test completed successfully"
    else
        log_warning ".NET CLI not available - using Docker build only"
        
        # Build Docker images
        log_info "Building Docker images..."
        $DOCKER_COMPOSE -f "$COMPOSE_FILE" -f "$COMPOSE_OVERRIDE" build
        
        log_success "Docker build completed successfully"
    fi
}

# Clean up local environment
cleanup() {
    log_info "Cleaning up local environment..."
    
    # Stop and remove containers
    $DOCKER_COMPOSE -f "$COMPOSE_FILE" -f "$COMPOSE_OVERRIDE" down -v --remove-orphans
    
    # Remove images (with confirmation)
    read -p "Remove Docker images? (y/N): " -n 1 -r
    echo
    if [[ $REPLY =~ ^[Yy]$ ]]; then
        $DOCKER_COMPOSE -f "$COMPOSE_FILE" -f "$COMPOSE_OVERRIDE" down --rmi all
        log_success "Images removed"
    fi
    
    # Clean build artifacts
    if command -v dotnet &> /dev/null; then
        log_info "Cleaning .NET build artifacts..."
        dotnet clean "$PROJECT_ROOT"
        find "$PROJECT_ROOT" -name "bin" -type d -exec rm -rf {} + 2>/dev/null || true
        find "$PROJECT_ROOT" -name "obj" -type d -exec rm -rf {} + 2>/dev/null || true
        log_success ".NET artifacts cleaned"
    fi
    
    log_success "Cleanup completed"
}

# Reset database
reset_database() {
    log_info "Resetting database..."
    
    # Stop API and Orleans to prevent connections
    $DOCKER_COMPOSE -f "$COMPOSE_FILE" -f "$COMPOSE_OVERRIDE" stop sportsbook-api orleans-silo
    
    # Reset PostgreSQL data
    $DOCKER_COMPOSE -f "$COMPOSE_FILE" -f "$COMPOSE_OVERRIDE" exec postgres psql -U dev -d sportsbook -c "
        DROP SCHEMA IF EXISTS public CASCADE;
        CREATE SCHEMA public;
        GRANT ALL ON SCHEMA public TO dev;
        GRANT ALL ON SCHEMA public TO public;
    "
    
    # Restart services
    $DOCKER_COMPOSE -f "$COMPOSE_FILE" -f "$COMPOSE_OVERRIDE" start orleans-silo sportsbook-api
    
    log_success "Database reset completed"
}

# Start individual service
start_service() {
    local service="$2"
    
    if [ -z "$service" ]; then
        log_error "Service name is required"
        exit 1
    fi
    
    log_info "Starting $service..."
    $DOCKER_COMPOSE -f "$COMPOSE_FILE" -f "$COMPOSE_OVERRIDE" up -d "$service"
    log_success "$service started"
}

# Stop individual service
stop_service() {
    local service="$2"
    
    if [ -z "$service" ]; then
        log_error "Service name is required"
        exit 1
    fi
    
    log_info "Stopping $service..."
    $DOCKER_COMPOSE -f "$COMPOSE_FILE" -f "$COMPOSE_OVERRIDE" stop "$service"
    log_success "$service stopped"
}

# Show usage information
show_usage() {
    cat << EOF
SportsbookLite Local Development Script

Usage: $0 [action] [service]

Actions:
  start         Start all services (default)
  stop          Stop all services
  restart       Restart all services
  status        Show service status
  logs          Show logs for all services or specific service
  health        Run health checks
  build         Build and test application
  cleanup       Clean up local environment (containers, volumes, images)
  reset-db      Reset database to clean state
  urls          Show service URLs
  start-svc     Start individual service
  stop-svc      Stop individual service

Service names (for logs, start-svc, stop-svc):
  postgres      PostgreSQL database
  redis         Redis cache
  pulsar        Pulsar message broker
  orleans-silo  Orleans Silo Host
  sportsbook-api FastEndpoints API

Examples:
  $0 start                    # Start all services
  $0 logs sportsbook-api      # Show API logs
  $0 start-svc postgres       # Start only PostgreSQL
  $0 health                   # Run health checks
  $0 reset-db                 # Reset database

EOF
}

# Main execution
main() {
    case "$ACTION" in
        "start")
            check_prerequisites
            start_services
            ;;
        "stop")
            check_prerequisites
            stop_services
            ;;
        "restart")
            check_prerequisites
            restart_services
            ;;
        "status")
            check_prerequisites
            show_status
            ;;
        "logs")
            check_prerequisites
            show_logs "$@"
            ;;
        "health")
            check_prerequisites
            run_health_checks
            ;;
        "build")
            check_prerequisites
            build_and_test
            ;;
        "cleanup")
            check_prerequisites
            cleanup
            ;;
        "reset-db")
            check_prerequisites
            reset_database
            ;;
        "urls")
            show_service_urls
            ;;
        "start-svc")
            check_prerequisites
            start_service "$@"
            ;;
        "stop-svc")
            check_prerequisites
            stop_service "$@"
            ;;
        "help"|"-h"|"--help")
            show_usage
            exit 0
            ;;
        *)
            log_error "Unknown action: $ACTION"
            show_usage
            exit 1
            ;;
    esac
}

# Execute main function
log_info "SportsbookLite Local Development Script"
log_info "Action: $ACTION"
log_info "Timestamp: $(date)"

main "$@"