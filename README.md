# MineWatch

Real-time monitoring platform for mining equipment. Collects GPS telemetry from fleet vehicles via MQTT, processes messages through AWS SQS, and persists data to PostgreSQL with batch writing.

## Architecture

```
┌──────────────┐    MQTT     ┌──────────────────┐    SQS     ┌────────────────────┐    Batch    ┌──────────┐
│  TruckMocker  │───────────>│  MineWatch.Api    │──────────>│  SQS (LocalStack)   │───────────>│ PostgreSQL│
│  (Simulator)  │            │  MqttSubscriber   │           │  SqsConsumerWorker  │            │           │
└──────────────┘             └──────────────────┘           └────────────────────┘            └──────────┘
                                      │
                                      ▼
                             ┌──────────────────┐
                             │  REST API         │
                             │  /api/devices     │
                             │  /api/auth        │
                             │  /health          │
                             └──────────────────┘
```

**Data flow:** MQTT message → SQS queue → batch writer → PostgreSQL

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
| Testing | xUnit, Moq, EF Core InMemory |
| CI | GitHub Actions |

## Project Structure

```
MineWatch/
├── src/
│   ├── MineWatch.Api/              # Web API, controllers, services
│   └── MineWatch.Infrastructure/   # Entities, DbContext, migrations
├── tests/
│   ├── MineWatch.Api.Tests/        # Service & consumer tests
│   └── MineWatch.Infrastructure.Tests/
├── TruckMocker/                    # GPS telemetry simulator
└── docker-compose.yml              # PostgreSQL, Mosquitto, LocalStack
```

## Getting Started

### Prerequisites

- .NET 9 SDK
- Docker & Docker Compose

### Run Infrastructure

```bash
docker compose up -d
```

This starts PostgreSQL (5432), Mosquitto (1883), and LocalStack (4566).

### Run Database Migrations

```bash
cd src/MineWatch.Api
dotnet ef database update
```

### Run the API

```bash
cd src/MineWatch.Api
dotnet run
```

Swagger UI available at `http://localhost:5000/swagger`.

### Run the Simulator

```bash
cd TruckMocker
dotnet run
```

Publishes synthetic GPS telemetry to the MQTT broker.

### Run Tests

```bash
dotnet test
```

## API Endpoints

| Method | Endpoint | Description |
|---|---|---|
| POST | `/api/auth` | Authenticate and get JWT token |
| GET | `/api/devices` | List devices (paginated) |
| GET | `/api/devices/{id}` | Get device by ID |
| POST | `/api/devices` | Create device |
| PUT | `/api/devices/{id}` | Update device |
| DELETE | `/api/devices/{id}` | Delete device |
| GET | `/health` | Liveness probe |
| GET | `/health/ready` | Readiness probe (checks DB) |

## Configuration

Configuration is managed via `appsettings.json` and environment variables:

- **ConnectionStrings:DefaultConnection** — PostgreSQL connection string
- **Jwt:Key** — JWT signing key (use User Secrets in development)
- **Aws:ServiceUrl** — SQS endpoint (LocalStack or AWS)
- **Sqs:QueueName / DlqName** — SQS queue names

## License

Apache 2.0
