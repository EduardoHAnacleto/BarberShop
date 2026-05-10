# BarberShop API — Recruiter Quick Start

## Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) installed and running
- Ports **8080**, **1433**, **6379**, **3000**, **9090** available

---

## One-Command Setup

### Windows (PowerShell)
```powershell
git clone https://github.com/YOUR_USERNAME/barbershop.git
cd barbershop
copy .env.example .env
docker compose up -d --build
```

### Linux / macOS
```bash
git clone https://github.com/YOUR_USERNAME/barbershop.git
cd barbershop
cp .env.example .env
docker compose up -d --build
```

> First run takes ~3–5 minutes (SQL Server initialization).

---

## What's Running

| Service        | URL                                        | Description                  |
|---------------|--------------------------------------------|------------------------------|
| **API**        | http://localhost:8080/swagger              | REST API + Swagger UI        |
| **Grafana**    | http://localhost:3000                      | Dashboards (no login needed) |
| **Prometheus** | http://localhost:9090                      | Metrics scraping             |
| **Tempo**      | http://localhost:3200                      | Distributed tracing          |

---

## Admin Credentials

```
Email:    admin@barbershop.com
Password: Admin@123
```

Use `POST /api/auth/login` to get a JWT token, then click **Authorize** in Swagger.

---

## Stopping Everything

```bash
docker compose down
# To also remove volumes (database data):
docker compose down -v
```

---

## Architecture Highlights

- **Clean Architecture** — Domain / Application / Infrastructure / API layers
- **CQRS-ready** — Repository + Unit of Work pattern
- **Real-time** — SignalR hubs for live updates
- **Caching** — Redis with prefix-based invalidation
- **Observability** — OpenTelemetry → Prometheus + Loki + Tempo → Grafana
- **Testing** — xUnit + Moq + FluentAssertions (~60 unit tests)
- **CI** — GitHub Actions on push to `main` / `develop`
