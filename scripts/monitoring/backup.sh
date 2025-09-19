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