# MineWatch 测试指南

## 1. 什么值得测，什么不值得

### 核心原则：测业务规则，不测框架

```csharp
// 不值得测 — 这是在测 EF Core 的 FindAsync 能不能找到数据
[Fact]
public async Task GetById_ExistingId_ReturnsDevice()
{
    var device = await service.GetByIdAsync(someId);
    Assert.Equal("TRUCK-001", device.Name);
}

// 值得测 — 这是在测你的业务规则：部分更新时 null 字段不覆盖
[Fact]
public async Task Update_OnlyName_NameChanged_TypeUnchanged()
{
    await service.UpdateAsync(id, new UpdateDeviceRequest(Name: "NewName", Type: null));
    var device = await service.GetByIdAsync(id);
    Assert.Equal("NewName", device.Name);
    Assert.Equal("OriginalType", device.Type); // null 没覆盖原值
}
```

### 判断标准

| 场景 | 是否值得测 | 原因 |
|------|-----------|------|
| 纯 CRUD（GetById, GetAll, Delete） | 否 | 只是转发给 EF Core |
| 有业务规则的方法 | 是 | 你的逻辑，你负责 |
| 字段映射（JSON → 对象） | 是 | 字段名拼写错误是常见 bug |
| 异常处理行为 | 是 | 缺字段时抛异常还是给默认值，是业务决策 |
| 框架自带行为（序列化、反序列化） | 否 | 微软已经测过了 |

### 举例

```
DeviceService — 不值得花时间测
  ├── GetById → FindAsync，EF Core 的行为
  ├── GetAll → Skip/Take，分页是 LINQ 的行为
  ├── Create → new Device() + Add，没有业务逻辑
  ├── Delete → Remove，没有业务逻辑
  └── Update → 唯一有点价值的：null 字段跳过（一个测试就够了）

TelemetryParser — 值得测
  ├── 正常映射 → 验证字段名没拼错
  └── 缺少字段 → 验证异常行为

TelemetryBatchWriter — 值得测
  ├── 批次满时写入 → 你的循环逻辑
  ├── 超时写入 → 你的 CancellationToken 逻辑
  ├── 多批次拆分 → 你的循环边界
  └── 重试 → 你的 retry 逻辑
```

## 2. TelemetryBatchWriter 测试的问题

### 问题 1：Task.Delay 不可靠

```csharp
await writer.StartAsync(CancellationToken.None);
await Task.Delay(1000);  // 祈祷 1 秒内能写完
var count = await dbContext.TelemetryReadings.CountAsync();
Assert.Equal(12, count);
```

**问题**：在慢机器或 CI 环境里，1 秒可能不够。测试变 flaky（有时过有时不过）。

**更好的方式**：用 `TaskCompletionSource` 做信号通知，或者用 `Polling` 等待条件满足：

```csharp
// 等待条件满足，而不是等固定时间
await Task.WhenAny(
    writer.ExecuteTask!,
    Task.Delay(5000)
);
```

但目前为了简单，Task.Delay + 足够大的等待时间是可以接受的折中。

### 问题 2：Times.Exactly(N) 和实现耦合太紧

```csharp
dbContextFactory.Verify(
    f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()),
    Times.Exactly(3));  // 为什么是 3？改一下实现就断
```

**问题**：这个断言验证的是"调了几次"，而不是"结果对不对"。如果实现改了（比如加了重试），测试就失败，但功能可能是对的。

**原则**：优先用 state verification（验证最终状态），少用 behavior verification（验证调用次数）。

```csharp
// 好的验证：最终结果对不对
Assert.Equal(1, saved.Count);
Assert.Equal("TRUCK-001", saved[0].VehicleNo);

// 尽量少用的验证：调了几次
dbContextFactory.Verify(f => f.CreateDbContextAsync(...), Times.Exactly(3));
```

### 问题 3：Retry 测试比实现还复杂

测试 retry 需要闭包计数器模拟"前 N 次失败"：

```csharp
var callCount = 0;
factory.Setup(f => f.CreateDbContextAsync(...))
    .ReturnsAsync(() =>
    {
        callCount++;
        if (callCount <= failCount)
            throw new InvalidOperationException("Simulated DB failure");
        return new MineWatchDbContext(options);
    });
```

为了测试一个简单的 for 循环 + try-catch，需要写这么多 mock 代码。这是因为 `WriteBatchAsync` 依赖 `DbContext`，没法直接单元测试 retry 逻辑。

**教训**：如果一个测试的 setup 代码比被测方法还长，考虑是不是测试的粒度不对，或者方法本身的依赖设计有问题。

## 3. TDD 导致的重构

### 问题：HandleMessageAsync 不可测试

原始代码：

```csharp
private async Task HandleMessageAsync(MqttApplicationMessageReceivedEventArgs eventArgs)
{
    var json = JsonSerializer.Deserialize<JsonElement>(
        eventArgs.ApplicationMessage.Payload.ToArray());     // ① 依赖 MQTT 框架
    var reading = new TelemetryReading
    {
        VehicleNo = json.GetProperty("vehicle_no").GetString()!,  // ② 业务逻辑
        // ...
    };
    await channel.Writer.WriteAsync(reading);               // ③ 依赖 Channel
}
```

三个问题：
1. **不可复用** — 绑死了 `MqttApplicationMessageReceivedEventArgs`，HTTP 接口来了没法复用
2. **不可测试** — 想测字段映射，必须先构造一个 `MqttApplicationMessageReceivedEventArgs`
3. **职责不清** — 出了 bug 不知道是 MQTT 数据没拿到、JSON 字段名写错、还是 Channel 满了

### 解决：提取 TelemetryParser

```
重构前：                          重构后：
MqttSubscriberService             MqttSubscriberService
  ├── 取 MQTT payload               ├── 取 payload → Encoding.UTF8.GetString
  ├── JSON 解析 + 字段映射          ├── TelemetryParser.Parse(payload)
  └── 写 Channel                    └── 写 Channel

                                   TelemetryParser（新建）
                                     └── Parse(string) → TelemetryReading
```

重构后的 `TelemetryParser`：
- 不依赖 MQTT、不依赖 Channel、不依赖 logger
- 输入 string，输出 TelemetryReading
- 测试只需要一行 `TelemetryParser.Parse(json)`，不需要 mock 任何东西

```csharp
// 测试变得极简
[Fact]
public void Parse_WhenValidJson_ReturnsTelemetryReading()
{
    var payload = """{"vehicle_no":"TRUCK-001",...}""";
    var reading = TelemetryParser.Parse(payload);
    Assert.Equal("TRUCK-001", reading.VehicleNo);
}
```

### 教训

不是为了测试而重构，而是**不可测试的代码通常意味着结构有问题**。

好的代码结构本身就方便测试：
- 每个方法只做一件事
- 依赖少，容易隔离
- 纯函数（输入 → 输出）最容易测

## 4. 总结原则

1. **测业务规则，不测框架** — CRUD 胶水代码不需要测试
2. **优先 state verification** — 验证结果对不对，而不是调了几次
3. **测试代码应该比实现简单** — 如果 setup 比被测方法还长，重新审视设计
4. **不可测试 = 结构有问题** — 需要大量 mock 才能测的方法，通常职责太多
5. **TDD 是设计工具** — 测试先写，暴露设计问题，推动重构
