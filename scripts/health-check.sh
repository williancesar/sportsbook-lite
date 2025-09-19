#!/bin/bash
# Health check script for SportsbookLite services
# Usage: ./health-check.sh [service] [environment]
# Example: ./health-check.sh api development

set -e

# Configuration
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"

# Default values
SERVICE="${1:-all}"
ENVIRONMENT="${2:-development}"
TIMEOUT="${3:-30}"

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

# Get service URLs based on environment
get_service_url() {
    local service="$1"
    local env="$2"
    
    case "$env" in
        "development")
            case "$service" in
                "api") echo "http://localhost:5000" ;;
                "orleans") echo "http://localhost:30000" ;;
                "postgres") echo "localhost:5432" ;;
                "redis") echo "localhost:6379" ;;
                "pulsar") echo "http://localhost:8080" ;;
            esac
            ;;
        "staging")
            case "$service" in
                "api") echo "https://api-staging.sportsbook.example.com" ;;
                "orleans") echo "https://orleans-staging.sportsbook.example.com" ;;
                "postgres") echo "postgres-service.sportsbook-lite-staging.svc.cluster.local:5432" ;;
                "redis") echo "redis-service.sportsbook-lite-staging.svc.cluster.local:6379" ;;
                "pulsar") echo "http://pulsar-service.sportsbook-lite-staging.svc.cluster.local:8080" ;;
            esac
            ;;
        "production")
            case "$service" in
                "api") echo "https://api.sportsbook.example.com" ;;
                "orleans") echo "https://orleans.sportsbook.example.com" ;;
                "postgres") echo "postgres-service.sportsbook-lite.svc.cluster.local:5432" ;;
                "redis") echo "redis-service.sportsbook-lite.svc.cluster.local:6379" ;;
                "pulsar") echo "http://pulsar-service.sportsbook-lite.svc.cluster.local:8080" ;;
            esac
            ;;
    esac
}

# Check if a URL is accessible
check_url() {
    local url="$1"
    local service="$2"
    local endpoint="${3:-/health}"
    
    log_info "Checking $service health at $url$endpoint"
    
    if curl -f -s --max-time "$TIMEOUT" "$url$endpoint" > /dev/null 2>&1; then
        log_success "$service is healthy"
        return 0
    else
        log_error "$service health check failed"
        return 1
    fi
}

# Check PostgreSQL health
check_postgres() {
    local host="$1"
    log_info "Checking PostgreSQL health at $host"
    
    if command -v pg_isready > /dev/null 2>&1; then
        if pg_isready -h "${host%%:*}" -p "${host##*:}" > /dev/null 2>&1; then
            log_success "PostgreSQL is healthy"
            return 0
        else
            log_error "PostgreSQL health check failed"
            return 1
        fi
    else
        log_warning "pg_isready not available, skipping PostgreSQL health check"
        return 0
    fi
}

# Check Redis health
check_redis() {
    local host="$1"
    log_info "Checking Redis health at $host"
    
    if command -v redis-cli > /dev/null 2>&1; then
        if redis-cli -h "${host%%:*}" -p "${host##*:}" ping > /dev/null 2>&1; then
            log_success "Redis is healthy"
            return 0
        else
            log_error "Redis health check failed"
            return 1
        fi
    else
        log_warning "redis-cli not available, skipping Redis health check"
        return 0
    fi
}

# Check Pulsar health
check_pulsar() {
    local url="$1"
    log_info "Checking Pulsar health at $url"
    
    if curl -f -s --max-time "$TIMEOUT" "$url/admin/v2/brokers/health" > /dev/null 2>&1; then
        log_success "Pulsar is healthy"
        return 0
    else
        log_error "Pulsar health check failed"
        return 1
    fi
}

# Check API health with detailed information
check_api_detailed() {
    local url="$1"
    log_info "Performing detailed API health check at $url"
    
    # Check basic health endpoint
    if ! check_url "$url" "API" "/health"; then
        return 1
    fi
    
    # Check readiness endpoint
    if check_url "$url" "API Readiness" "/health/ready"; then
        log_success "API is ready"
    else
        log_warning "API is not ready"
    fi
    
    # Check liveness endpoint
    if check_url "$url" "API Liveness" "/health/live"; then
        log_success "API is live"
    else
        log_warning "API liveness check failed"
    fi
    
    # Get detailed health information
    log_info "Retrieving detailed health information..."
    local health_response=$(curl -s --max-time "$TIMEOUT" "$url/health" 2>/dev/null)
    if [ $? -eq 0 ] && [ -n "$health_response" ]; then
        echo "$health_response" | jq '.' 2>/dev/null || echo "$health_response"
    fi
    
    return 0
}

# Check Orleans health with detailed information
check_orleans_detailed() {
    local url="$1"
    log_info "Performing detailed Orleans health check at $url"
    
    # Check basic health endpoint
    if ! check_url "$url" "Orleans" "/health"; then
        return 1
    fi
    
    # Check Orleans-specific endpoints
    if check_url "$url" "Orleans Cluster" "/health/orleans"; then
        log_success "Orleans cluster is healthy"
    else
        log_warning "Orleans cluster health check failed"
    fi
    
    # Check grain directory
    if check_url "$url" "Grain Directory" "/health/grains"; then
        log_success "Grain directory is healthy"
    else
        log_warning "Grain directory health check failed"
    fi
    
    return 0
}

# Main health check function
run_health_checks() {
    local service="$1"
    local environment="$2"
    local failed_checks=0
    
    log_info "Starting health checks for $service in $environment environment"
    
    case "$service" in
        "api")
            api_url=$(get_service_url "api" "$environment")
            if [ -n "$api_url" ]; then
                check_api_detailed "$api_url" || ((failed_checks++))
            else
                log_error "Could not determine API URL for environment: $environment"
                ((failed_checks++))
            fi
            ;;
        "orleans")
            orleans_url=$(get_service_url "orleans" "$environment")
            if [ -n "$orleans_url" ]; then
                check_orleans_detailed "$orleans_url" || ((failed_checks++))
            else
                log_error "Could not determine Orleans URL for environment: $environment"
                ((failed_checks++))
            fi
            ;;
        "postgres")
            postgres_host=$(get_service_url "postgres" "$environment")
            if [ -n "$postgres_host" ]; then
                check_postgres "$postgres_host" || ((failed_checks++))
            else
                log_error "Could not determine PostgreSQL host for environment: $environment"
                ((failed_checks++))
            fi
            ;;
        "redis")
            redis_host=$(get_service_url "redis" "$environment")
            if [ -n "$redis_host" ]; then
                check_redis "$redis_host" || ((failed_checks++))
            else
                log_error "Could not determine Redis host for environment: $environment"
                ((failed_checks++))
            fi
            ;;
        "pulsar")
            pulsar_url=$(get_service_url "pulsar" "$environment")
            if [ -n "$pulsar_url" ]; then
                check_pulsar "$pulsar_url" || ((failed_checks++))
            else
                log_error "Could not determine Pulsar URL for environment: $environment"
                ((failed_checks++))
            fi
            ;;
        "all")
            for svc in api orleans postgres redis pulsar; do
                run_health_checks "$svc" "$environment" || ((failed_checks++))
            done
            ;;
        *)
            log_error "Unknown service: $service"
            log_info "Available services: api, orleans, postgres, redis, pulsar, all"
            return 1
            ;;
    esac
    
    return $failed_checks
}

# Continuous monitoring function
continuous_monitoring() {
    local service="$1"
    local environment="$2"
    local interval="${3:-30}"
    
    log_info "Starting continuous monitoring for $service in $environment environment (interval: ${interval}s)"
    log_info "Press Ctrl+C to stop monitoring"
    
    while true; do
        echo "=================================="
        echo "$(date): Health Check Report"
        echo "=================================="
        
        run_health_checks "$service" "$environment"
        
        echo ""
        log_info "Next check in ${interval} seconds..."
        sleep "$interval"
    done
}

# Show usage information
show_usage() {
    cat << EOF
SportsbookLite Health Check Script

Usage: $0 [OPTIONS] [service] [environment]

Arguments:
  service      Service to check (api, orleans, postgres, redis, pulsar, all) [default: all]
  environment  Environment (development, staging, production) [default: development]

Options:
  -t, --timeout SECONDS    Request timeout in seconds [default: 30]
  -c, --continuous         Run continuous monitoring
  -i, --interval SECONDS   Monitoring interval in seconds [default: 30]
  -h, --help              Show this help message

Examples:
  $0                           # Check all services in development
  $0 api production           # Check API in production
  $0 -c all staging           # Continuous monitoring of all services in staging
  $0 -t 60 orleans production # Check Orleans with 60-second timeout

EOF
}

# Parse command line arguments
CONTINUOUS=false
INTERVAL=30

while [[ $# -gt 0 ]]; do
    case $1 in
        -t|--timeout)
            TIMEOUT="$2"
            shift 2
            ;;
        -c|--continuous)
            CONTINUOUS=true
            shift
            ;;
        -i|--interval)
            INTERVAL="$2"
            shift 2
            ;;
        -h|--help)
            show_usage
            exit 0
            ;;
        -*)
            log_error "Unknown option: $1"
            show_usage
            exit 1
            ;;
        *)
            if [ -z "$SERVICE" ]; then
                SERVICE="$1"
            elif [ -z "$ENVIRONMENT" ]; then
                ENVIRONMENT="$1"
            else
                log_error "Too many arguments"
                show_usage
                exit 1
            fi
            shift
            ;;
    esac
done

# Set defaults
SERVICE="${SERVICE:-all}"
ENVIRONMENT="${ENVIRONMENT:-development}"

# Validate arguments
if [[ ! "$SERVICE" =~ ^(api|orleans|postgres|redis|pulsar|all)$ ]]; then
    log_error "Invalid service: $SERVICE"
    show_usage
    exit 1
fi

if [[ ! "$ENVIRONMENT" =~ ^(development|staging|production)$ ]]; then
    log_error "Invalid environment: $ENVIRONMENT"
    show_usage
    exit 1
fi

# Main execution
log_info "SportsbookLite Health Check Script"
log_info "Service: $SERVICE"
log_info "Environment: $ENVIRONMENT"
log_info "Timeout: ${TIMEOUT}s"

if [ "$CONTINUOUS" = true ]; then
    continuous_monitoring "$SERVICE" "$ENVIRONMENT" "$INTERVAL"
else
    run_health_checks "$SERVICE" "$ENVIRONMENT"
    exit_code=$?
    
    if [ $exit_code -eq 0 ]; then
        log_success "All health checks passed!"
    else
        log_error "Health checks failed! ($exit_code failed checks)"
    fi
    
    exit $exit_code
fi