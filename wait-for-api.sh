#!/usr/bin/env bash
# wait-for-api.sh — waits until the API is healthy before printing the welcome message
# Usage: bash wait-for-api.sh

API_URL="http://localhost:8080/health"
MAX_WAIT=120
INTERVAL=5
elapsed=0

echo ""
echo "⏳  Waiting for BarberShop API to become healthy..."

while [ $elapsed -lt $MAX_WAIT ]; do
    status=$(curl -s -o /dev/null -w "%{http_code}" "$API_URL" 2>/dev/null)

    if [ "$status" = "200" ]; then
        echo ""
        echo "✅  API is healthy!"
        echo ""
        echo "══════════════════════════════════════════════════════════"
        echo "  🚀  BarberShop API is ready"
        echo "══════════════════════════════════════════════════════════"
        echo ""
        echo "  Swagger UI   →  http://localhost:8080/swagger"
        echo "  Grafana      →  http://localhost:3000"
        echo "  Prometheus   →  http://localhost:9090"
        echo ""
        echo "  Admin login:"
        echo "    Email    →  admin@barbershop.com"
        echo "    Password →  Admin@123"
        echo ""
        echo "══════════════════════════════════════════════════════════"
        echo ""
        exit 0
    fi

    echo "  Still waiting... (${elapsed}s elapsed, HTTP ${status})"
    sleep $INTERVAL
    elapsed=$((elapsed + INTERVAL))
done

echo ""
echo "❌  API did not become healthy within ${MAX_WAIT}s."
echo "   Run 'docker compose logs api' to investigate."
exit 1
