# Sprint 1 — 设备管理 + 遥测摄取（第 1–5 周）

> **目标**：搭建可运行的设备管理 API + MQTT 遥测摄取服务，数据持久化到 PostgreSQL。

---

## 目录

1. [技术栈](#技术栈)
2. [整体架构](#整体架构)
3. [每周任务详情](#每周任务详情)
   - [第 1 周：项目脚手架 + 数据库](#第-1-周项目脚手架--数据库)
   - [第 2 周：设备 CRUD API + JWT Auth](#第-2-周设备-crud-api--jwt-auth)
   - [第 3 周：AWS IoT Core + MQTT 订阅](#第-3-周aws-iot-core--mqtt-订阅)
   - [第 4 周：Channel 批量写入 + 背压控制](#第-4-周channel-批量写入--背压控制)
   - [第 5 周：测试 + 健康检查 + 收尾](#第-5-周测试--健康检查--收尾)
4. [里程碑](#里程碑)
5. [交付物清单](#交付物清单)

---

## 技术栈

| 类别 | 技术 |
|------|------|
| 语言 / 框架 | C# 13 · .NET 9 · ASP.NET Core Web API |
| ORM | Entity Framework Core + PostgreSQL Provider |
| MQTT | MQTTnet |
| 云服务 | AWS IoT Core（MQTT over TLS，Thing 证书） |
| 认证 | JWT Bearer Token |
| 数据库 | PostgreSQL 16（Docker） |
| 测试 | xUnit · Moq |
| 文档 | Swagger / OpenAPI |
| 容器 | Docker Compose |

---

## 整体架构

```
设备（MQTT over TLS）
       │
       ▼
AWS IoT Core（Thing 注册 · 规则引擎）
       │
       ▼  MQTT Topic: devices/+/telemetry
MQTTnet Subscriber（BackgroundService）
       │
       ▼  Channel<T> 批量缓冲
PostgreSQL（遥测数据表）
       │
       ▼ REST API
设备管理 API（CRUD · JWT Auth）
```

---

## 每周任务详情

### 第 1 周：项目脚手架 + 数据库

**目标**：建立 Solution 结构，配置本地 PostgreSQL，完成 Device 实体和数据库迁移。

| 任务 | 具体步骤 | 产出 |
|------|----------|------|
| 创建 Solution | `dotnet new sln` → 添加 `src/MineWatch.Api`（WebAPI）· `src/MineWatch.Infrastructure`（类库）· `tests/MineWatch.UnitTests` | 完整 Solution 结构 |
| 配置 Docker Compose | 编写 `docker-compose.yml` 包含 `postgres:16` 和 `eclipse-mosquitto:2` | 本地 PostgreSQL + MQTT Broker 可用 |
| 配置 EF Core | 在 Infrastructure 项目添加 `Microsoft.EntityFrameworkCore` · `Npgsql.EntityFrameworkCore.PostgreSQL` | 迁移可执行 |
| 创建 Device 实体 | `Device.cs`（Id · Name · Type · Status · CreatedAt）+ `DeviceStatus` 枚举 | Entity 类 |
| 配置 DbContext | `MineWatchDbContext.cs` + Fluent API 配置 + 迁移 | `dotnet ef database update` 可建表 |
| 配置 Swagger | 启用 Swagger UI + OpenAPI 规范 | 访问 `/swagger` 可看到 API 文档 |
| 创建 TelemetryReading 实体 | `TelemetryReading.cs`（Id · DeviceId · Timestamp · Temperature · Pressure）| 遥测实体（为后续准备） |

---

### 第 2 周：设备 CRUD API + JWT Auth

**目标**：实现设备 CRUD REST API，带 JWT 认证和 Swagger 文档。

| 任务 | 具体步骤 | 产出 |
|------|----------|------|
| 添加认证包 | `Microsoft.AspNetCore.Authentication.JwtBearer` | JWT 中间件启用 |
| 配置 JWT | `appsettings.json` 添加 Jwt:Key · Jwt:Issuer · Jwt:Audience；配置 `AddAuthentication` | Token 可生成和验证 |
| 登录接口 | `POST /api/auth`（UserName + Password → JWT） | 返回有效 Token |
| 设备 CRUD | `GET/POST /api/devices` · `GET/PUT/DELETE /api/devices/{id}` | RESTful API |
| 设备服务 | `IDeviceService` + `DeviceService`（Scoped）| 业务逻辑层 |
| DTO | `CreateDeviceRequest` · `UpdateDeviceRequest` · `DeviceResponse` · `PageResponse<T>` | 请求/响应模型 |
| 分页查询 | `GET /api/devices?page=1&pageSize=20` | 列表分页 |
| 错误处理 | 全局异常中间件 · 统一响应格式 | `{ "success": false, "error": "..." }` |
| Swagger JWT | 配置 Swagger 显示 Bearer Token 输入框 | UI 可测试受保护接口 |
| 数据库种子 | `DbSeeder.cs` 插入 3 台示例卡车 | 开发环境有测试数据 |

**Device API 规范**：

```
POST   /api/auth            登录（公开）
GET    /api/devices          查询设备列表（需 Auth）
GET    /api/devices/{id}     查询单个设备（需 Auth）
POST   /api/devices          创建设备（需 Auth）
PUT    /api/devices/{id}     更新设备（需 Auth）
DELETE /api/devices/{id}     删除设备（需 Auth）
GET    /swagger              API 文档（公开）
```

---

### 第 3 周：AWS IoT Core + MQTT 订阅

**目标**：连接 AWS IoT Core，实现 MQTT 订阅后台服务，消息可接收并反序列化。

| 任务 | 具体步骤 | 产出 |
|------|----------|------|
| AWS 账号配置 | 确认 IoT Core 免费层资格；配置 AWS CLI（`aws configure`） | 可调用 AWS SDK |
| 创建 IoT Thing | AWS Console 创建 Thing；下载证书（`*.pem.crt` · `*.pem.key` · `AmazonRootCA1.pem`） | 证书文件本地保存 |
| 配置 MQTTnet | 在 Api 项目添加 `MQTTnet`；编写 MQTT 连接逻辑 | 设备可连接 IoT Core |
| MQTT 订阅服务 | `MqttSubscriberService : BackgroundService`（订阅 `devices/+/telemetry`） | 消息可接收 |
| 消息反序列化 | 将 JSON payload 映射到 `TelemetryReading` | 结构化数据 |
| EF Core 迁移 | 添加 TelemetryReadings 表（DbSet + 迁移） | 数据库表创建 |
| 测试连通 | 用 TruckMocker 或 MQTT.fx 发送测试消息 | 端到端验证 |

**踩坑提示**：
- AWS IoT Core 要求 MQTT over TLS 1.2，MQTTnet 需要正确配置 TLS 选项
- 证书路径避免中文和空格
- IoT Core 端点地址从 AWS Console 获取，格式如 `xxxxx-ats.iot.ap-southeast-2.amazonaws.com`

**MQTT Topic 规范**：

```
devices/{deviceId}/telemetry

Payload 示例：
{
  "timestamp": "2026-04-30T10:00:00Z",
  "temperature": 45.6,
  "pressure": 101.325
}
```

---

### 第 4 周：Channel 批量写入 + 背压控制

**目标**：用 `Channel<T>` 实现高效批量写入，加入背压控制，系统稳定可靠。

| 任务 | 具体步骤 | 产出 |
|------|----------|------|
| Channel 管道 | 创建 `Channel<TelemetryReading>`，MQTT 订阅写入 Channel，独立消费者读取并批量写库 | 高效写入管道 |
| 批量写入策略 | 每累积 100 条或每 1 秒触发一次 `SaveChangesAsync` | 减少数据库往返 |
| 背压控制 | `SemaphoreSlim` 限制并发写入数 | 系统过载时优雅降级 |
| 写入验证 | TruckMocker 发送 1000+ 条消息，确认全部写入 DB | 功能正确性验证 |
| 日志 | 关键节点日志：消息接收 · 批量写入 · 错误 | 可观测性基础 |

**Channel 管道架构**：

```
MQTT 消息到达
    │
    ▼
Channel<TelemetryReading>.Writer
    │
    ▼  BoundedChannel（容量限制 = 背压）
Channel.Reader（消费者循环）
    │
    ▼  累积到 100 条 或 超时 1 秒
批量 SaveChangesAsync → PostgreSQL
```

---

### 第 5 周：测试 + 健康检查 + 收尾

**目标**：单元测试、健康检查、README、GitHub 推送。

| 任务 | 具体步骤 | 产出 |
|------|----------|------|
| DeviceService 单元测试 | Moq 模拟 DbContext，测试 CRUD 方法 | 核心业务逻辑覆盖 |
| MqttSubscriberService 测试 | 测试消息反序列化 · Channel 写入逻辑 | 关键路径覆盖 |
| 健康检查 | `GET /health`（Liveness）· `GET /health/ready`（PostgreSQL + MQTT 连接状态） | K8s / ECS 就绪探针 |
| README 编写 | 项目介绍 · 架构图 · 环境搭建步骤 · API 文档链接 | 完整 README.md |
| Docker Compose 完善 | `docker compose up` 一键启动全部依赖 | 一键启动 |
| GitHub 推送 | 创建 GitHub 仓库 · Push | 仓库上线 |
| Sprint Review | 对照目标检查完成情况，记录踩坑笔记 | Sprint 1 完成 |

---

## 里程碑

| 里程碑 | 时间 | 验收标准 |
|--------|------|----------|
| M1：项目 + API 启动 | 第 2 周结束 | Solution 可编译运行；CRUD API 可用；JWT 生效；Swagger 可测试 |
| M2：MQTT 接入 | 第 3 周结束 | AWS IoT Core 连通；MQTT 消息可接收并反序列化 |
| M3：批量写入 | 第 4 周结束 | Channel 管道工作正常；1000 条消息全部写库 |
| M4：Sprint 1 交付 | 第 5 周结束 | 单元测试 > 70%；健康检查可用；README 完成；GitHub 已推送 |

---

## 交付物清单

### 代码仓库

- [x] `MineWatch.sln` — 完整 Solution
- [x] `src/MineWatch.Api` — WebAPI 项目（设备管理 API + MQTT 订阅）
- [x] `src/MineWatch.Infrastructure` — 基础设施项目（EF Core 仓储）
- [x] `tests/MineWatch.UnitTests` — 单元测试项目
- [x] `docker-compose.yml` — 本地开发环境
- [x] `TruckMocker` — MQTT 模拟器

### 功能验收

- [x] 设备 CRUD API（含 JWT 认证）可正常工作
- [ ] 通过 AWS IoT Core 接收 MQTT 消息并持久化到 PostgreSQL
- [ ] `Channel<T>` 实现批量写入
- [ ] Swagger 文档完整（包含认证说明）
- [x] 健康检查端点可用

### 测试

- [ ] 单元测试覆盖率 > 70%
- [ ] 所有核心业务逻辑有测试覆盖

### 文档

- [ ] `README.md`（项目介绍 + 快速开始 + API 文档）
- [ ] 代码内注释（关键类和方法）

---

## 附录：环境变量清单

```bash
# appsettings.json 或环境变量
ASPNETCORE_ENVIRONMENT=Development
ConnectionStrings__DefaultConnection=Host=localhost;Database=minewatch;Username=postgres;Password=postgres
Jwt__Key=your-256-bit-secret-key-here-minimum-32-chars
Jwt__Issuer=MineWatch
Jwt__Audience=MineWatchApi
Aws__Region=ap-southeast-2
Aws__IotCore__Endpoint=xxxxx-ats.iot.ap-southeast-2.amazonaws.com
Aws__IotCore__CertificatePath=/path/to/device.pem.crt
Aws__IotCore__PrivateKeyPath=/path/to/device.pem.key
Aws__IotCore__CaCertificatePath=/path/to/AmazonRootCA1.pem
```

---

*最后更新：2026 年 5 月*
