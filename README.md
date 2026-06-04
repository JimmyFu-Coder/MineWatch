# MineWatch

Real-time monitoring platform for mining equipment. Collects GPS telemetry from fleet vehicles via MQTT, processes through an alert engine with configurable rules (speed, geo-fence, idle), and exposes data via REST API with SignalR real-time push.

## Architecture

```
  ┌──────────────┐  MQTT   ┌──────────────────────────────────────────────────┐
  │  TruckMocker  │────────>│  MineWatch.Worker                                 │
  │  (Simulator)  │         │                                                    │
  └──────────────┘         │  MqttSubscriberService ──> SQS Queue              │
                             │                                    │              │
                             │  SqsConsumerWorker <────────────────┘              │
                             │       │                                            │
                             │       ▼                                            │
                             │  TelemetryBatchWriter                              │
                             │   ├─ Auto-register unknown devices                │
                             │   ├─ Batch write to PostgreSQL                    │
                             │   ├─ AlertEngine.EvaluateAsync()                  │
                             │   └─ NotificationPublisher (SQS)                  │
                             │                                                    │
                             │  AlertEngine                                       │
                             │   ├─ SpeedRuleEvaluator                           │
                             │   ├─ GeoFenceRuleEvaluator                        │
                             │   └─ IdleRuleEvaluator                            │
                             └──────────────────────────────────────────────────┘
                                       │
  ┌────────────────────────────────────┼─────────────────────────────────────┐
  │  MineWatch.Api                      │                                     │
  │                                     ▼                                     │
  │  ┌─────────────┐    ┌──────────────────────┐    ┌─────────────────────┐  │
  │  │ REST API     │    │  PostgreSQL           │    │  SignalR Hub        │  │
  │  │ Controllers  │<──>│  - Devices            │    │  TelemetryHub       │  │
  │  │ Auth (JWT)   │    │  - TelemetryReadings  │    │  - TelemetryUpdate  │  │
  │  │ Swagger UI   │    │  - AlertRules         │    │  - AlertReceived    │  │
  │  │ Prometheus   │    │  - Alerts             │    └─────────────────────┘  │
  │  │ Health Checks│    │  - Users (Identity)   │                             │
  │  └─────────────┘    └──────────────────────┘                             │
  └──────────────────────────────────────────────────────────────────────────┘
```

**Data flow:** TruckMocker → MQTT → Worker (subscribe + SQS) → SQS → Worker (consume + batch write + alert evaluation) → PostgreSQL → API → Client

## Key Design Decisions

| Decision | Rationale |
|---|---|
| SQS between MQTT and DB | Decouples ingestion from persistence, enables retry/DLQ on failure, provides backpressure |
| Channel-based batch writer | `Channel<T>` with a 100-item/1-second threshold reduces DB round-trips from 1-per-message to ~1-per-batch |
| IDbContextFactory for Worker | Background services need independent DbContext instances; factory pattern avoids scoped-lifetime conflicts |
| Auto-migration on startup | Both API and Worker run `MigrateAsync()` on boot — zero manual migration steps |
| Auto-register devices | Unknown vehicles from telemetry are automatically registered, no manual device setup required |
| Strategy pattern for evaluators | `IRuleEvaluator` interface — add new rule types without modifying AlertEngine |
| Prometheus endpoint on API | `/metrics` exposes request rates, latencies, and custom counters for Grafana dashboards |

## Tech Stack

| Component | Technology |
|---|---|
| Language | C# 13 / .NET 9 |
| Web Framework | ASP.NET Core Web API |
| ORM | Entity Framework Core 9 with Npgsql |
| Database | PostgreSQL 16 |
| Messaging | MQTTnet 5.1 (MQTT) + AWS SQS SDK |
| Auth | ASP.NET Identity + JWT Bearer tokens with role claims |
| Real-time | SignalR WebSocket hub |
| Local AWS | LocalStack 3.8 |
| MQTT Broker | Eclipse Mosquitto 2 |
| Logging | Serilog (structured JSON) |
| Metrics | OpenTelemetry + Prometheus exporter |
| Testing | xUnit, Moq, EF Core InMemory (52 unit + 13 integration) |
| CI | GitHub Actions |
| Containerization | Docker Compose (6 services) |

## Project Structure

```
MineWatch/
├── src/
│   ├── MineWatch.Api/              # REST API, controllers, SignalR hub, middleware
│   ├── MineWatch.Worker/           # Background services (MQTT, SQS, batch writer, alert engine)
│   ├── MineWatch.Infrastructure/   # Entities, DbContext, migrations, seeding
│   └── MineWatch.Contracts/        # Shared DTOs (NotificationMessage)
├── tests/
│   ├── MineWatch.Api.Tests/        # Unit tests (52 tests)
│   └── MineWatch.IntegrationTests/ # Integration tests (13 tests)
├── TruckMocker/                    # GPS telemetry simulator
├── docs/                           # Design documents
├── infra/                          # Docker configs, Terraform (planned)
├── docker-compose.yml              # Full local dev environment
└── verify-pipeline.sh              # One-command pipeline verification
```

## Getting Started

### One-command startup

```bash
docker compose up -d
```

This starts 6 services: PostgreSQL, Mosquitto, LocalStack, API (auto-migration + seed data + admin user), Worker, and TruckMocker. The API is available at `http://localhost:5211/swagger`.

### Verify the pipeline

```bash
./verify-pipeline.sh | tee pipeline-verification.log
```

### Manual startup (for development)

Prerequisites: .NET 9 SDK, Docker

After cloning, set up the git hooks (auto-format on commit):

```bash
git config core.hooksPath .githooks
```

```bash
# 1. Start infrastructure only
docker compose up -d postgres mosquitto localstack

# 2. Run API (auto-migrates on startup)
dotnet run --project src/MineWatch.Api

# 3. Run Worker (MQTT subscriber + SQS consumer + alert engine)
dotnet run --project src/MineWatch.Worker

# 4. Start simulator
dotnet run --project TruckMocker
```

### Quick API test

```bash
# Login as seeded admin
TOKEN=$(curl -s -X POST http://localhost:5211/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"Admin@123"}' | python3 -c "import sys,json; print(json.load(sys.stdin)['token'])")

# List devices
curl http://localhost:5211/api/devices -H "Authorization: Bearer $TOKEN" | python3 -m json.tool

# Get latest telemetry
curl http://localhost:5211/api/telemetry/latest -H "Authorization: Bearer $TOKEN" | python3 -m json.tool

# Query alerts
curl http://localhost:5211/api/alerts -H "Authorization: Bearer $TOKEN" | python3 -m json.tool
```

### Run tests

```bash
dotnet test          # All 65 tests (52 unit + 13 integration)
```

## API Endpoints

### Authentication

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| POST | `/api/auth/register` | No | Register user with username, password, optional role |
| POST | `/api/auth/login` | No | Login, returns JWT with role claims |

### Devices

| Method | Endpoint | Auth | Roles | Description |
|---|---|---|---|---|
| GET | `/api/devices` | Yes | Any | List devices (paginated) |
| GET | `/api/devices/{id}` | Yes | Any | Get device by ID |
| POST | `/api/devices` | Yes | Admin | Create device |
| PUT | `/api/devices/{id}` | Yes | Admin, Operator | Update device |
| DELETE | `/api/devices/{id}` | Yes | Admin | Delete device |

### Telemetry

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| GET | `/api/telemetry/latest` | Yes | Latest position per vehicle (optional `vehicleNo` filter) |
| GET | `/api/telemetry/history` | Yes | Historical trajectory (paginated, requires `vehicleNo`) |

### Alerts

| Method | Endpoint | Auth | Roles | Description |
|---|---|---|---|---|
| GET | `/api/alerts/rules` | Yes | Any | List alert rules (paginated) |
| GET | `/api/alerts/rules/{id}` | Yes | Any | Get rule detail |
| POST | `/api/alerts/rules` | Yes | Admin | Create alert rule |
| PUT | `/api/alerts/rules/{id}` | Yes | Admin, Operator | Update rule |
| DELETE | `/api/alerts/rules/{id}` | Yes | Admin | Delete rule |
| GET | `/api/alerts` | Yes | Any | Query alerts (filter by status/device/rule, paginated) |
| PUT | `/api/alerts/{id}/acknowledge` | Yes | Admin, Operator | Acknowledge alert |
| PUT | `/api/alerts/{id}/resolve` | Yes | Admin, Operator | Resolve alert |

### Infrastructure

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| GET | `/health/ready` | No | Readiness probe (checks DB) |
| GET | `/metrics` | No | Prometheus metrics endpoint |

### SignalR

| Hub | Method | Description |
|---|---|---|
| `/hubs/telemetry` | `TelemetryUpdate` | Live vehicle position push |
| `/hubs/telemetry` | `AlertReceived` | Live alert notification push |

## Observability

| Endpoint | Purpose |
|---|---|
| `GET /metrics` | Prometheus-scrapable metrics (request rate, latency histograms) |
| `GET /health/ready` | Kubernetes-style readiness probe (verifies DB connectivity) |
| Console output | Structured JSON logs via Serilog (enriched with machine name, environment) |

## Configuration

Configuration is via `appsettings.json` and environment variables (overridden in docker-compose):

| Key | Description |
|---|---|
| `ConnectionStrings:DefaultConnection` | PostgreSQL connection string |
| `Jwt:Key` | JWT signing key (env var in production, User Secrets in development) |
| `Jwt:Issuer` / `Jwt:Audience` | JWT token issuer and audience |
| `AWS:ServiceURL` | SQS endpoint (LocalStack or real AWS) |
| `AWS:Region` | AWS region |
| `Sqs:QueueName` | SQS queue name |
| `Sqs:DlqName` | Dead-letter queue name |
| `Sqs:MaxReceiveCount` | DLQ threshold (default: 3) |
| `Mqtt:Server` / `Mqtt:Port` | MQTT broker address |

## Default Seed Data

On first startup, the system seeds:

- **Roles:** Admin, Operator, Viewer
- **Admin user:** username `admin`, password `Admin@123`
- **Devices:** Truck-001, Truck-002, Truck-003
- **Alert rules:**
  - Speed Limit — Trucks: 40 km/h threshold, 300s cooldown
  - Restricted Zone — Office Area: geo-fence circle (Perth CBD), no cooldown
  - Idle Timeout — Trucks: 5 min stationary, 600s cooldown

## Known Trade-offs

These are deliberate simplifications for the demo/portfolio context. A production deployment would address each one:

| Area | Current Implementation | Production Approach |
|---|---|---|
| **SQS message deletion** | `SqsConsumerWorker` deletes from SQS immediately after writing to the in-memory `Channel<T>`, before `TelemetryBatchWriter` persists to DB. If the worker crashes between channel write and DB write, that data is lost. | Delete-after-persist: acknowledge the message only after the batch writer confirms the DB write. Alternatives: outbox pattern, or use the batch writer callback to signal completion. |
| **Channel backpressure** | Bounded `Channel<T>(1000)` with `FullMode = DropOldest`. Suitable for live dashboards where stale positions are low-value, but inappropriate for audit/reporting pipelines that require every sample. | For audit-critical data, use `FullMode.Wait` and let SQS absorb backpressure, or scale out consumers. Consider separate pipelines for live vs. historical data. |
| **Demo credentials** | `docker-compose.yml` contains a demo JWT signing key and `DbSeeder` creates `admin / Admin@123`. These are for local development only. | Production uses AWS Secrets Manager or equivalent for JWT keys and connection strings. No seed credentials in production — admin accounts created via a secure bootstrapping flow. |
| **Device type cache** | `AlertEngine` caches rules (30s TTL) and device types (5min TTL) in-memory. Changes propagate on cache expiry, not immediately. | PostgreSQL NOTIFY/LISTEN or MQTT pub/sub for real-time cache invalidation. |

## License

Apache 2.0
