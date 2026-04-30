# Sprint 1 — 设备数据采集服务（第 1–7 周）

> **目标**：搭建可运行的设备管理 + 遥测摄取服务，通过 AWS IoT Core 接收 MQTT 数据并写库。

---

## 目录

1. [技术栈](#技术栈)
2. [整体架构](#整体架构)
3. [每周任务详情](#每周任务详情)
   - [第 1–2 周：项目脚手架 + AWS IoT Core 接入](#第-12-周项目脚手架--aws-iot-core-接入)
   - [第 3–4 周：设备管理 API](#第-34-周设备管理-api)
   - [第 5–6 周：遥测数据摄取](#第-56-周遥测数据摄取)
   - [第 7 周：收尾](#第-7-周收尾)
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
MQTTnet Subscriber（IHostedService）
       │
       ▼
PostgreSQL（遥测数据表）
       │
       ▼ REST API
设备管理 API（CRUD · JWT Auth）
```

---

## 每周任务详情

### 第 1–2 周：项目脚手架 + AWS IoT Core 接入

**目标**：建立 Solution 结构，配置本地 PostgreSQL 和 Redis，连接 AWS IoT Core。

#### 第 1 周：项目初始化

| 任务 | 具体步骤 | 产出 |
|------|----------|------|
| 创建 Solution | `dotnet new sln` → 添加 `src/MineWatch.Api`（WebAPI）· `src/MineWatch.Infrastructure`（类库）· `tests/MineWatch.UnitTests` | 完整 Solution 结构 |
| 配置 Docker Compose | 编写 `docker-compose.yml` 包含 `postgres:16` | 本地 PostgreSQL 可用 |
| 配置 EF Core | 在 Infrastructure 项目添加 `Microsoft.EntityFrameworkCore` · `Npgsql.EntityFrameworkCore.PostgreSQL` | 迁移可执行 |
| 配置 Swagger | 启用 Swagger UI + OpenAPI 规范 | 访问 `/swagger` 可看到 API 文档 |
| 创建 Device 实体 | `Device.cs`（Id · Name · Type · Status · CreatedAt） | Entity 类 |

#### 第 2 周：AWS IoT Core 接入

| 任务 | 具体步骤 | 产出 |
|------|----------|------|
| AWS 账号配置 | 确认 IoT Core 免费层资格；配置 AWS CLI（`aws configure`） | 可调用 AWS SDK |
| 创建 IoT Thing | AWS Console 创建 Thing；下载证书（`*.pem.crt` · `*.pem.key` · `AmazonRootCA1.pem`） | 证书文件本地保存 |
| 配置 MQTTnet | 在 Api 项目添加 `MQTTnet`；编写 `MqttClientService.cs` 连接 IoT Core | 设备可连接并发布消息 |
| 创建测试设备 | 使用 MQTT.fx 或 AWS Console 测试设备连接 | 确认 MQTT 消息可收发 |
| 遥测数据模型 | `TelemetryReading.cs`（DeviceId · Timestamp · Temperature · Pressure） | 遥测实体 |

**踩坑提示**：
- AWS IoT Core 要求 MQTT over TLS 1.2，MQTTnet 需要正确配置 TLS 选项
- 证书路径避免中文和空格
- IoT Core 端点地址从 AWS Console 获取，格式如 `xxxxx-ats.iot.ap-southeast-2.amazonaws.com`

---

### 第 3–4 周：设备管理 API

**目标**：实现设备 CRUD REST API，带 JWT 认证和 Swagger 文档。

#### 第 3 周：CRUD + JWT

| 任务 | 具体步骤 | 产出 |
|------|----------|------|
| 添加认证包 | `Microsoft.AspNetCore.Authentication.JwtBearer` | JWT 中间件启用 |
| 配置 JWT | `appsettings.json` 添加 Jwt:Key · Jwt:Issuer · Jwt:Audience；配置 `AddAuthentication` | Token 可生成和验证 |
| 登录接口 | `POST /api/auth/login`（UserName + Password → JWT） | 返回有效 Token |
| 设备 CRUD | `GET/POST /api/devices` · `GET/PUT/DELETE /api/devices/{id}` | RESTful API |
| 设备仓储 | `IDeviceRepository` + `EfCoreDeviceRepository` | 骨架已建，逻辑在拆分服务时实现 |
| EF Core 迁移 | `dotnet ef migrations add InitialCreate` · `database-update` | 数据库表创建 |

#### 第 4 周：完善 + 文档

| 任务 | 具体步骤 | 产出 |
|------|----------|------|
| 设备状态枚举 | `DeviceStatus`（Online · Offline · Maintenance） | 类型安全 |
| 分页查询 | `GET /api/devices?page=1&pageSize=20` | 列表分页 |
| 错误处理 | 全局异常中间件 · 统一响应格式 | `{ "success": false, "error": "..." }` |
| Swagger JWT | 配置 Swagger 显示 Bearer Token 输入框 | UI 可测试受保护接口 |
| 单元测试 | 测试 DeviceService 的 CRUD 方法 | 覆盖率 > 70% |

**Device API 规范**：

```
POST   /api/devices          创建设备（需 Auth）
GET    /api/devices          查询设备列表（需 Auth）
GET    /api/devices/{id}     查询单个设备（需 Auth）
PUT    /api/devices/{id}     更新设备（需 Auth）
DELETE /api/devices/{id}     删除设备（需 Auth）
POST   /api/auth/login       登录（公开）
GET    /swagger              API 文档（公开）
```

---

### 第 5–6 周：遥测数据摄取

**目标**：实现 MQTT 订阅后台服务，将遥测数据持久化到 PostgreSQL。

#### 第 5 周：MQTT 订阅 + 后台服务

| 任务 | 具体步骤 | 产出 |
|------|----------|------|
| MQTT 服务实现 | `MqttSubscriberService : BackgroundService`（订阅 `devices/+/telemetry`） | 消息可接收 |
| 消息反序列化 | 将 JSON  payload 映射到 `TelemetryReading` | 结构化数据 |
| 遥测仓储 | `ITelemetryRepository` + `EfCoreTelemetryRepository` | 数据访问抽象 |
| 批量写入 | 使用 `Channel<T>` 缓冲 + 批量 Insert（每 100 条或每 1 秒） | 高效写入 |
| EF Core 迁移 | 添加 TelemetryReadings 表 | 数据库表创建 |

#### 第 6 周：性能 + 测试

| 任务 | 具体步骤 | 产出 |
|------|----------|------|
| 背压控制 | `SemaphoreSlim` 限制并发写入数 | 系统稳定 |
| 健康检查 | `GET /health`（PostgreSQL 连接 + MQTT 连接状态） | 健康检查端点 |
| 集成测试 | Test `MqttSubscriberService` 消息处理逻辑 | 核心逻辑覆盖 |
| 性能验证 | 手动压测：MQTT.fx 发 1000 条消息，确认全部写入 DB | 功能验证 |

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

### 第 7 周：收尾

**目标**：测试、文档、GitHub 推送。

| 任务 | 具体步骤 | 产出 |
|------|----------|------|
| 单元测试补充 | 覆盖 DeviceService · TelemetryService | 覆盖率 > 70% |
| README 编写 | 项目介绍 · 架构图 · 环境搭建步骤 · API 文档链接 | 完整 README.md |
| Docker Compose 完善 | 添加 `docker-compose up` 启动全部依赖（PostgreSQL） | 一键启动 |
| GitHub 推送 | `git init` · 创建 GitHub 仓库 · Push | 仓库上线 |
| Sprint Review | 对照原始目标检查完成情况 | Sprint 1 完成 |

---

## 里程碑

| 里程碑 | 时间 | 验收标准 |
|--------|------|----------|
| M1：项目启动 | 第 2 周结束 | Solution 可编译运行；Docker Compose 启动 PostgreSQL |
| M2：设备 API 完成 | 第 4 周结束 | CRUD API 可用；JWT 认证生效；Swagger 可测试 |
| M3：遥测摄取完成 | 第 6 周结束 | MQTT 消息可写库；批量写入正常 |
| M4：Sprint 1 交付 | 第 7 周结束 | 单元测试 > 70%；README 完成；GitHub 已推送 |

---

## 交付物清单

### 代码仓库

- [ ] `MineWatch.sln` — 完整 Solution
- [ ] `src/MineWatch.Api` — WebAPI 项目（设备管理 API + MQTT 订阅）
- [ ] `src/MineWatch.Infrastructure` — 基础设施项目（EF Core 仓储）
- [ ] `tests/MineWatch.UnitTests` — 单元测试项目
- [ ] `docker-compose.yml` — 本地开发环境

### 功能验收

- [ ] 设备 CRUD API（含 JWT 认证）可正常工作
- [ ] 通过 AWS IoT Core 接收 MQTT 消息并持久化到 PostgreSQL
- [ ] `Channel<T>` 实现批量写入
- [ ] Swagger 文档完整（包含认证说明）

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
Aws__IotCore__Endpoint=https://xxxxx-ats.iot.ap-southeast-2.amazonaws.com
Aws__IotCore__CertificatePath=/path/to/device.pem.crt
Aws__IotCore__PrivateKeyPath=/path/to/device.pem.key
Aws__IotCore__CaCertificatePath=/path/to/AmazonRootCA1.pem
```

---

*最后更新：2026 年 4 月*