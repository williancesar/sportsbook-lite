#!/bin/bash

# Generate test metrics for Sportsbook-Lite monitoring
# This script simulates realistic betting activity to populate monitoring dashboards

set -e

# Configuration
API_URL="${API_URL:-http://localhost:5000}"
ORLEANS_URL="${ORLEANS_URL:-http://localhost:9090}"
ITERATIONS="${ITERATIONS:-100}"
DELAY="${DELAY:-0.5}"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo -e "${GREEN}Starting Sportsbook-Lite Metrics Generation${NC}"
echo "API URL: $API_URL"
echo "Orleans URL: $ORLEANS_URL"
echo "Iterations: $ITERATIONS"
echo "Delay: ${DELAY}s between requests"
echo ""

# Function to check service availability
check_service() {
    local url=$1
    local name=$2
    
    if curl -s -f -o /dev/null "$url/health" 2>/dev/null || curl -s -f -o /dev/null "$url/metrics" 2>/dev/null; then
        echo -e "${GREEN}✓${NC} $name is available"
        return 0
    else
        echo -e "${RED}✗${NC} $name is not available at $url"
        return 1
    fi
}

# Check services
echo "Checking services..."
check_service "$API_URL" "API" || { echo -e "${RED}API is not running. Please start the API service.${NC}"; exit 1; }
check_service "$ORLEANS_URL" "Orleans Silo" || echo -e "${YELLOW}Warning: Orleans metrics endpoint not accessible${NC}"
echo ""

# Arrays for random data generation
SPORTS=("football" "basketball" "tennis" "baseball" "soccer")
EVENT_TYPES=("match" "tournament" "league" "championship")
MARKET_TYPES=("winner" "over_under" "handicap" "both_teams_score")
CURRENCIES=("USD" "EUR" "GBP")
STATUSES=("success" "failed" "pending")

# Function to generate random element from array
random_element() {
    local array=("$@")
    echo "${array[$RANDOM % ${#array[@]}]}"
}

# Function to generate random number in range
random_number() {
    local min=$1
    local max=$2
    echo $((RANDOM % (max - min + 1) + min))
}

# Function to simulate bet placement
place_bet() {
    local amount=$(random_number 10 500)
    local sport=$(random_element "${SPORTS[@]}")
    local event_type=$(random_element "${EVENT_TYPES[@]}")
    local market_type=$(random_element "${MARKET_TYPES[@]}")
    local currency=$(random_element "${CURRENCIES[@]}")
    local event_id="evt-$(random_number 1000 9999)"
    local user_id="usr-$(random_number 100 999)"
    
    # Simulate different success rates
    local success_rate=90
    local status="success"
    if [ $((RANDOM % 100)) -gt $success_rate ]; then
        status="failed"
    fi
    
    # Create JSON payload
    local payload=$(cat <<EOF
{
    "userId": "$user_id",
    "eventId": "$event_id",
    "amount": $amount,
    "currency": "$currency",
    "sport": "$sport",
    "eventType": "$event_type",
    "marketType": "$market_type",
    "odds": $(echo "scale=2; 1 + $RANDOM / 10000" | bc)
}
EOF
)
    
    # Send request (simulate with curl)
    if [ "$status" == "success" ]; then
        curl -s -X POST "$API_URL/api/v1/bets" \
            -H "Content-Type: application/json" \
            -d "$payload" \
            -o /dev/null 2>&1 || true
        echo -n "."
    else
        # Simulate failure by sending malformed request
        curl -s -X POST "$API_URL/api/v1/bets" \
            -H "Content-Type: application/json" \
            -d '{"invalid": "data"}' \
            -o /dev/null 2>&1 || true
        echo -n "x"
    fi
}

# Function to simulate odds updates
update_odds() {
    local market_id="mkt-$(random_number 100 999)"
    local event_id="evt-$(random_number 1000 9999)"
    local new_odds=$(echo "scale=2; 1 + $RANDOM / 10000" | bc)
    
    curl -s -X PUT "$API_URL/api/v1/odds/$market_id" \
        -H "Content-Type: application/json" \
        -d "{\"eventId\": \"$event_id\", \"odds\": $new_odds}" \
        -o /dev/null 2>&1 || true
}

# Function to simulate wallet operations
wallet_operation() {
    local user_id="usr-$(random_number 100 999)"
    local operation_type=$(random_element "deposit" "withdraw" "transfer")
    local amount=$(random_number 50 1000)
    local currency=$(random_element "${CURRENCIES[@]}")
    
    curl -s -X POST "$API_URL/api/v1/wallet/$operation_type" \
        -H "Content-Type: application/json" \
        -d "{\"userId\": \"$user_id\", \"amount\": $amount, \"currency\": \"$currency\"}" \
        -o /dev/null 2>&1 || true
}

# Function to simulate event operations
event_operation() {
    local event_id="evt-$(random_number 1000 9999)"
    local sport=$(random_element "${SPORTS[@]}")
    local status=$(random_element "scheduled" "live" "completed" "cancelled")
    
    curl -s -X PUT "$API_URL/api/v1/events/$event_id/status" \
        -H "Content-Type: application/json" \
        -d "{\"status\": \"$status\", \"sport\": \"$sport\"}" \
        -o /dev/null 2>&1 || true
}

# Function to check metrics endpoint
check_metrics() {
    echo ""
    echo -e "\n${YELLOW}Checking metrics generation...${NC}"
    
    # Check API metrics
    if curl -s "$API_URL/metrics" | grep -q "http_requests_total"; then
        echo -e "${GREEN}✓${NC} API metrics are being generated"
    else
        echo -e "${YELLOW}⚠${NC} API metrics might not be configured"
    fi
    
    # Check Orleans metrics
    if curl -s "$ORLEANS_URL/metrics" 2>/dev/null | grep -q "orleans_grain"; then
        echo -e "${GREEN}✓${NC} Orleans metrics are being generated"
    else
        echo -e "${YELLOW}⚠${NC} Orleans metrics might not be configured"
    fi
}

# Main execution
echo -e "${YELLOW}Generating test traffic...${NC}"
echo "Progress: "

for i in $(seq 1 $ITERATIONS); do
    # Mix of different operations
    operation=$((RANDOM % 10))
    
    case $operation in
        0|1|2|3|4|5)  # 60% bet placement
            place_bet
            ;;
        6|7)  # 20% odds updates
            update_odds
            echo -n "o"
            ;;
        8)  # 10% wallet operations
            wallet_operation
            echo -n "w"
            ;;
        9)  # 10% event operations
            event_operation
            echo -n "e"
            ;;
    esac
    
    # Progress indicator every 10 iterations
    if [ $((i % 10)) -eq 0 ]; then
        echo -n " [$i/$ITERATIONS]"
    fi
    
    # Random delay to simulate realistic traffic
    sleep_time=$(echo "scale=2; $DELAY * (0.5 + $RANDOM / 32768)" | bc)
    sleep "$sleep_time"
done

echo ""
echo -e "\n${GREEN}Test traffic generation completed!${NC}"

# Check metrics
check_metrics

# Display summary
echo -e "\n${GREEN}Summary:${NC}"
echo "- Generated approximately $ITERATIONS operations"
echo "- Traffic included: bets, odds updates, wallet operations, event updates"
echo ""
echo -e "${YELLOW}Next steps:${NC}"
echo "1. Open Grafana at http://localhost:3000"
echo "2. View the Orleans Overview dashboard"
echo "3. Check the Business Metrics dashboard"
echo "4. Review the API Performance dashboard"
echo ""
echo "To generate continuous traffic, run:"
echo "  while true; do $0; sleep 5; done"