# wait-for-api.ps1 — waits until the API is healthy before printing the welcome message
# Usage: .\wait-for-api.ps1

$ApiUrl  = "http://localhost:8080/health"
$MaxWait = 120
$Interval = 5
$Elapsed  = 0

Write-Host ""
Write-Host "⏳  Waiting for BarberShop API to become healthy..." -ForegroundColor Cyan

while ($Elapsed -lt $MaxWait) {
    try {
        $response = Invoke-WebRequest -Uri $ApiUrl -UseBasicParsing -TimeoutSec 3 -ErrorAction SilentlyContinue
        if ($response.StatusCode -eq 200) {
            Write-Host ""
            Write-Host "✅  API is healthy!" -ForegroundColor Green
            Write-Host ""
            Write-Host "══════════════════════════════════════════════════════════" -ForegroundColor DarkCyan
            Write-Host "  🚀  BarberShop API is ready" -ForegroundColor White
            Write-Host "══════════════════════════════════════════════════════════" -ForegroundColor DarkCyan
            Write-Host ""
            Write-Host "  Swagger UI   →  http://localhost:8080/swagger" -ForegroundColor Yellow
            Write-Host "  Grafana      →  http://localhost:3000"          -ForegroundColor Yellow
            Write-Host "  Prometheus   →  http://localhost:9090"          -ForegroundColor Yellow
            Write-Host ""
            Write-Host "  Admin login:"
            Write-Host "    Email    →  admin@barbershop.com"
            Write-Host "    Password →  Admin@123"
            Write-Host ""
            Write-Host "══════════════════════════════════════════════════════════" -ForegroundColor DarkCyan
            Write-Host ""
            exit 0
        }
    } catch {
        # not ready yet
    }

    Write-Host "  Still waiting... ($($Elapsed)s elapsed)" -ForegroundColor DarkGray
    Start-Sleep -Seconds $Interval
    $Elapsed += $Interval
}

Write-Host ""
Write-Host "❌  API did not become healthy within ${MaxWait}s." -ForegroundColor Red
Write-Host "   Run 'docker compose logs api' to investigate." -ForegroundColor Red
exit 1
