# MineWatch Product Roadmap

## Phase 1: Core Monitoring Platform (Completed)

Real-time fleet telemetry ingestion with device management.

### Milestone 1.1 — API Foundation
- [x] ASP.NET Core Web API project setup
- [x] JWT authentication
- [x] Device CRUD endpoints
- [x] Global exception handling middleware
- [x] Swagger / OpenAPI documentation

**Deliverables:** REST API with auth, device management, API docs

### Milestone 1.2 — Data Layer
- [x] PostgreSQL with EF Core
- [x] Device and TelemetryReading entities
- [x] Database migrations
- [x] IDbContextFactory pattern for background services

**Deliverables:** Persistent data layer, migration pipeline

### Milestone 1.3 — Telemetry Pipeline
- [x] MQTT subscriber (BackgroundService)
- [x] Telemetry message parsing
- [x] AWS SQS integration with DLQ
- [x] SQS consumer worker (BackgroundService)
- [x] Channel-based batch writer with retry logic

**Deliverables:** End-to-end telemetry flow: MQTT → SQS → batch → PostgreSQL

### Milestone 1.4 — Simulation & DevOps
- [x] TruckMocker GPS simulator
- [x] Docker Compose for local development
- [x] GitHub Actions CI pipeline
- [x] Unit tests for core services

**Deliverables:** Local development environment, CI, test suite

---

## Phase 2: Real-Time Dashboard & Alerting

Make telemetry data visible and actionable.

### Milestone 2.1 — Real-Time Data API
- [ ] Telemetry query endpoints (latest position, historical trail)
- [ ] SignalR WebSocket hub for live position updates
- [ ] Geo-fence zone management API

**Deliverables:** Queryable telemetry API, real-time push to clients

### Milestone 2.2 — Alert Engine

**Rule Types:**
- [ ] Speed Threshold — trigger when vehicle speed exceeds limit (e.g. 120 km/h / 33.3 m/s)
- [ ] Geo-Fence Breach — trigger when vehicle enters restricted zone (circle or polygon)
- [ ] Idle Timeout — trigger when vehicle is stationary beyond threshold (e.g. 5 minutes)

**Rule Management:**
- [ ] Global rules (apply to all devices) and device-specific rules
- [ ] Enable / disable rules without deletion
- [ ] Severity levels: Low / Medium / High / Critical
- [ ] Cooldown period per rule to prevent alert storms (same rule + device won't re-trigger within N seconds)

**Alert Lifecycle:**
- [ ] Active → Acknowledged → Resolved status flow
- [ ] Record trigger location (lat/lon), speed, timestamp, and triggering telemetry reading ID
- [ ] Acknowledge with operator identity tracking

**Data Model:**
- [ ] `AlertRule` entity — rule type, threshold, severity, device scope, cooldown, enabled flag
- [ ] `Alert` entity — linked to rule + device + telemetry reading, status, message, trigger metadata
- [ ] EF Core migration with indexes on `Alert.Status`, `Alert.DeviceId`, `Alert.TriggeredAt`, `AlertRule.IsEnabled`

**API Endpoints:**
- [ ] `POST   /api/alerts/rules` — create alert rule
- [ ] `GET    /api/alerts/rules` — list rules (paginated)
- [ ] `GET    /api/alerts/rules/{id}` — get rule detail
- [ ] `PUT    /api/alerts/rules/{id}` — update rule (threshold, severity, enabled)
- [ ] `DELETE /api/alerts/rules/{id}` — delete rule
- [ ] `GET    /api/alerts` — query alerts (filter by status / deviceId / ruleId, paginated)
- [ ] `GET    /api/alerts/{id}` — get alert detail
- [ ] `POST   /api/alerts/{id}/acknowledge` — acknowledge alert with operator name
- [ ] `POST   /api/alerts/{id}/resolve` — resolve alert

**Pipeline Integration:**
- [ ] `AlertEngine` service evaluates rules against each incoming `TelemetryReading` after DB write
- [ ] In-memory rule cache (30s TTL) to avoid querying rules on every reading
- [ ] In-memory idle state tracking (per-device last movement timestamp)
- [ ] Alert evaluation failure does not block telemetry ingestion

**Seed Data:**
- [ ] 3 default rules on first run: speed limit 120 km/h, restricted zone (circle near mine coordinates), idle 5 min

**Data Flow:**
```
TruckMocker → MQTT → SQS → SqsConsumerWorker
                                    │
                            (1) Write TelemetryReading to DB
                            (2) AlertEngine.EvaluateAsync(reading)
                                    │
                            Write Alert records to DB
```

**Deliverables:** Configurable alert rules, automatic evaluation on telemetry ingestion, queryable alert API, alert lifecycle management

### Milestone 2.3 — Notification Channels (Deferred)
- [ ] Email via Amazon SES
- [ ] SMS via Amazon SNS
- [ ] WebSocket push via SignalR

**Deliverables:** Multi-channel alert notifications

### Milestone 2.4 — Web Dashboard
- [ ] React / Blazor front-end
- [ ] Live fleet map with vehicle positions
- [ ] Alert list and acknowledgment UI
- [ ] Device management interface

**Deliverables:** Operational dashboard for fleet monitoring

### Milestone 2.5 — Integration Tests & Performance Verification

Prove the system works end-to-end with concrete numbers, not just unit tests.

**Alert Engine Integration Tests (WebApplicationFactory + InMemory DB):**
- [ ] Speed threshold: trigger when speed exceeds limit, no trigger when below
- [ ] Geo-fence breach (circle): trigger inside zone, no trigger outside
- [ ] Geo-fence breach (polygon): trigger inside polygon, no trigger outside
- [ ] Idle timeout: trigger after N seconds stationary, no trigger when moving
- [ ] Cooldown: duplicate alert not created within cooldown window
- [ ] Device-specific rule: only applies to target device
- [ ] Disabled rule: does not trigger
- [ ] Multiple rules: all matching rules create separate alerts

**AlertsController End-to-End Tests:**
- [ ] Rule CRUD: create → read → update → delete
- [ ] Alert query with filters: by status, by deviceId, by ruleId
- [ ] Alert acknowledge: status changes, `acknowledgedBy` and `acknowledgedAt` set
- [ ] Alert resolve: status changes, `resolvedAt` set
- [ ] Authorization required on all endpoints

**Performance Benchmarks (with concrete baseline assertions):**
- [ ] AlertEngine evaluation throughput: 10,000 readings × 100 rules, measure **readings/sec** — baseline > 100/sec
- [ ] Single evaluation latency: 1,000 iterations, measure **avg microseconds** — baseline < 5,000 μs
- [ ] Batch telemetry write throughput: 10,000 readings in batches of 100, measure **writes/sec** — baseline > 50/sec
- [ ] Alert query performance: 10,000 alerts in DB, paginated query, measure **query time ms** — baseline < 500 ms

**Test Infrastructure:**
- [ ] `MineWatch.IntegrationTests` project (xUnit + WebApplicationFactory + EF InMemory)
- [ ] `TestAuthHandler` to bypass JWT in integration tests
- [ ] `CustomWebApplicationFactory` that replaces DB + removes hosted services
- [ ] Update CI pipeline to run integration tests

**Deliverables:** Integration test suite with 15+ test cases, 4 performance benchmarks with published numbers, CI integration

---

## Phase 3: Analytics & Reporting

Business intelligence from fleet data.

### Milestone 3.1 — Data Aggregation
- [ ] Time-series aggregation service (hourly/daily summaries)
- [ ] Distance traveled and fuel consumption estimation
- [ ] Utilization reports (active vs idle time)

**Deliverables:** Aggregated metrics API

### Milestone 3.2 — Reporting
- [ ] Scheduled report generation (PDF/Excel)
- [ ] Report delivery via email
- [ ] Custom date range queries and exports

**Deliverables:** Automated reporting pipeline

### Milestone 3.3 — Advanced Analytics
- [ ] Heat map of vehicle activity
- [ ] Predictive maintenance indicators
- [ ] Trend analysis and anomaly detection

**Deliverables:** Analytics dashboard with insights

---

## Phase 4: Production Hardening & Scale

### Milestone 4.1 — Security
- [ ] ASP.NET Identity with role-based access control
- [ ] API key management for device authentication
- [ ] Audit logging
- [ ] Secrets management (AWS Secrets Manager)

**Deliverables:** Production-ready auth and security

### Milestone 4.2 — Reliability
- [ ] Health checks with comprehensive dependencies
- [ ] Structured logging (Serilog → CloudWatch)
- [ ] Metrics and monitoring (Prometheus / Grafana)
- [ ] Graceful shutdown and data durability guarantees

**Deliverables:** Production observability stack

### Milestone 4.3 — Infrastructure
- [ ] Terraform / CDK IaC for AWS deployment
- [ ] Container orchestration (ECS / EKS)
- [ ] Database backup and disaster recovery
- [ ] Auto-scaling and load testing

**Deliverables:** Cloud deployment pipeline, scalable infrastructure
