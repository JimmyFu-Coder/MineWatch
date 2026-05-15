# Sprint 2A 第 6 周知识点复习

## 1. Amazon SQS 核心概念

### 标准队列 vs FIFO 队列

| | Standard | FIFO |
|---|---|---|
| 顺序 | 不保证 | 严格先进先出 |
| 吞吐 | 无限 | 300 msg/s（无批量）/ 3000 msg/s（批量） |
| 重复 | 可能重复 | 精确一次（去重） |
| 本项目 | 用 Standard | 遥测数据不需要顺序 |

### 关键参数

- **Visibility Timeout**：消费者取走消息后，消息对其他消费者不可见的时间窗口。处理完了就删除，没处理完消息自动回到队列。
- **Max Receive Count**：消息被消费但未删除的次数上限，超过后转入 DLQ。
- **Long Polling**（`WaitTimeSeconds = 20`）：消费者空请求时等待最多 20 秒，有消息立即返回。减少空轮询，降低 API 调用次数。
- **消息保留期**：默认 4 天，最长 14 天。

### 死信队列（DLQ）

处理失败的消息最终归宿。不是单独的队列类型，就是一个普通的 SQS 队列，通过主队列的 **Redrive Policy** 关联：

```json
{
  "maxReceiveCount": 3,
  "deadLetterTargetArn": "arn:aws:sqs:..."
}
```

消息被消费 3 次都没删除（处理失败），SQS 自动转移到 DLQ。

## 2. AWS SDK for .NET

### 依赖注入配置

```csharp
// 从 appsettings.json 的 "AWS" 节读取配置
builder.Services.AddDefaultAWSOptions(builder.Configuration.GetAWSOptions());
// 注册 SQS 客户端到 DI 容器
builder.Services.AddAWSService<IAmazonSQS>();
```

`appsettings.json` 中的配置：
```json
{
  "AWS": {
    "ServiceURL": "http://localhost:4566",  // LocalStack 地址
    "Region": "ap-southeast-2"
  }
}
```

`ServiceURL` 是关键——生产环境不需要设置（SDK 自动用 AWS 区域端点），但 LocalStack 必须设置。

### 核心 API

| 操作 | 方法 |
|------|------|
| 创建队列 | `CreateQueueAsync(queueName)` |
| 获取队列属性 | `GetQueueAttributesAsync(queueUrl, attributeNames)` |
| 设置队列属性 | `SetQueueAttributesAsync(queueUrl, attributes)` |
| 发送消息 | `SendMessageAsync(queueUrl, messageBody)` |
| 接收消息 | `ReceiveMessageAsync(request)` |
| 删除消息 | `DeleteMessageAsync(queueUrl, receiptHandle)` |
| 批量删除 | `DeleteMessageBatchAsync(queueUrl, entries)` |

## 3. IHostedService vs BackgroundService

| | IHostedService | BackgroundService |
|---|---|---|
| 基类 | 接口，需实现 `StartAsync` + `StopAsync` | 抽象类，只需实现 `ExecuteAsync` |
| 生命周期 | `StartAsync` 跑完就结束 | `ExecuteAsync` 持续运行直到取消 |
| 适用场景 | 一次性初始化任务 | 长期运行的后台任务 |
| 本项目 | `SqsBootstrapService`（创建队列） | `MqttSubscriberService`（持续订阅） |

**启动顺序**：ASP.NET Core 按 DI 注册顺序执行 `StartAsync`，所以 Bootstrap 必须先注册。

## 4. LocalStack

本地模拟 AWS 服务的 Docker 容器，开发阶段零费用。

- 端口：`4566`（统一入口）
- 版本注意：`latest` 标签可能需要 license，用固定版本如 `3.8.1`
- AWS CLI 访问：`aws --endpoint-url=http://localhost:4566 <command>`
- 验证健康：`curl http://localhost:4566/_localstack/health`

## 5. Primary Constructor（C# 12）

```csharp
// 传统写法
public class MyService
{
    private readonly IAmazonSQS _sqsClient;
    public MyService(IAmazonSQS sqsClient) { _sqsClient = sqsClient; }
}

// Primary constructor
public class MyService(IAmazonSQS sqsClient)
{
    // sqsClient 直接在类里使用
}
```

注意：primary constructor 的参数直接在方法中使用，不需要 `this.xxx` 或私有字段。但在 lambda 或事件处理器中可能需要保存为字段（如 `_stoppingToken`）。

## 6. Architecture: 为什么 SQS 替换 Channel

| 问题 | Channel 怎么处理 | SQS 怎么处理 |
|------|-----------------|-------------|
| 应用崩溃 | 内存中消息全丢 | 消息持久化在队列 |
| 多实例部署 | 每个实例独立 Channel，无法共享 | 多实例消费同一队列 |
| 处理失败 | 需要自己写重试 | visibility timeout 自动回到队列 |
| 死消息 | 没有机制 | DLQ 自动接收 |
| 消息积压 | Bounded capacity 阻塞生产者 | 队列自动缓冲，消费者按速率拉取 |

**核心区别**：Channel 是进程内的高性能管道，SQS 是跨进程的可靠消息队列。生产环境需要后者。

## 7. TDD 实践：SqsConsumerWorker

### 测试文件结构

```csharp
public class SqsConsumerWorkerTests
{
    // 四个依赖的 mock/stub
    private readonly Mock<IAmazonSQS> _sqsMock;
    private readonly SqsConfig _sqsConfig;
    private readonly IDbContextFactory<MineWatchDbContext> _dbContextFactory;
    private readonly Mock<ILogger<SqsConsumerWorker>> _loggerMock;

    // 构造函数：初始化所有 mock 和 InMemoryDatabase
    // 辅助方法：CreateReading() 生成测试数据
    // 测试方法：ExecuteAsync_ValidMessages_WritesToDbAndDeletesFromSqs
}
```

### 测试的三段式

| 阶段 | 做什么 | 关键 API |
|------|--------|----------|
| Arrange | 构造测试数据，mock SQS 行为 | `JsonSerializer.Serialize`, `new Message`, `_sqsMock.Setup` |
| Act | 构造 worker 并执行 | `TestableSqsConsumerWorker.RunExecuteAsync` |
| Assert | 验证数据库和 SQS 调用 | `db.TelemetryReadings.ToListAsync()`, `_sqsMock.Verify` |

### Mock SQS 的技巧

BackgroundService 是无限循环，测试时需要让它停下来。用 `callCount` 计数器：

```csharp
var callCount = 0;
_sqsMock.Setup(s => s.ReceiveMessageAsync(...))
    .ReturnsAsync(() =>
    {
        callCount++;
        if (callCount == 1) return response;    // 第 1 次：返回消息
        throw new OperationCanceledException();  // 第 2 次：终止循环
    });
```

### 调用 protected 方法

`ExecuteAsync` 是 `protected`，测试类无法直接调用。解决方法：写一个测试子类。

```csharp
internal class TestableSqsConsumerWorker(...) : SqsConsumerWorker(...)
{
    public Task RunExecuteAsync(CancellationToken ct) => ExecuteAsync(ct);
}
```

### 验证数据库

用 `IDbContextFactory` 创建新的 DbContext 查询，断言记录数和内容：

```csharp
await using var db = _dbContextFactory.CreateDbContext();
var saved = await db.TelemetryReadings.ToListAsync();
Assert.Single(saved);
Assert.Equal(reading.VehicleNo, saved[0].VehicleNo);
```

### 验证 Mock 调用

用 `Verify` 确认方法被调用了正确次数和参数：

```csharp
_sqsMock.Verify(s => s.DeleteMessageAsync(
    It.Is<DeleteMessageRequest>(r =>
        r.QueueUrl == _sqsConfig.QueueUrl &&
        r.ReceiptHandle == "handle-1"),
    It.IsAny<CancellationToken>()),
    Times.Once);
```

## 8. SqsConsumerWorker.ExecuteAsync 实现逻辑

```
while (!stoppingToken.IsCancellationRequested)
    try
        1. ReceiveMessageAsync 从 SQS 拉消息
        2. foreach 每条消息：
           a. JsonSerializer.Deserialize 反序列化
           b. null 检查，失败则跳过并记日志
           c. DbContext 写入数据库
           d. DeleteMessageAsync 从 SQS 删除
    catch OperationCanceledException → break 退出循环
    catch Exception → 记日志，继续循环
```

### 关键设计点

- **先写数据库再删消息**：确保数据持久化后才从队列移除
- **每条消息独立 DbContext**：避免一条消息失败影响其他消息
- **OperationCanceledException 单独 catch**：这是正常的退出信号，不是错误
- **其他异常只记日志不抛出**：worker 不应该因为单次失败就崩溃

## 9. 快速查 API 的方法

遇到不认识的 .NET API，**F12 跳进去看签名** → 再 F12 进参数类型看属性 → IDE 自动提示补全属性列表。不需要死记硬背。

- F12 = Go to Definition（看方法签名和文档注释）
- 打出 `{` 后 IDE 会列出对象所有可设置的属性
- 对象初始化器语法 `new X { Prop = value }` 是 C# 标准写法
