# BarberShop API

![CI](https://github.com/EduardoHAnacleto/barbershop/actions/workflows/ci.yml/badge.svg)
![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)
![Docker](https://img.shields.io/badge/Docker-ready-2496ED?logo=docker)
![License](https://img.shields.io/badge/license-MIT-green)

> A production-ready REST API for barbershop appointment management, built as a portfolio project to demonstrate software architecture skills, engineering best practices, and a modern production-grade tech stack.

---

## Table of Contents

- [About](#about)
- [Architecture](#architecture)
- [Tech Stack & Practices](#tech-stack--practices)
- [Features](#features)
- [Project Structure](#project-structure)
- [Testing](#testing)
- [Observability](#observability)
- [CI/CD](#cicd)
- [Quick Start for Recruiters](#quick-start-for-recruiters)
- [API Reference](#api-reference)

---

## About

**BarberShop API** is a full-featured barbershop management system that handles appointments, customers, workers, services, users, and business hours — including exceptional closures like holidays.

The project was built to showcase **production-level engineering**: clean architecture, thorough unit test coverage, distributed caching, real-time communication, full observability with OpenTelemetry, and an automated CI pipeline.

---

## Architecture

The project follows **Clean Architecture** with strict layer separation and a clear dependency rule — outer layers depend on inner layers, never the other way around.

```
┌──────────────────────────────────────────────────────────┐
│                     BarberShop.API                       │
│         Controllers · Hubs · Extensions · Program        │
├──────────────────────────────────────────────────────────┤
│                  BarberShop.Application                  │
│      Services · Interfaces · DTOs · MappingProfile       │
├──────────────────────────────────────────────────────────┤
│                 BarberShop.Infrastructure                 │
│    Repositories · UnitOfWork · EF Core · Redis           │
├──────────────────────────────────────────────────────────┤
│                   BarberShop.Domain                       │
│            Entities · Enums · Value Objects              │
└──────────────────────────────────────────────────────────┘
```

### Dependency Rule

- **Domain** — zero external dependencies. Pure business entities and enums.
- **Application** — depends only on Domain. Defines interfaces (contracts), DTOs, and use cases.
- **Infrastructure** — implements Application interfaces. Contains EF Core, Redis, and repositories.
- **API** — depends on Application and Infrastructure. Orchestrates the HTTP pipeline, DI, and SignalR hubs.

### Design Patterns

| Pattern | Where |
|---|---|
| Repository Pattern | `IRepository<T>` + `GenericRepository<T>` |
| Unit of Work | `IUnitOfWork` / `UnitOfWork` — ensures transactional atomicity |
| Result Pattern | `Result<T>` — no exceptions for control flow |
| Base Service | `BaseService` — abstracts caching and SignalR notifications |
| CQRS-ready | Service layer separates reads from writes |

---

## Tech Stack & Practices

### Backend
| Technology | Version | Purpose |
|---|---|---|
| .NET / ASP.NET Core | 10.0 | Runtime and HTTP framework |
| Entity Framework Core | 10.0 | ORM with SQL Server and InMemory |
| AutoMapper | 16.x | Entity-to-DTO mapping |
| BCrypt.Net | 4.x | Secure password hashing |
| JWT Bearer | 10.0 | Stateless authentication |
| Google.Apis.Auth | 1.73 | Google OAuth login |
| SignalR | — | Real-time push notifications |

### Infrastructure
| Technology | Purpose |
|---|---|
| SQL Server 2022 | Primary relational database |
| Redis 7 | Distributed cache with prefix-based invalidation |
| Docker + Docker Compose | Full stack containerization |

### Observability
| Technology | Purpose |
|---|---|
| OpenTelemetry | Traces, metrics, and logs instrumentation |
| Prometheus | Metrics scraping and storage |
| Grafana Tempo | Distributed tracing |
| Grafana Loki | Log aggregation correlated to traces via TraceId |
| Grafana | Pre-provisioned dashboards (zero manual setup) |

### Testing & Quality
| Technology | Purpose |
|---|---|
| xUnit | Test framework |
| Moq | Dependency mocking |
| FluentAssertions | Expressive assertions |
| Coverlet | Code coverage collection |

### DevOps
| Technology | Purpose |
|---|---|
| GitHub Actions | CI pipeline: build, test, and coverage upload |
| Docker Hub | Pre-built image distribution |

---

## Features

- **Appointments** — full CRUD, batch cancellation, batch delay, filters by worker / customer / service / status / date range
- **Customers** — full CRUD
- **Workers** — full CRUD with linked services
- **Services** — full CRUD
- **Users** — full CRUD with role-based access control (Admin / User / Client)
- **Authentication** — email/password login (JWT) and Google OAuth login
- **Account lockout** — automatic lockout after 5 failed attempts, admin unlock endpoint
- **Business hours** — weekly schedule configuration (open time, close time, break interval) and exceptional closures (holidays, maintenance)
- **Is-open check** — endpoint that returns whether the barbershop is open at a given date and time
- **Real-time updates** — SignalR notifies connected clients on every mutation across all entities
- **Distributed cache** — Redis caches listings and individual records, automatically invalidated by prefix on every write

---

## Project Structure

```
BarberShop/
│
├── BarberShop.API/
│   ├── Controllers/          # AppointmentsController, WorkersController, ...
│   ├── Extensions/           # OpenTelemetryExtensions, SwaggerExtensions
│   ├── Hubs/                 # AppointmentsHub, WorkersHub, ...
│   └── Program.cs
│
├── BarberShop.Application/
│   ├── DTOs/                 # Request and Response DTOs
│   ├── Interfaces/           # IRepository, IUnitOfWork, IXxxService
│   ├── Services/             # AppointmentsService, AuthService, ...
│   └── Common/               # Result<T>
│
├── BarberShop.Infrastructure/
│   ├── Data/                 # AppDbContext + Fluent API mappings
│   ├── Repositories/         # GenericRepository + specific repositories
│   ├── Services/             # RedisService
│   └── UnitOfWork/           # UnitOfWork
│
├── BarberShop.Domain/
│   ├── Models/               # Appointment, Customer, Worker, Service, User, ...
│   └── Enums/                # Status, UserRoles, ClosureType
│
├── BarberShop.Tests/
│   └── Services/             # Unit tests per service
│
├── database/
│   └── schema.sql            # Full schema + seed data (schedule + admin user)
│
├── observability/
│   ├── grafana/              # Auto-provisioned dashboards and datasources
│   ├── prometheus/           # prometheus.yml
│   ├── loki/                 # loki.yml
│   └── tempo/                # tempo.yml
│
├── docker-compose.yml             # Full stack for local development
├── docker-compose.standalone.yml  # Single-file stack for recruiters (no build needed)
└── Dockerfile
```

---

## Testing

The project has approximately **60 unit tests** covering all services in the Application layer.

Each test class follows the **AAA pattern (Arrange / Act / Assert)** with:
- Repository mocks via `Moq`
- `IConnectionMultiplexer` mock for Redis (cache always empty in tests)
- `IHubContext<T>` mock for SignalR
- `NullLogger` to isolate tests from I/O
- Expressive assertions with `FluentAssertions`

```bash
# Run all tests locally
dotnet test BarberShop.slnx --configuration Release --verbosity normal
```

**Services covered:**
- `AppointmentsService` — CRUD, filters, batch delay, batch cancel, status transition rules
- `WorkersService` — CRUD, service linking, name and wage validations
- `CustomersService` — CRUD, validations
- `ServicesService` — CRUD, name / duration / price validations
- `UsersService` — CRUD, unique email validation, roles
- `WorkingHoursService` — schedule management, closures, `IsOpenAsync` with multiple edge case scenarios

---

## Observability

Every business operation emits:

- **Trace** — an OpenTelemetry span with relevant attributes (IDs, counts, status) visible in **Grafana Tempo**
- **Metrics** — custom counters and histograms (e.g. `barbershop.appointments.created`, `barbershop.auth.login_failed`) scraped by **Prometheus** and visualized in **Grafana**
- **Structured logs** — shipped via OTLP to **Loki**, correlated to traces by `TraceId`

Grafana starts **fully provisioned** with:
- Datasources configured (Prometheus, Loki, Tempo) with trace-to-log correlation
- A pre-loaded dashboard with panels for RPS, P95 latency, 5xx error rate, appointments created/cancelled, auth success vs failure, and .NET heap memory

---

## CI/CD

The CI pipeline runs automatically on GitHub Actions on every push to `main` or `develop`, and on every pull request targeting `main`:

```
Checkout → Setup .NET 10 → Restore → Build (Release) → Test + Coverage → Upload Artifact
```

The production image is published to Docker Hub:
```bash
docker pull eduardohanacleto/barbershop-api:1.0.0
```

---

## Quick Start for Recruiters

> You only need **Docker Desktop** installed. No other dependencies required.

### Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) installed and running
- Ports **8080**, **1433**, **6379**, **3000**, **3100**, **3200**, **4317**, **9090** available

---

### Step 1 — Download the compose file

Save the file below as `docker-compose.standalone.yml` in any folder on your machine:

**Linux / macOS:**
```bash
curl -O https://raw.githubusercontent.com/EduardoHAnacleto/barbershop/main/docker-compose.standalone.yml
```

**Windows (PowerShell):**
```powershell
Invoke-WebRequest -Uri "https://raw.githubusercontent.com/EduardoHAnacleto/barbershop/main/docker-compose.standalone.yml" -OutFile "docker-compose.standalone.yml"
```

---

### Step 2 — Start the stack

```bash
docker compose -f docker-compose.standalone.yml up -d
```

Docker will automatically pull all required images. The first run may take **3 to 5 minutes**.

---

### Step 3 — Wait for the API to be ready

The API depends on SQL Server initializing and the schema being created. Check the health endpoint:

**Linux / macOS:**
```bash
until curl -sf http://localhost:8080/health > /dev/null; do
  echo "Waiting for API..."; sleep 5
done && echo "API is ready!"
```

**Windows (PowerShell):**
```powershell
do {
    Start-Sleep 5
    Write-Host "Waiting for API..."
} until ((Invoke-WebRequest -Uri http://localhost:8080/health -UseBasicParsing -ErrorAction SilentlyContinue).StatusCode -eq 200)
Write-Host "API is ready!"
```

---

### Step 4 — Access the services

| Service | URL | Description |
|---|---|---|
| **Swagger UI** | http://localhost:8080/swagger | Interactive API documentation |
| **Grafana** | http://localhost:3000 | Metrics and tracing dashboards |
| **Prometheus** | http://localhost:9090 | Raw metrics |
| **Health Check** | http://localhost:8080/health/detail | All services status |

---

### Step 5 — Authenticate in Swagger

1. Go to http://localhost:8080/swagger
2. Expand `POST /api/auth/login`
3. Click **Try it out** and send:
```json
{
  "email": "admin@barbershop.com",
  "password": "Admin@123"
}
```
4. Copy the `token` value from the response
5. Click the **Authorize** button (top right corner)
6. Type `Bearer ` followed by the copied token and click **Authorize**

All protected endpoints are now unlocked.

---

### Step 6 — Explore Grafana

1. Go to http://localhost:3000 (no login required — anonymous access enabled)
2. Click **Dashboards** in the left menu
3. Open the **BarberShop** folder
4. Select the **BarberShop API** dashboard

Make a few API calls via Swagger to generate data in the charts.

---

### Stop the stack

```bash
# Stop containers but keep data volumes
docker compose -f docker-compose.standalone.yml down

# Stop containers and remove all data (database, cache, metrics)
docker compose -f docker-compose.standalone.yml down -v
```

---

## API Reference

### Authentication
| Method | Route | Auth | Description |
|---|---|---|---|
| POST | `/api/auth/login` | Public | Login with email and password |
| POST | `/api/auth/google` | Public | Login with Google OAuth |
| POST | `/api/auth/unlock/{userId}` | Admin | Unlock a locked account |

### Appointments
| Method | Route | Auth | Description |
|---|---|---|---|
| GET | `/api/appointments/all` | Public | List all appointments |
| GET | `/api/appointments/{id}` | Public | Get by ID |
| POST | `/api/appointments` | Public | Create appointment |
| PUT | `/api/appointments/{id}` | Public | Update appointment |
| DELETE | `/api/appointments/{id}` | Public | Cancel appointment |
| GET | `/api/appointments/range` | Public | Filter by date range |
| GET | `/api/appointments/worker/{id}` | Public | Filter by worker |
| GET | `/api/appointments/customer/{id}` | Public | Filter by customer |
| GET | `/api/appointments/service/{id}` | Public | Filter by service |
| GET | `/api/appointments/status/{status}` | Public | Filter by status |

### Workers
| Method | Route | Auth | Description |
|---|---|---|---|
| GET | `/api/workers/all` | Public | List all workers |
| GET | `/api/workers/{id}` | Public | Get by ID |
| POST | `/api/workers` | Public | Create worker |
| PUT | `/api/workers/{id}` | Public | Update worker |
| DELETE | `/api/workers/{id}` | Public | Remove worker |
| GET | `/api/workers/by-worker/{id}` | Public | Get services offered by a worker |
| GET | `/api/workers/by-service/{id}` | Public | Get workers that offer a service |

### Customers
| Method | Route | Auth | Description |
|---|---|---|---|
| GET | `/api/customers/all` | Public | List all customers |
| GET | `/api/customers/{id}` | Public | Get by ID |
| POST | `/api/customers` | Public | Create customer |
| PUT | `/api/customers/{id}` | Public | Update customer |
| DELETE | `/api/customers/{id}` | Public | Remove customer |

### Services
| Method | Route | Auth | Description |
|---|---|---|---|
| GET | `/api/services/all` | Public | List all services |
| GET | `/api/services/{id}` | Public | Get by ID |
| POST | `/api/services` | Public | Create service |
| PUT | `/api/services/{id}` | Public | Update service |
| DELETE | `/api/services/{id}` | Public | Remove service |

### Users
| Method | Route | Auth | Description |
|---|---|---|---|
| GET | `/users/all` | Public | List all users |
| GET | `/users/{id}` | Public | Get by ID |
| POST | `/users` | Admin | Create user |
| PUT | `/users/{id}` | Admin | Update user |
| DELETE | `/users/{id}` | Admin | Remove user |

### Business Hours
| Method | Route | Auth | Description |
|---|---|---|---|
| GET | `/api/working-hours/schedule` | Public | Full weekly schedule |
| GET | `/api/working-hours/schedule/{day}` | Public | Schedule for a specific day |
| PUT | `/api/working-hours/schedule/{id}` | Admin | Update a day's schedule |
| GET | `/api/working-hours/closures` | Public | List exceptional closures |
| POST | `/api/working-hours/closures` | Admin | Add a closure |
| DELETE | `/api/working-hours/closures/{id}` | Admin | Remove a closure |
| GET | `/api/working-hours/is-open?dateTime=` | Public | Check if open at a given time |

### SignalR Hubs
| Hub | Route | Event |
|---|---|---|
| AppointmentsHub | `/appointmentsHub` | `AppointmentsChanged` |
| WorkersHub | `/workersHub` | `WorkersChanged` |
| CustomersHub | `/customersHub` | `CustomersChanged` |
| ServicesHub | `/servicesHub` | `ServicesChanged` |
| UsersHub | `/usersHub` | `UsersChanged` |

---

## Appointment Status

| Value | Status | Description |
|---|---|---|
| 0 | `Scheduled` | Appointment is scheduled |
| 1 | `OnGoing` | Appointment is in progress |
| 2 | `Completed` | Appointment was completed |
| 3 | `Cancelled` | Appointment was cancelled |
| 4 | `Deleted` | Soft-deleted |

## User Roles

| Value | Role |
|---|---|
| 0 | Client |
| 1 | User |
| 3 | Admin |

---

*Built by **Eduardo H. Anacleto** as a technical portfolio project.*
