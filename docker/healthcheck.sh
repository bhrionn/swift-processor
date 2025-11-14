#!/bin/bash

# Health check script for SWIFT Message Processor services
# Can be used for monitoring and alerting

set -e

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

# Configuration
API_URL="${API_URL:-http://localhost:5000}"
FRONTEND_URL="${FRONTEND_URL:-http://localhost:3000}"
PROMETHEUS_URL="${PROMETHEUS_URL:-http://localhost:9090}"
GRAFANA_URL="${GRAFANA_URL:-http://localhost:3001}"

print_status() {
    local service=$1
    local status=$2
    local message=$3
    
    if [ "$status" = "healthy" ]; then
        echo -e "${GREEN}✓${NC} $service: $message"
    elif [ "$status" = "warning" ]; then
        echo -e "${YELLOW}⚠${NC} $service: $message"
    else
        echo -e "${RED}✗${NC} $service: $message"
    fi
}

check_api() {
    if curl -sf "$API_URL/health" > /dev/null 2>&1; then
        print_status "API" "healthy" "Running at $API_URL"
        return 0
    else
        print_status "API" "unhealthy" "Not responding at $API_URL"
        return 1
    fi
}

check_frontend() {
    if curl -sf "$FRONTEND_URL" > /dev/null 2>&1; then
        print_status "Frontend" "healthy" "Running at $FRONTEND_URL"
        return 0
    else
        print_status "Frontend" "unhealthy" "Not responding at $FRONTEND_URL"
        return 1
    fi
}

check_console() {
    if docker ps --filter "name=swift-console" --filter "status=running" | grep -q swift-console; then
        print_status "Console" "healthy" "Container running"
        return 0
    else
        print_status "Console" "unhealthy" "Container not running"
        return 1
    fi
}

check_prometheus() {
    if curl -sf "$PROMETHEUS_URL/-/healthy" > /dev/null 2>&1; then
        print_status "Prometheus" "healthy" "Running at $PROMETHEUS_URL"
        return 0
    else
        print_status "Prometheus" "warning" "Not running (optional)"
        return 0
    fi
}

check_grafana() {
    if curl -sf "$GRAFANA_URL/api/health" > /dev/null 2>&1; then
        print_status "Grafana" "healthy" "Running at $GRAFANA_URL"
        return 0
    else
        print_status "Grafana" "warning" "Not running (optional)"
        return 0
    fi
}

check_database() {
    if docker exec swift-console test -f /app/data/messages.db 2>/dev/null; then
        print_status "Database" "healthy" "SQLite database exists"
        return 0
    else
        print_status "Database" "warning" "Database file not found"
        return 1
    fi
}

check_communication() {
    if docker exec swift-console test -d /app/communication 2>/dev/null; then
        print_status "Communication" "healthy" "Communication directory exists"
        return 0
    else
        print_status "Communication" "unhealthy" "Communication directory not found"
        return 1
    fi
}

main() {
    echo "SWIFT Message Processor - Health Check"
    echo "========================================"
    echo ""
    
    local failed=0
    
    check_api || ((failed++))
    check_frontend || ((failed++))
    check_console || ((failed++))
    check_database || ((failed++))
    check_communication || ((failed++))
    check_prometheus
    check_grafana
    
    echo ""
    echo "========================================"
    
    if [ $failed -eq 0 ]; then
        echo -e "${GREEN}All critical services are healthy${NC}"
        exit 0
    else
        echo -e "${RED}$failed critical service(s) unhealthy${NC}"
        exit 1
    fi
}

main "$@"
