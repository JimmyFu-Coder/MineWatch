# .NET 开发学习笔记

## 1. C# 项目结构

### Top-level Statements 文件顺序规则
```
正确顺序（必须）：
1. using 语句
2. 可执行代码 (top-level statements)
3. 类型定义 (class, record, interface)

错误示例：
  var x = 1;           ← 可执行代码
  class Foo { }        ← 类型定义在后面 ✗
```

### 编译目标
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
  </PropertyGroup>
</Project>
```

---

## 2. NuGet 包管理

### 添加包
```bash
dotnet add <project> package <PackageName>
```

### 示例
```bash
dotnet add TruckMocker/TruckMocker.csproj package MQTTnet
dotnet add TruckMocker/TruckMocker.csproj package CsvHelper
```

---

## 3. CsvHelper - CSV 读写

### 读取 CSV
```csharp
using CsvHelper;
using CsvHelper.Configuration;

var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    HeaderValidated = null,
    MissingFieldFound = null
};

using var reader = new StreamReader(csvPath);
using var csv = new CsvReader(reader, config);
var records = csv.GetRecords<MyClass>();
```

### 自定义类型转换（处理 NULL）
```csharp
public class NullDoubleConverter : DefaultTypeConverter
{
    public override object ConvertFromString(string text, IReaderRow row, MemberMapData memberMapData)
    {
        if (string.IsNullOrEmpty(text) || text.Trim().ToUpper() == "NULL")
            return null;
        return double.Parse(text, CultureInfo.InvariantCulture);
    }
}

public class MyClass
{
    [Name("col_name")]
    [TypeConverter(typeof(NullDoubleConverter))]
    public double? MyProperty { get; set; }
}
```

### 写入 CSV
```csharp
using var writer = new StreamWriter(outputPath);
using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture));
csv.WriteRecords(records);
```

---

## 4. MQTTnet - MQTT 客户端

### 安装
```bash
dotnet add package MQTTnet
```

### 连接 & 发布
```csharp
using MQTTnet;

var factory = new MqttClientFactory();
using var client = factory.CreateMqttClient();

var options = new MqttClientOptionsBuilder()
    .WithTcpServer("localhost", 1883)
    .Build();

await client.ConnectAsync(options);

// 发布消息
var message = new MqttApplicationMessageBuilder()
    .WithTopic("vehicle_no")           // topic 名称
    .WithPayload(jsonString)
    .Build();

await client.PublishAsync(message);
await client.DisconnectAsync();
```

### 配置对象模式
```csharp
public record MqttConfig
{
    public string Server { get; init; } = "localhost";
    public int Port { get; init; } = 1883;
}
```

---

## 5. 并行异步处理

### 并行发送（多任务同时运行）
```csharp
var tasks = items.Select(async item =>
{
    await DoSomethingAsync(item);
    return result;
}).ToList();

await Task.WhenAll(tasks);  // 等待所有任务完成
```

### 关键点
- `async` 方法不会阻塞其他任务
- `await Task.Delay()` 只暂停当前 Task
- `Task.WhenAll()` 等待所有 Task 完成
- `Interlocked.Add()` 用于跨线程更新计数器

---

## 6. Docker Compose - Mosquitto MQTT Broker

### docker-compose.yml
```yaml
services:
  mosquitto:
    image: eclipse-mosquitto:2
    container_name: minewatch-mosquitto
    ports:
      - "1883:1883"      # MQTT plain
      - "9001:9001"      # WebSocket
    volumes:
      - mosquitto_data:/mosquitto/data
      - mosquitto_logs:/mosquitto/log

volumes:
  mosquitto_data:
  mosquitto_logs:
```

### 启动
```bash
docker compose up -d mosquitto
```

### 本地已有 Mosquitto?
```bash
# 检查端口
lsof -i :1883

# 本地已运行则无需再启动 Docker
```

### 测试订阅
```bash
mosquitto_sub -t "#" -v          # 订阅所有主题
mosquitto_sub -t "VEHICLE_#" -v  # 订阅车辆主题
```

---

## 7. 轨迹数据模型

### 配置参数
```csharp
public record SimulationConfig
{
    public int VehicleCount { get; init; } = 5;
    public int PointsPerVehicle { get; init; } = 300;
    public int FrequencyHz { get; init; } = 1;
    public double AvgSpeedMps { get; init; } = 30;
    public (double lat, double lon)[] Bounds { get; init; }
}
```

### 轨迹记录
```csharp
public class TrajectoryRecord
{
    public string VehicleNo { get; set; }
    public DateTime Timestamp { get; set; }
    public double Lat { get; set; }
    public double Lon { get; set; }
    public double Speed { get; set; }
    public double Heading { get; set; }
}
```

---

## 8. 事件注入接口（预留扩展）

```csharp
public interface IEventInjector
{
    void Inject(int vehicleIndex, int pointIndex,
        ref double? lat, ref double? lon,
        ref double? speed, ref double? heading);
}
```

后续可实现：
- 超速注入
- 越界注入
- 急刹注入

---

## 9. 常用命令

```bash
# 构建
dotnet build

# 运行
dotnet run --project <path>

# 添加包
dotnet add <project> package <name>

# Docker
docker compose up -d <service>
docker rm -f <container>
```

---

## 10. Task / async / await 语法

### 基础概念

| 关键字 | 作用 |
|--------|------|
| `async` | 标记方法为异步方法 |
| `await` | 等待异步操作完成，**不阻塞线程** |
| `Task` | 代表一个可能还未完成的异步操作 |

---

### async 方法的四种返回类型

```csharp
// 1. 无返回值
async Task RunAsync()
{
    await Task.Delay(1000);
}

// 2. 返回 T
async Task<string> GetNameAsync()
{
    await Task.Delay(1000);
    return "Truck-01";
}

// 3. ValueTask<T>（高性能场景，见下文）
async ValueTask<string> GetNameValueAsync()
{
    await Task.Delay(1000);
    return "Truck-01";
}

// ❌ 错误：async 不能返回 T
async string Error() { }  // 编译错误！
```

---

### 调用异步方法

```csharp
// ✅ 正确：在 async 方法里用 await
async Task Main()
{
    var data = await GetDataAsync();
    Console.WriteLine(data);
}

// ✅ 正确：非 async 方法阻塞式等待（仅限控制台程序 main）
var data = GetDataAsync().GetAwaiter().GetResult();

// ❌ 错误：普通方法里直接 await
string GetName()
{
    return await GetNameAsync();  // 编译错误！
}
```

---

### 执行流程

```
调用 GetDevicesAsync()
    ↓
httpClient.GetAsync() 发出 HTTP 请求
    ↓
此时线程去干别的（不阻塞）
    ↓
HTTP 响应回来
    ↓
继续执行后面的代码
```

---

### Task.WhenAll — 等待多个 Task 同时完成

```csharp
// 等待多个无返回值的 Task 完成
await Task.WhenAll(
    DoAsync("A"),
    DoAsync("B"),
    DoAsync("C")
);

// 等待多个有返回值的 Task，返回结果数组
string[] results = await Task.WhenAll(
    FetchAsync("url1"),
    FetchAsync("url2"),
    FetchAsync("url3")
);
// results = [result1, result2, result3]
```

**如果任意一个失败，`WhenAll` 会等所有 Task 完成后才抛出异常。**

---

### Task.WhenAny — 任意一个完成就返回

```csharp
// 任意一个先完成就返回
Task<string> first = await Task.WhenAny(
    httpClient.GetStringAsync("fast-url"),
    httpClient.GetStringAsync("slow-url")
);
```

---

### Task 的创建方式

```csharp
// 直接返回已完成的 Task
Task<string> t1 = Task.FromResult("done");

// 创建失败状态的 Task
Task<string> t2 = Task.FromException<string>(new Exception("failed"));

// 从头创建
Task t3 = new Task(() => Console.WriteLine("执行"));
t3.Start();
```

---

### Select + ToList 实现并行

```csharp
// Select 返回 IEnumerable<Task>，此时只是"配方"，还没执行
var tasks = urls.Select(async url => await GetAsync(url));

// .ToList() 强制立即枚举所有元素，触发所有 lambda 同时执行
var taskList = tasks.ToList();

// 等待全部完成
await Task.WhenAll(taskList);
```

---

### ValueTask vs Task

| | `Task` | `ValueTask` |
|--|--------|-------------|
| 引入版本 | C# 5.0 / async 所有版本 | C# 7.0 |
| 内存 | 堆分配（new Task） | 栈分配 or 堆分配（取决于情况） |
| 适用场景 | 通用 | 高性能 hot path |
| 限制 | — | 只能在 async 方法中使用一次 await |

**什么时候用 ValueTask：**
- 方法内部逻辑会**同步完成**（不需要真的 await）
- 性能敏感的高频调用路径
- 避免 GC 压力

```csharp
// 场景：缓存命中时直接返回，不需要 await
async ValueTask<string> GetCachedAsync(string key)
{
    if (cache.TryGet(key, out var cached))
        return cached;  // 同步完成，无堆分配

    var result = await FetchFromDbAsync(key);  // 需要 await
    return result;
}
```

**如果需要多次 await 同一个异步操作，必须用 Task，不能用 ValueTask。**

---

### C# Task vs JS Promise 静态方法对比

| JS Promise | C# Task | 作用 |
|------------|---------|------|
| `Promise.all()` | `Task.WhenAll()` | 全部完成，任意失败则整体失败 |
| `Promise.allSettled()` | — | 等全部完成，不管成功失败 |
| `Promise.race()` | `Task.WhenAny()` | 任意一个完成/失败就返回 |
| `Promise.any()` | — | 任意一个成功就返回 |
| `Promise.resolve()` | `Task.FromResult()` | 直接返回成功结果 |
| `Promise.reject()` | `Task.FromException()` | 直接返回失败 |

---

## 待学习/后续

- [ ] AWS IoT Core 连接（X.509 证书、TLS）
- [ ] 数据库集成（EF Core + PostgreSQL）
- [ ] 警报逻辑实现（超速、越界、滞留检测）
- [ ] 设备端 vs 云端事件计算架构