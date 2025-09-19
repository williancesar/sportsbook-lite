#!/bin/bash
# Deployment script for SportsbookLite
# Usage: ./deploy.sh [environment] [action]
# Example: ./deploy.sh staging deploy

set -e

# Configuration
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"

# Default values
ENVIRONMENT="${1:-development}"
ACTION="${2:-deploy}"
NAMESPACE="sportsbook-lite"
TIMEOUT=600

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
    
    # Check if kubectl is installed and configured
    if ! command -v kubectl &> /dev/null; then
        log_error "kubectl is not installed"
        exit 1
    fi
    
    # Check if docker is installed
    if ! command -v docker &> /dev/null; then
        log_error "docker is not installed"
        exit 1
    fi
    
    # Check if jq is installed
    if ! command -v jq &> /dev/null; then
        log_warning "jq is not installed - some features may not work properly"
    fi
    
    # Test kubectl connection
    if ! kubectl cluster-info &> /dev/null; then
        log_error "Cannot connect to Kubernetes cluster"
        exit 1
    fi
    
    log_success "Prerequisites check passed"
}

# Set namespace based on environment
set_namespace() {
    case "$ENVIRONMENT" in
        "development")
            NAMESPACE="sportsbook-lite-dev"
            ;;
        "staging")
            NAMESPACE="sportsbook-lite-staging"
            ;;
        "production")
            NAMESPACE="sportsbook-lite"
            ;;
        *)
            log_error "Unknown environment: $ENVIRONMENT"
            exit 1
            ;;
    esac
    
    log_info "Using namespace: $NAMESPACE"
}

# Create namespace if it doesn't exist
create_namespace() {
    log_info "Creating namespace $NAMESPACE if it doesn't exist..."
    
    if ! kubectl get namespace "$NAMESPACE" &> /dev/null; then
        kubectl create namespace "$NAMESPACE"
        kubectl label namespace "$NAMESPACE" app.kubernetes.io/name=sportsbook-lite
        log_success "Created namespace $NAMESPACE"
    else
        log_info "Namespace $NAMESPACE already exists"
    fi
}

# Apply configuration files
apply_configs() {
    log_info "Applying configuration files..."
    
    # Apply in correct order for dependencies
    kubectl apply -f "$PROJECT_ROOT/k8s/configmaps.yaml" -n "$NAMESPACE"
    kubectl apply -f "$PROJECT_ROOT/k8s/secrets.yaml" -n "$NAMESPACE"
    
    log_success "Configuration files applied"
}

# Deploy infrastructure services
deploy_infrastructure() {
    log_info "Deploying infrastructure services..."
    
    # Deploy PostgreSQL
    log_info "Deploying PostgreSQL..."
    kubectl apply -f "$PROJECT_ROOT/k8s/postgres-deployment.yaml" -n "$NAMESPACE"
    
    # Deploy Redis
    log_info "Deploying Redis..."
    kubectl apply -f "$PROJECT_ROOT/k8s/redis-deployment.yaml" -n "$NAMESPACE"
    
    # Deploy Pulsar
    log_info "Deploying Pulsar..."
    kubectl apply -f "$PROJECT_ROOT/k8s/pulsar-deployment.yaml" -n "$NAMESPACE"
    
    log_success "Infrastructure services deployed"
}

# Wait for infrastructure to be ready
wait_for_infrastructure() {
    log_info "Waiting for infrastructure services to be ready..."
    
    # Wait for PostgreSQL
    log_info "Waiting for PostgreSQL..."
    kubectl wait --for=condition=ready pod -l app.kubernetes.io/name=postgres -n "$NAMESPACE" --timeout=${TIMEOUT}s
    
    # Wait for Redis
    log_info "Waiting for Redis..."
    kubectl wait --for=condition=ready pod -l app.kubernetes.io/name=redis -n "$NAMESPACE" --timeout=${TIMEOUT}s
    
    # Wait for Pulsar
    log_info "Waiting for Pulsar..."
    kubectl wait --for=condition=ready pod -l app.kubernetes.io/name=pulsar -n "$NAMESPACE" --timeout=${TIMEOUT}s
    
    log_success "Infrastructure services are ready"
}

# Deploy application services
deploy_application() {
    log_info "Deploying application services..."
    
    # Deploy Orleans cluster
    log_info "Deploying Orleans cluster..."
    kubectl apply -f "$PROJECT_ROOT/k8s/orleans-cluster.yaml" -n "$NAMESPACE"
    
    # Wait for Orleans cluster to be ready
    log_info "Waiting for Orleans cluster..."
    kubectl wait --for=condition=ready pod -l app.kubernetes.io/name=orleans-silo -n "$NAMESPACE" --timeout=${TIMEOUT}s
    
    # Deploy API
    log_info "Deploying API..."
    kubectl apply -f "$PROJECT_ROOT/k8s/api-deployment.yaml" -n "$NAMESPACE"
    
    # Wait for API to be ready
    log_info "Waiting for API..."
    kubectl wait --for=condition=available deployment/sportsbook-api -n "$NAMESPACE" --timeout=${TIMEOUT}s
    
    log_success "Application services deployed"
}

# Apply ingress configuration
apply_ingress() {
    if [ "$ENVIRONMENT" != "development" ]; then
        log_info "Applying ingress configuration..."
        kubectl apply -f "$PROJECT_ROOT/k8s/ingress.yaml" -n "$NAMESPACE"
        log_success "Ingress configuration applied"
    else
        log_info "Skipping ingress for development environment"
    fi
}

# Apply monitoring configuration
apply_monitoring() {
    if command -v kubectl &> /dev/null && kubectl get crd servicemonitors.monitoring.coreos.com &> /dev/null; then
        log_info "Applying monitoring configuration..."
        kubectl apply -f "$PROJECT_ROOT/k8s/monitoring.yaml" -n "$NAMESPACE"
        log_success "Monitoring configuration applied"
    else
        log_warning "Prometheus operator not detected, skipping monitoring configuration"
    fi
}

# Run smoke tests
run_smoke_tests() {
    log_info "Running smoke tests..."
    
    # Port forward to API for testing
    kubectl port-forward service/sportsbook-api-service 8080:80 -n "$NAMESPACE" &
    PORT_FORWARD_PID=$!
    
    # Wait for port forwarding to be established
    sleep 5
    
    # Run health checks
    if "$PROJECT_ROOT/scripts/health-check.sh" api "$ENVIRONMENT"; then
        log_success "Smoke tests passed"
    else
        log_error "Smoke tests failed"
        kill $PORT_FORWARD_PID 2>/dev/null || true
        exit 1
    fi
    
    # Clean up port forwarding
    kill $PORT_FORWARD_PID 2>/dev/null || true
}

# Show deployment status
show_status() {
    log_info "Deployment status:"
    
    echo ""
    echo "Pods:"
    kubectl get pods -n "$NAMESPACE" -o wide
    
    echo ""
    echo "Services:"
    kubectl get services -n "$NAMESPACE"
    
    echo ""
    echo "Deployments:"
    kubectl get deployments -n "$NAMESPACE"
    
    echo ""
    echo "StatefulSets:"
    kubectl get statefulsets -n "$NAMESPACE"
    
    if [ "$ENVIRONMENT" != "development" ]; then
        echo ""
        echo "Ingresses:"
        kubectl get ingress -n "$NAMESPACE"
    fi
    
    echo ""
    echo "Recent Events:"
    kubectl get events -n "$NAMESPACE" --sort-by='.lastTimestamp' | tail -10
}

# Rollback deployment
rollback_deployment() {
    log_info "Rolling back deployment..."
    
    # Rollback API deployment
    kubectl rollout undo deployment/sportsbook-api -n "$NAMESPACE"
    kubectl rollout status deployment/sportsbook-api -n "$NAMESPACE" --timeout=${TIMEOUT}s
    
    # Rollback Orleans StatefulSet
    kubectl rollout undo statefulset/orleans-silo -n "$NAMESPACE"
    kubectl rollout status statefulset/orleans-silo -n "$NAMESPACE" --timeout=${TIMEOUT}s
    
    log_success "Rollback completed"
}

# Clean up deployment
cleanup_deployment() {
    log_info "Cleaning up deployment..."
    
    # Delete application resources
    kubectl delete -f "$PROJECT_ROOT/k8s/api-deployment.yaml" -n "$NAMESPACE" --ignore-not-found=true
    kubectl delete -f "$PROJECT_ROOT/k8s/orleans-cluster.yaml" -n "$NAMESPACE" --ignore-not-found=true
    
    # Delete infrastructure resources (with confirmation for production)
    if [ "$ENVIRONMENT" = "production" ]; then
        read -p "Are you sure you want to delete infrastructure in production? (yes/no): " confirm
        if [ "$confirm" != "yes" ]; then
            log_info "Infrastructure cleanup cancelled"
            return
        fi
    fi
    
    kubectl delete -f "$PROJECT_ROOT/k8s/pulsar-deployment.yaml" -n "$NAMESPACE" --ignore-not-found=true
    kubectl delete -f "$PROJECT_ROOT/k8s/redis-deployment.yaml" -n "$NAMESPACE" --ignore-not-found=true
    kubectl delete -f "$PROJECT_ROOT/k8s/postgres-deployment.yaml" -n "$NAMESPACE" --ignore-not-found=true
    
    # Delete configuration
    kubectl delete -f "$PROJECT_ROOT/k8s/configmaps.yaml" -n "$NAMESPACE" --ignore-not-found=true
    kubectl delete -f "$PROJECT_ROOT/k8s/secrets.yaml" -n "$NAMESPACE" --ignore-not-found=true
    
    # Delete ingress
    kubectl delete -f "$PROJECT_ROOT/k8s/ingress.yaml" -n "$NAMESPACE" --ignore-not-found=true
    
    log_success "Cleanup completed"
}

# Update deployment
update_deployment() {
    log_info "Updating deployment..."
    
    # Update API deployment
    kubectl set image deployment/sportsbook-api sportsbook-api="$IMAGE_TAG" -n "$NAMESPACE"
    kubectl rollout status deployment/sportsbook-api -n "$NAMESPACE" --timeout=${TIMEOUT}s
    
    # Update Orleans StatefulSet
    kubectl set image statefulset/orleans-silo orleans-silo="$ORLEANS_IMAGE_TAG" -n "$NAMESPACE"
    kubectl rollout status statefulset/orleans-silo -n "$NAMESPACE" --timeout=${TIMEOUT}s
    
    log_success "Update completed"
}

# Show usage information
show_usage() {
    cat << EOF
SportsbookLite Deployment Script

Usage: $0 [environment] [action]

Environments:
  development  Local development environment
  staging      Staging environment
  production   Production environment

Actions:
  deploy       Full deployment (default)
  update       Update existing deployment with new images
  rollback     Rollback to previous version
  status       Show deployment status
  cleanup      Remove all resources
  logs         Show application logs

Examples:
  $0 staging deploy      # Deploy to staging
  $0 production update   # Update production with new images
  $0 staging rollback    # Rollback staging deployment
  $0 production status   # Show production status

EOF
}

# Show logs
show_logs() {
    log_info "Showing logs for $ENVIRONMENT environment..."
    
    echo "=== Orleans Silo Logs ==="
    kubectl logs -l app.kubernetes.io/name=orleans-silo -n "$NAMESPACE" --tail=50
    
    echo ""
    echo "=== API Logs ==="
    kubectl logs -l app.kubernetes.io/name=sportsbook-api -n "$NAMESPACE" --tail=50
}

# Main execution
main() {
    case "$ACTION" in
        "deploy")
            check_prerequisites
            set_namespace
            create_namespace
            apply_configs
            deploy_infrastructure
            wait_for_infrastructure
            deploy_application
            apply_ingress
            apply_monitoring
            run_smoke_tests
            show_status
            log_success "Deployment completed successfully!"
            ;;
        "update")
            check_prerequisites
            set_namespace
            update_deployment
            run_smoke_tests
            show_status
            log_success "Update completed successfully!"
            ;;
        "rollback")
            check_prerequisites
            set_namespace
            rollback_deployment
            show_status
            log_success "Rollback completed successfully!"
            ;;
        "status")
            check_prerequisites
            set_namespace
            show_status
            ;;
        "cleanup")
            check_prerequisites
            set_namespace
            cleanup_deployment
            log_success "Cleanup completed successfully!"
            ;;
        "logs")
            check_prerequisites
            set_namespace
            show_logs
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

# Validate arguments
if [[ ! "$ENVIRONMENT" =~ ^(development|staging|production)$ ]]; then
    log_error "Invalid environment: $ENVIRONMENT"
    show_usage
    exit 1
fi

if [[ ! "$ACTION" =~ ^(deploy|update|rollback|status|cleanup|logs|help)$ ]]; then
    log_error "Invalid action: $ACTION"
    show_usage
    exit 1
fi

# Execute main function
log_info "SportsbookLite Deployment Script"
log_info "Environment: $ENVIRONMENT"
log_info "Action: $ACTION"
log_info "Timestamp: $(date)"

main