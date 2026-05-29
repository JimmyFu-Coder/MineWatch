# MineWatch

Real-time monitoring platform for mining equipment. Collects GPS telemetry from fleet vehicles via MQTT, processes messages through AWS SQS, and persists data to PostgreSQL with batch writing.

## Architecture

```
  ┌──────────────┐  MQTT   ┌───────────────────┐
  │  TruckMocker  │────────>│  MineWatch.Worker  │
  │  (Simulator)  │         │                    │
  └──────────────┘         │  MqttSubscriber ───┼──> SQS Queue ──> SqsConsumerWorker
                            │                    │                        │
                            └────────────────────┘                        │
                                                                          ▼
  ┌──────────────────┐                              ┌──────────────────────────────┐
  │  MineWatch.Api    │<─────────────────────────────│  TelemetryBatchWriter        │
  │                   │         PostgreSQL            │  (Channel<T>, batch of 100,  │
  │  REST API         │                              │   5s flush, retry on failure) │
  │  Swagger UI       │                              └──────────────────────────────┘
  │  Prometheus       │
  │  Health Checks    │
  └──────────────────┘
```

**Data flow:** TruckMocker → MQTT → Worker (subscribe + push to SQS) → SQS → Worker (consume + batch write) → PostgreSQL → API

## Key Design Decisions

| Decision | Rationale |
|---|---|
| SQS between MQTT and DB | Decouples ingestion from persistence, enables retry/DLQ on failure, provides backpressure |
| Channel-based batch writer | `Channel<T>` with a 100-item/5-second threshold reduces DB round-trips from 1-per-message to ~1-per-batch |
| IDbContextFactory for Worker | Background services need independent DbContext instances; factory pattern avoids scoped-lifetime conflicts |
| Auto-migration on startup | API runs `MigrateAsync()` on boot — zero manual migration steps in development |
| Prometheus endpoint on API | `/metrics` exposes request rates, latencies, and custom counters for Grafana dashboards |

## Tech Stack

| Component | Technology |
|---|---|
| Language | C# 13 / .NET 9 |
| Web Framework | ASP.NET Core Web API |
| ORM | Entity Framework Core 9 with Npgsql |
| Database | PostgreSQL 16 |
| Messaging | MQTTnet 5.1 (MQTT) + AWS SQS SDK |
| Auth | JWT Bearer tokens |
| Local AWS | LocalStack 3.8 |
| MQTT Broker | Eclipse Mosquitto 2 |
| Logging | Serilog (structured JSON) |
| Metrics | OpenTelemetry + Prometheus exporter |
| Testing | xUnit, Moq, EF Core InMemory |
| CI | GitHub Actions |
| IaC | Terraform (AWS ECS Fargate) |

## Project Structure

```
MineWatch/
├── src/
│   ├── MineWatch.Api/              # REST API, controllers, middleware
│   ├── MineWatch.Worker/           # Background services (MQTT subscriber, SQS consumer, batch writer)
│   └── MineWatch.Infrastructure/   # Entities, DbContext, migrations, seeding
├── tests/
│   └── MineWatch.Api.Tests/        # Unit tests (10 tests)
├── TruckMocker/                    # GPS telemetry simulator
├── infra/terraform/                # AWS infrastructure (ECS, RDS, SQS, ALB)
└── docker-compose.yml              # Full local dev environment
```

## Getting Started

### One-command startup

```bash
docker compose up -d
```

This starts the entire stack: PostgreSQL, Mosquitto, LocalStack, API (with auto-migration + seed data), and Worker. The API is available at `http://localhost:5211/swagger`.

### Manual startup (for development)

Prerequisites: .NET 9 SDK, Docker

```bash
# 1. Start infrastructure only
docker compose up -d postgres mosquitto localstack

# 2. Run API (auto-migrates on startup)
dotnet run --project src/MineWatch.Api

# 3. Run Worker (MQTT subscriber + SQS consumer)
dotnet run --project src/MineWatch.Worker

# 4. Start simulator
dotnet run --project TruckMocker
```

### Quick API test

```bash
# Authenticate
curl -X POST http://localhost:5211/api/auth \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"admin"}'

# Use the returned token to list devices
curl http://localhost:5211/api/devices \
  -H "Authorization: Bearer <token>"
```

### Run tests

```bash
dotnet test
```

## API Endpoints

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| POST | `/api/auth` | No | Authenticate, returns JWT |
| GET | `/api/devices` | Yes | List devices (paginated) |
| GET | `/api/devices/{id}` | Yes | Get device by ID |
| POST | `/api/devices` | Yes | Create device |
| PUT | `/api/devices/{id}` | Yes | Update device |
| DELETE | `/api/devices/{id}` | Yes | Delete device |
| GET | `/health/ready` | No | Readiness probe (checks DB) |
| GET | `/metrics` | No | Prometheus metrics endpoint |

## Observability

| Endpoint | Purpose |
|---|---|
| `GET /metrics` | Prometheus-scrapable metrics (request rate, latency histograms) |
| `GET /health/ready` | Kubernetes-style readiness probe (verifies DB connectivity) |
| Console output | Structured JSON logs via Serilog (enriched with machine name, environment) |

Connect Grafana to the `/metrics` endpoint to build dashboards for request rates, latency percentiles, and custom counters.

## Configuration

Configuration is managed via `appsettings.json` and environment variables (overridden in docker-compose):

| Key | Description |
|---|---|
| `ConnectionStrings:DefaultConnection` | PostgreSQL connection string |
| `Jwt:Key` | JWT signing key (use User Secrets in development) |
| `Jwt:TestUser / Jwt:Password` | Default credentials for development |
| `Aws:ServiceUrl` | SQS endpoint (LocalStack or real AWS) |
| `Sqs:QueueName` | SQS queue name |
| `Sqs:DlqName` | Dead-letter queue name |
| `Sqs:MaxReceiveCount` | DLQ threshold (default: 3) |
| `Mqtt:Server / Mqtt:Port` | MQTT broker address |

## License

Apache 2.0
