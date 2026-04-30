# C# 后端开发学习计划：IoT 实时监控平台

> **目标**：通过构建一个矿山设备实时监控平台，系统掌握 C# 高并发、微服务、DDD 和 AWS 部署，最终用于 C# 后端开发岗位求职。
>
> **周期**：约 30 周 | **每周投入**：10–15 小时 | **方向**：IoT / 实时监控

---

## 目录

1. [项目概述](#项目概述)
2. [技术栈总览](#技术栈总览)
3. [本地开发环境策略](#本地开发环境策略)
4. [Sprint 1 — 设备数据采集服务](#sprint-1--设备数据采集服务第-17-周)
5. [Sprint 2 — 消息队列 + 实时告警](#sprint-2--消息队列--实时告警第-816-周)
6. [Sprint 3 — DDD 重构 + 微服务拆分](#sprint-3--ddd-重构--微服务拆分第-1724-周)
7. [Sprint 4 — AWS 生产化部署](#sprint-4--aws-生产化部署第-2530-周)
8. [AWS 费用规划](#aws-费用规划)
9. [面试资产清单](#面试资产清单)

---

## 项目概述

**项目名称**：MineWatch — 矿山设备实时监控平台

**背景**：贴近 Raptor OS 的实际工作场景，三个独立微服务构成完整系统：

| 微服务 | 职责 |
|--------|------|
| 设备管理服务 | 设备注册、证书管理、元数据 CRUD |
| 遥测摄取服务 | 接收 MQTT 数据流，高并发写入 |
| 告警通知服务 | 阈值检测、规则引擎、多渠道通知 |

**最终可展示的面试话术**：

> "我在 AWS 上独立设计并交付了一个生产级 IoT 监控平台，涵盖 AWS IoT Core / SQS / SNS / Amazon MQ / ECS Fargate / RDS，全套 Terraform IaC 管理，GitHub Actions CI/CD 自动化部署。"

---

## 技术栈总览

### 后端核心

- **语言 / 框架**：C# 12 · .NET 8 · ASP.NET Core Web API
- **ORM**：Entity Framework Core + PostgreSQL Provider
- **并发**：`Channel<T>` · TPL · `IAsyncEnumerable` · `SemaphoreSlim`
- **架构**：Clean Architecture · DDD · CQRS + MediatR
- **测试**：xUnit · Moq · TestContainers · WebApplicationFactory

### 消息队列（分层使用）

| 服务 | 用途 | 本地替代 |
|------|------|----------|
| Amazon SQS | 遥测数据异步缓冲，IoT Core 规则引擎推送 | LocalStack |
| Amazon SNS | 告警一对多广播（邮件 / SQS / Lambda） | LocalStack |
| Amazon MQ (RabbitMQ) | 微服务间事件总线，AMQP 语义 | Docker RabbitMQ |
| MassTransit | .NET 消息框架，屏蔽底层 broker 差异 | 同上 |

### AWS 基础设施

- **IoT 接入**：AWS IoT Core（MQTT over TLS，Thing 证书）
- **容器**：ECS Fargate + ECR
- **数据库**：RDS PostgreSQL（生产）/ Docker PostgreSQL（本地）
- **缓存**：ElastiCache Redis（生产）/ Docker Redis（本地）
- **监控**：CloudWatch + X-Ray
- **网关**：Amazon API Gateway
- **IaC**：Terraform

### CI/CD

- GitHub Actions → ECR Push → ECS Rolling Deploy
- OIDC 无密钥认证，蓝绿部署，PR 触发自动部署到 Staging

---

## 本地开发环境策略

**核心原则**：Sprint 1–3 全程本地开发，零云费用；Sprint 4 一次性上 AWS。

### 本地 ↔ AWS 对照表

| 组件 | 本地（Docker） | 生产（AWS） | 切换成本 |
|------|---------------|-------------|----------|
| PostgreSQL | `postgres:16` | RDS PostgreSQL | 连接字符串替换 |
| Redis | `redis:7` | ElastiCache | 连接字符串替换 |
| RabbitMQ | `rabbitmq:3-management` | Amazon MQ | MassTransit transport 配置 |
| SQS / SNS | LocalStack | Amazon SQS / SNS | endpoint URL 替换 |
| MQTT Broker | EMQX Docker | AWS IoT Core | 证书逻辑需适配（建议早期接入） |

### Docker Compose 启动命令

```bash
# 启动全部本地依赖
docker compose up -d

# 服务包含：PostgreSQL · Redis · RabbitMQ · EMQX · LocalStack
```

### AWS IoT Core 说明

> **建议 Sprint 1 即接入真实 IoT Core**，原因：
> - IoT Core 免费层：每月 250,000 条消息，**12 个月有效**（从账号创建日起算）
> - 设备证书认证逻辑与本地 EMQX 有差异，早接入早踩坑
> - 2025 年 7 月 15 日后新建账号可获最高 $200 免费 credits

---

## Sprint 1 — 设备数据采集服务（第 1–7 周）

**目标**：搭建可运行的设备管理 + 遥测摄取服务，通过 AWS IoT Core 接收 MQTT 数据并写库。

### 学习内容（刚好够用）

- ASP.NET Core Web API：Minimal API · 依赖注入 · 中间件管道
- C# async/await 正确姿势：Task · CancellationToken · 异常处理
- Entity Framework Core：Code First · Migration · PostgreSQL Provider
- MQTT 协议基础：MQTTnet 库 · AWS IoT Core Thing 注册 · TLS 证书
- 后台服务：`IHostedService` · `BackgroundService`

### 每周任务

| 周次 | 任务 | 产出 |
|------|------|------|
| 第 1–2 周 | 项目脚手架 + AWS IoT Core 接入 | Solution 结构 · Docker Compose · IoT Core 测试设备连通 |
| 第 3–4 周 | 设备管理 API | CRUD + EF Core · JWT Auth · Swagger 文档 |
| 第 5–6 周 | 遥测数据摄取 | MQTT 订阅 · `IHostedService` · 数据写库 |
| 第 7 周 | 收尾 | xUnit 单元测试 · README · GitHub Push |

### 技术栈

`ASP.NET Core` · `EF Core` · `AWS IoT Core` · `MQTTnet` · `PostgreSQL` · `JWT` · `xUnit`

### Sprint 1 交付物

- 可运行的设备管理 API（含 Swagger）
- 通过 AWS IoT Core 接收遥测数据并持久化到 PostgreSQL
- xUnit 覆盖核心业务逻辑

---

## Sprint 2 — 消息队列 + 实时告警（第 8–16 周）

**目标**：引入完整消息队列架构，实现高并发遥测处理和实时告警推送。

### 学习内容（刚好够用）

- **SQS**：消费者模式 · 可见性超时 · 死信队列 · .NET AWS SDK
- **SNS**：Topic · Subscription · 多协议推送
- **Amazon MQ + MassTransit**：Outbox Pattern · 消息幂等性 · 死信处理
- **并发编程**：`Channel<T>` 管道 · 背压控制 · `BenchmarkDotNet` 压测
- **实时推送**：SignalR Hub · Redis Backplane
- **缓存**：Redis 设备状态缓存 · 告警去重 · 滑动窗口限流
- **可观测性**：Serilog 结构化日志 · CloudWatch Metrics

### 消息队列架构说明

```
AWS IoT Core
    │  规则引擎路由
    ▼
Amazon SQS ──► .NET Worker Service ──► PostgreSQL（写遥测）
                    │
                    ▼ 阈值超限
               告警规则引擎
                    │
                    ▼
              Amazon SNS Topic
               ├── 邮件订阅
               ├── SQS 订阅 ──► 日志服务
               └── Lambda 订阅
                    │
             Amazon MQ (RabbitMQ)
          （微服务间事件总线，MassTransit）
                    │
              SignalR Hub ──► 前端实时推送
```

### 选型理由

| 服务 | 选型理由 |
|------|----------|
| SQS | 设备消息量大、允许短暂延迟，队列缓冲天然适合遥测摄取 |
| SNS | 告警需要同时通知多个下游（邮件 + 日志 + 前端），一对多广播 |
| Amazon MQ | 微服务间需要 AMQP 语义和事务性消息，MassTransit 屏蔽 broker 差异 |

### 每周任务

| 周次 | 任务 | 产出 |
|------|------|------|
| 第 8–9 周 | SQS 消费者服务 | IoT Core 规则引擎 → SQS → Worker Service 消费写库 |
| 第 10–11 周 | 告警规则引擎 + SNS | 阈值检测 → SNS Publish → 邮件通知端到端验证 |
| 第 12–13 周 | Amazon MQ + MassTransit | Outbox Pattern 落地，消息不丢失保障 |
| 第 14–15 周 | SignalR + ElastiCache | 实时推送 + Redis 缓存最新设备状态 |
| 第 16 周 | 可观测性 | CloudWatch Dashboard + Serilog 结构化日志 |

### 技术栈

`SQS` · `SNS` · `Amazon MQ` · `MassTransit` · `Outbox Pattern` · `Channel<T>` · `SignalR` · `ElastiCache` · `LocalStack`

### Sprint 2 交付物

- 完整消息链路：IoT Core → SQS → 处理 → SNS 告警 → Amazon MQ 事件总线
- BenchmarkDotNet 压测报告（目标：10k msg/s 吞吐）
- CloudWatch Dashboard 截图
- 实时告警推送的简单 React 前端演示页

---

## Sprint 3 — DDD 重构 + 微服务拆分（第 17–24 周）

**目标**：将单体服务按 DDD 拆分为三个独立微服务，引入 Clean Architecture 分层。

### 学习内容（刚好够用）

- **DDD 战术模式**：聚合根 · 值对象 · 领域事件 · 仓储 · 领域服务
- **限界上下文划分**：设备管理 BC / 遥测采集 BC / 告警通知 BC
- **CQRS + MediatR**：Command Handler · Query Handler · Pipeline Behavior
- **Clean Architecture 分层**：Domain → Application → Infrastructure → API
- **Amazon API Gateway**：JWT Authorizer · 路由 · Rate Limiting · ECS 集成
- **集成测试**：TestContainers · WebApplicationFactory · LocalStack

### DDD 核心概念对照（IoT 场景）

| DDD 概念 | 在本项目中的体现 |
|----------|----------------|
| 聚合根 | `Device`（含设备状态、连接历史） |
| 值对象 | `DeviceLocation` · `TelemetryReading` · `AlertThreshold` |
| 领域事件 | `DeviceConnectedEvent` · `ThresholdBreachedEvent` |
| 仓储 | `IDeviceRepository` · `ITelemetryRepository` |
| 限界上下文 | 设备管理 / 遥测采集 / 告警通知（三个独立微服务） |

### 每周任务

| 周次 | 任务 | 产出 |
|------|------|------|
| 第 17–19 周 | DDD 重构现有服务 | 识别聚合根、值对象、领域事件，重构分层 |
| 第 20–21 周 | CQRS + MediatR 全面改造 | 所有写操作改 Command Handler，读操作改 Query Handler |
| 第 22–23 周 | 微服务独立部署 + API Gateway | 三个服务独立仓库，API Gateway 统一入口 |
| 第 24 周 | 集成测试 + 架构文档 | TestContainers 集成测试 · ADR 文档 · 架构图更新 |

### 技术栈

`DDD` · `CQRS` · `MediatR` · `Clean Architecture` · `Amazon API Gateway` · `TestContainers` · `ADR`

### Sprint 3 交付物

- 三个微服务按 Clean Architecture 分层，独立 GitHub 仓库
- Amazon API Gateway 统一入口，JWT 鉴权
- DDD 架构图（含限界上下文划分）
- ADR（架构决策记录）文档
- 集成测试覆盖率 > 70%

---

## Sprint 4 — AWS 生产化部署（第 25–30 周）

**目标**：将三个微服务完整部署到 AWS，Terraform IaC 管理全套资源，GitHub Actions 自动化 CI/CD。

> **优势**：你已有 AWS 认证 + Terraform + GitHub Actions 经验，这个 Sprint 进度会比其他人快很多。

### AWS 架构总览

```
Internet
    │
Amazon API Gateway（JWT Authorizer · Rate Limiting）
    │
Application Load Balancer
    ├── ECS Fargate — 设备管理服务
    ├── ECS Fargate — 遥测摄取服务
    └── ECS Fargate — 告警通知服务
         │
    ┌────┴────────────────────────────────┐
    │                                     │
Amazon MQ          RDS PostgreSQL         ElastiCache Redis
(RabbitMQ)         (Multi-AZ)             (集群模式)
    │
Amazon SQS ◄── AWS IoT Core ◄── 设备（MQTT over TLS）
    │
Amazon SNS
    ├── 邮件
    └── Lambda
         │
CloudWatch + X-Ray（监控 · 追踪 · 告警）
```

### Terraform 资源清单

```hcl
# 需要用 Terraform 管理的 AWS 资源
module "networking"   # VPC · Subnets · Security Groups · NAT Gateway
module "iot_core"     # IoT Core · Thing · Policy · Rule
module "messaging"    # SQS · SNS · Amazon MQ
module "compute"      # ECS Cluster · Task Definitions · Services · ALB
module "database"     # RDS PostgreSQL · Parameter Group · Subnet Group
module "cache"        # ElastiCache Redis · Subnet Group
module "gateway"      # API Gateway · JWT Authorizer · Stages
module "monitoring"   # CloudWatch Dashboards · Alarms · X-Ray
module "cicd"         # ECR · IAM Roles · OIDC Provider
```

### GitHub Actions CI/CD Pipeline

```yaml
# 触发条件：PR merge 到 main
jobs:
  build-and-test:     # dotnet build + dotnet test
  docker-build-push:  # Docker Build → ECR Push（OIDC 无密钥）
  deploy-staging:     # ECS Rolling Update → 健康检查 Gate
  deploy-production:  # 手动审批 → ECS Blue/Green Deploy
```

### 每周任务

| 周次 | 任务 | 产出 |
|------|------|------|
| 第 25–26 周 | Terraform 搭 AWS 基础设施 | VPC · RDS · ElastiCache · SQS · SNS · Amazon MQ · ECS Cluster |
| 第 27 周 | ECS Fargate 部署三个服务 | Task Definition · Service · ALB · 端到端联调 |
| 第 28 周 | GitHub Actions CI/CD | OIDC + ECR + ECS Rolling Update，PR 触发自动部署 Staging |
| 第 29 周 | CloudWatch Dashboard + X-Ray | SQS 队列深度告警 · ECS 资源告警 · 分布式追踪链路 |
| 第 30 周 | 项目文档 + 面试准备 | 架构图（含 AWS 资源）· 成本估算 · 压测报告 · STAR 故事 |

### 技术栈

`ECS Fargate` · `ECR` · `RDS PostgreSQL` · `ElastiCache` · `Amazon API Gateway` · `CloudWatch` · `X-Ray` · `Terraform` · `GitHub Actions` · `OIDC`

### Sprint 4 交付物

- 完整 Terraform 代码（`terraform apply` 一键起全套环境）
- GitHub Actions 全自动 CI/CD Pipeline
- CloudWatch Dashboard 截图（含关键指标）
- X-Ray 分布式追踪链路截图
- AWS 架构图（可用于面试展示）

---

## AWS 费用规划

### 开发阶段（Sprint 1–3）：接近零费用

| 服务 | 本地替代 | 费用 |
|------|----------|------|
| PostgreSQL | Docker | $0 |
| Redis | Docker | $0 |
| RabbitMQ | Docker | $0 |
| SQS / SNS | LocalStack | $0 |
| MQTT Broker | EMQX Docker / IoT Core 免费层 | $0 |

### 部署阶段（Sprint 4）：1–2 个月

| 服务 | 预估月费（AUD） | 备注 |
|------|---------------|------|
| ECS Fargate（3 服务） | ~15–20 | 0.5 vCPU · 1GB 各服务 |
| RDS db.t3.micro | ~25–30 | Single-AZ 开发用 |
| ElastiCache t3.micro | ~15 | |
| Amazon MQ mq.t3.micro | ~20 | |
| SQS / SNS | ~1–2 | 开发量级 |
| AWS IoT Core | $0 | 免费层 250k msg/月 |
| CloudWatch | ~3–5 | |
| **合计** | **~80–95** | **2 个月约 AUD 160–190** |

> **IoT Core 免费层注意**：12 个月有效期从**账号创建日**开始计算，不是从开始使用 IoT Core 的日期。2025 年 7 月 15 日后新建账号可获最高 $200 credits。

### 节省费用的建议

1. 部署完成、截图留档后立即 `terraform destroy` 销毁环境
2. RDS 使用 Single-AZ（开发演示用，不需要 Multi-AZ）
3. ECS Task 配置最小规格（0.5 vCPU · 1GB），演示时按需启动

---

## 面试资产清单

完成全部 Sprint 后，你将拥有：

### GitHub 仓库内容

- [ ] 三个微服务独立仓库，Clean Architecture 分层
- [ ] Terraform IaC（一键部署全套 AWS 环境）
- [ ] GitHub Actions CI/CD Pipeline（OIDC + ECR + ECS）
- [ ] Docker Compose 本地开发环境
- [ ] xUnit 单元测试 + TestContainers 集成测试
- [ ] ADR 架构决策记录文档
- [ ] 完整 README（含架构图、技术选型说明）

### 可量化的面试数据

- 遥测摄取吞吐量（BenchmarkDotNet 压测报告）
- 单元测试覆盖率（目标 > 70%）
- CloudWatch Dashboard 截图（真实 AWS 监控数据）
- X-Ray 分布式追踪链路截图

### 面试时可讲的故事（STAR 格式）

1. **高并发**：用 `Channel<T>` 替换同步写入，将遥测摄取吞吐提升 X 倍
2. **消息队列**：SQS + Outbox Pattern 解决 IoT 数据丢失问题的设计思路
3. **DDD 重构**：识别三个限界上下文，把单体拆成微服务的决策过程
4. **AWS 部署**：Terraform IaC + ECS Fargate + GitHub Actions 全自动化的实现细节
5. **PostgreSQL 调优**：结合现有 Raptor OS 经验，讲 RDS Parameter Group 和索引优化

---

## 参考资源

### 书籍

- 《实现领域驱动设计》— Vaughn Vernon（DDD 圣经）
- 《.NET 微服务：容器化 .NET 应用架构》— Microsoft 官方免费电子书

### 官方文档

- [ASP.NET Core 文档](https://docs.microsoft.com/aspnet/core)
- [AWS IoT Core 开发者指南](https://docs.aws.amazon.com/iot/latest/developerguide)
- [MassTransit 文档](https://masstransit.io/documentation)
- [Terraform AWS Provider](https://registry.terraform.io/providers/hashicorp/aws)

### 工具

- **LocalStack**：本地模拟 AWS 服务（`localstack/localstack` Docker 镜像）
- **TestContainers**：集成测试用真实容器（`Testcontainers` NuGet 包）
- **BenchmarkDotNet**：.NET 性能基准测试

---

*最后更新：2026 年 4 月*
