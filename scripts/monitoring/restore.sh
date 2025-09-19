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