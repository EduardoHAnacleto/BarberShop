# BarberShop API

![CI](https://github.com/YOUR_USERNAME/barbershop/actions/workflows/ci.yml/badge.svg)
![.NET](https://img.shields.io/badge/.NET-10.0-purple)
![Docker](https://img.shields.io/badge/Docker-ready-blue)

A production-ready barbershop appointment management REST API built with **ASP.NET Core 10**, following Clean Architecture principles.

---

## 🚀 Quick Start (Docker)

> **Requires:** [Docker Desktop](https://www.docker.com/products/docker-desktop/) — nothing else.

```bash
# 1. Clone
git clone https://github.com/YOUR_USERNAME/barbershop.git
cd barbershop

# 2. Copy environment file
cp .env.example .env          # Linux / macOS
# copy .env.example .env      # Windows PowerShell

# 3. Start everything
docker compose up -d --build

# 4. Wait for the API to be ready (~3-5 min on first run)
bash wait-for-api.sh          # Linux / macOS
# .\wait-for-api.ps1          # Windows PowerShell
```

### Access Points

| Service      | URL                              |
|-------------|----------------------------------|
| Swagger UI   | http://localhost:8080/swagger   |
| Grafana      | http://localhost:3000           |
| Prometheus   | http://localhost:9090           |

### Admin Credentials
```
Email:    admin@barbershop.com
Password: Admin@123
```

---

## 🏗️ Architecture

```
BarberShop/
├── BarberShop.Domain/          # Entities, enums — zero dependencies
├── BarberShop.Application/     # Use cases, interfaces, DTOs, services
├── BarberShop.Infrastructure/  # EF Core, Redis, repositories, UoW
├── BarberShop.API/             # Controllers, hubs, middleware, Program.cs
└── BarberShop.Tests/           # xUnit unit tests (~60 tests)
```

**Patterns & practices:**
- Clean Architecture (Domain → Application → Infrastructure → API)
- Repository + Unit of Work
- Result pattern (no exception-driven flow control)
- CQRS-ready service layer

---

## 🛠️ Tech Stack

| Layer           | Technology                                    |
|----------------|-----------------------------------------------|
| Runtime         | .NET 10 / ASP.NET Core                       |
| Database        | SQL Server 2022                              |
| Cache           | Redis 7                                      |
| Real-time       | SignalR                                      |
| Auth            | JWT Bearer + Google OAuth                    |
| ORM             | Entity Framework Core 10                     |
| Mapping         | AutoMapper 16                                |
| Observability   | OpenTelemetry → Prometheus + Loki + Tempo → Grafana |
| Testing         | xUnit · Moq · FluentAssertions · Coverlet    |
| CI              | GitHub Actions                               |
| Container       | Docker + Docker Compose                      |

---

## 📡 API Endpoints

### Auth
| Method | Route                      | Auth     |
|--------|---------------------------|----------|
| POST   | /api/auth/login           | Public   |
| POST   | /api/auth/google          | Public   |
| POST   | /api/auth/unlock/{userId} | Admin    |

### Appointments
| Method | Route                              | Auth   |
|--------|-----------------------------------|--------|
| GET    | /api/appointments/all             | Public |
| GET    | /api/appointments/{id}            | Public |
| POST   | /api/appointments                 | Public |
| PUT    | /api/appointments/{id}            | Public |
| DELETE | /api/appointments/{id}            | Public |
| GET    | /api/appointments/range           | Public |
| GET    | /api/appointments/worker/{id}     | Public |
| GET    | /api/appointments/customer/{id}   | Public |
| GET    | /api/appointments/status/{status} | Public |

### Workers / Customers / Services / Users
Standard CRUD on `/api/workers`, `/api/customers`, `/api/services`, `/users`.

### Working Hours
| Method | Route                           | Auth   |
|--------|---------------------------------|--------|
| GET    | /api/working-hours/schedule     | Public |
| PUT    | /api/working-hours/schedule/{id}| Admin  |
| GET    | /api/working-hours/closures     | Public |
| POST   | /api/working-hours/closures     | Admin  |
| DELETE | /api/working-hours/closures/{id}| Admin  |
| GET    | /api/working-hours/is-open      | Public |

---

## 🧪 Running Tests

```bash
dotnet test BarberShop.slnx --configuration Release --verbosity normal
```

---

## 📊 Observability

The stack ships with a full observability pipeline out of the box:

- **Traces** — every service operation emits OpenTelemetry spans visible in **Grafana Tempo**
- **Metrics** — custom counters/histograms (appointments created, auth failures, operation durations) scraped by **Prometheus**
- **Logs** — structured logs shipped to **Loki**, correlated with traces via `TraceId`
- **Dashboards** — pre-provisioned **Grafana** dashboard at `http://localhost:3000`

---

## 🛑 Stopping

```bash
docker compose down        # stop containers, keep volumes
docker compose down -v     # stop containers + delete database data
```
