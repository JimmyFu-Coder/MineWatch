# Repository 模式设计指南

## 什么是 Repository？

Repository（仓储）是封装数据访问逻辑的设计模式。它在业务逻辑和数据库之间建立一个抽象层，让业务代码无需关心数据从哪来、怎么存。

```
┌─────────────────────────────────────────────────────────┐
│                   业务层（Service）                       │
│                                                          │
│  _repository.GetAllAsync()                               │
│  _repository.AddAsync(device)                            │
│  不关心数据来自 PostgreSQL、MySQL 还是内存                  │
└────────────────────────┬────────────────────────────────┘
                         │  IDeviceRepository 接口
                         ▼
┌─────────────────────────────────────────────────────────┐
│                接口层（IDeviceRepository）                │
│                                                          │
│  Task<IEnumerable<Device>> GetAllAsync();                │
│  Task<Device?> GetByIdAsync(Guid id);                    │
│  Task<Device> AddAsync(Device device);                   │
│  Task UpdateAsync(Device device);                        │
│  Task DeleteAsync(Guid id);                              │
└────────────────────────┬────────────────────────────────┘
                         │  实现注入
                         ▼
┌─────────────────────────────────────────────────────────┐
│               实现层（EfCoreDeviceRepository）            │
│                                                          │
│  await _context.Devices.ToListAsync()                    │
│  await _context.Devices.AddAsync(device)                 │
│  具体实现：EF Core + PostgreSQL                          │
└─────────────────────────────────────────────────────────┘
```

---

## 本项目的 Repository 实现

### IDeviceRepository 接口

```csharp
namespace MineWatch.Infrastructure.Repositories;

public interface IDeviceRepository
{
    Task<IEnumerable<Device>> GetAllAsync();
    Task<Device?> GetByIdAsync(Guid id);
    Task<Device> AddAsync(Device device);
    Task UpdateAsync(Device device);
    Task DeleteAsync(Guid id);
}
```

### EfCoreDeviceRepository 实现

```csharp
using Microsoft.EntityFrameworkCore;
using MineWatch.Infrastructure.Data;
using MineWatch.Infrastructure.Entities;

namespace MineWatch.Infrastructure.Repositories;

public class EfCoreDeviceRepository : IDeviceRepository
{
    private readonly MineWatchDbContext _context;

    public EfCoreDeviceRepository(MineWatchDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Device>> GetAllAsync()
    {
        return await _context.Devices.ToListAsync();
    }

    public async Task<Device?> GetByIdAsync(Guid id)
    {
        return await _context.Devices.FindAsync(id);
    }

    public async Task<Device> AddAsync(Device device)
    {
        await _context.Devices.AddAsync(device);
        await _context.SaveChangesAsync();
        return device;
    }

    public async Task UpdateAsync(Device device)
    {
        _context.Devices.Update(device);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var device = await _context.Devices.FindAsync(id);
        if (device != null)
        {
            _context.Devices.Remove(device);
            await _context.SaveChangesAsync();
        }
    }
}
```

---

## Repository 注册到 DI

```csharp
// Program.cs
builder.Services.AddScoped<IDeviceRepository, EfCoreDeviceRepository>();

// Controller 构造器注入
public DevicesController(IDeviceRepository repository)
{
    _repository = repository;
}
```

**为什么是 Scoped？** — 和 DbContext 一样，每个请求独立实例。

---

## Repository 模式的优点

### 1. 分离关注点

```
❌ 没有 Repository：
Service 直接用 DbContext，测试要启动真实数据库
   ↓
业务逻辑和数据库访问耦合

✅ 有 Repository：
Service 只依赖接口，换个实现就能测试
```

### 2. 方便测试（Mock）

```csharp
// 单元测试：Mock 掉 Repository
[Fact]
public async Task GetAll_ReturnsDevices()
{
    // Arrange
    var mockRepo = new Mock<IDeviceRepository>();
    mockRepo.Setup(r => r.GetAllAsync())
        .ReturnsAsync(new List<Device>
        {
            new() { Id = Guid.NewGuid(), Name = "钻机 A" }
        });

    var service = new DeviceService(mockRepo.Object);

    // Act
    var result = await service.GetAllDevicesAsync();

    // Assert
    Assert.Single(result);
}
```

### 3. 换数据源不改业务代码

```csharp
// PostgreSQL 实现
public class EfCoreDeviceRepository : IDeviceRepository { ... }

// 内存实现（测试用）
public class InMemoryDeviceRepository : IDeviceRepository { ... }

// MongoDB 实现（以后可能用）
public class MongoDbDeviceRepository : IDeviceRepository { ... }

// 注册时切换实现
builder.Services.AddScoped<IDeviceRepository, MongoDbDeviceRepository>();
```

### 4. 统一的数据访问接口

```
Service A ──► IDeviceRepository ──► EfCoreDeviceRepository（PostgreSQL）
Service B ──► IDeviceRepository ──► EfCoreDeviceRepository（PostgreSQL）
Service C ──► IDeviceRepository ──► EfCoreDeviceRepository（PostgreSQL）

所有 service 用同一个接口，数据访问逻辑集中管理
```

---

## Repository 模式的缺点

### 1. 代码量增加

```
没有 Repository：
   Service.GetAll() → 直接用 _context.Devices

有 Repository：
   定义接口 → 写实现类 → 注册到 DI → 注入到 Service
   （多了 3 步）
```

### 2. 过度抽象

```csharp
// ❌ 过度抽象：Repository 只是包装 DbContext，没有额外价值
public async Task<List<Device>> GetAllAsync()
    => await _context.Devices.ToListAsync();

// Repository 里的方法和 DbContext 方法几乎一一对应
// 这时候 Repository 成了"无用层"
```

### 3. 不适合复杂查询

```csharp
// ❌ Repository 难以封装复杂查询
// 查询条件动态组合、联表、聚合等
public interface IDeviceRepository
{
    // 要么暴露很少的方法，查询能力受限
    Task<IEnumerable<Device>> GetAllAsync();

    // 要么暴露很多方法，变成贫血接口
    Task<IEnumerable<Device>> GetByStatusAsync(DeviceStatus status);
    Task<IEnumerable<Device>> GetByTypeAsync(string type);
    Task<IEnumerable<Device>> GetByStatusAndTypeAsync(DeviceStatus status, string type);
    Task<IEnumerable<Device>> SearchAsync(string keyword);
    // ... 无限膨胀
}
```

---

## 什么时候不需要 Repository？

### 简单 CRUD 应用

```csharp
// 一个简单的 Todo API，数据访问就一行
[ApiController]
public class TodosController : ControllerBase
{
    private readonly AppDbContext _context;

    [HttpGet]
    public async Task<IActionResult> GetAll()
        => Ok(await _context.Todos.ToListAsync());

    [HttpPost]
    public async Task<IActionResult> Create(Todo todo)
    {
        _context.Todos.Add(todo);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetAll), todo);
    }
}

// 这种情况 Repository 是过度设计
// 直接用 DbContext 就行
```

### 复杂查询主导的应用

```csharp
// 报表系统：大量复杂动态查询
// Repository 无法优雅处理
var query = _context.TelemetryReadings
    .Where(r => r.DeviceId == deviceId)
    .Where(r => r.Timestamp >= startDate)
    .Where(r => r.Timestamp <= endDate)
    .Where(r => query.StatusFilter.Contains(r.Status))
    .GroupBy(r => new { r.Status, Date = r.Timestamp.Date })
    .Select(g => new ReportItem
    {
        Status = g.Key.Status,
        Date = g.Key.Date,
        Count = g.Count(),
        AvgValue = g.Avg(r => r.Value)
    });

// 用 Specification 模式或 Query Object 模式更合适
```

### Micro ORM（Dapper）

```csharp
// Dapper 本身就是"轻量级 Repository"
var devices = await _connection.QueryAsync<Device>(
    "SELECT * FROM devices WHERE status = @Status",
    new { Status = "Online" });

// 已经有了数据访问封装，额外再加 Repository 层意义不大
```

---

## 什么时候需要 Repository？

### 1. 中大型项目，多个 Service 共用数据

```
多个 Service 要访问 Device 数据
   ↓
用一个 Repository 统一管理
   ↓
避免每个 Service 各自写 DbContext 查询逻辑
```

### 2. 需要 Mock 测试

```
Service 的业务逻辑要单独测试
   ↓
需要 Mock 掉数据访问层
   ↓
Repository 接口是 Mock 的基础
```

### 3. 以后可能换数据源

```
现在用 PostgreSQL
以后可能换 MySQL / MongoDB / 内存数据库
   ↓
Repository 接口隔离变化
```

### 4. 数据访问逻辑复杂

```csharp
// 批量操作、事务、领域事件等逻辑
public async Task ImportDevicesAsync(IEnumerable<Device> devices)
{
    await using var transaction = await _context.Database.BeginTransactionAsync();
    try
    {
        foreach (var device in devices)
        {
            await _context.Devices.AddAsync(device);
        }
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
    }
    catch
    {
        await transaction.RollbackAsync();
        throw;
    }
}

// 这类复杂逻辑封装在 Repository 里，Service 不需要知道
```

---

## Specification 模式（高级）

对于复杂查询，用 Specification 模式替代 Repository 的多方法：

```csharp
// 定义查询规格
public class DeviceByStatusSpec : Specification<Device>
{
    public DeviceByStatusSpec(DeviceStatus status)
    {
        Query.Where(d => d.Status == status);
    }
}

public class DeviceByStatusAndTypeSpec : Specification<Device>
{
    public DeviceByStatusAndTypeSpec(DeviceStatus status, string type)
    {
        Query.Where(d => d.Status == status && d.Type == type);
    }
}

// Repository 只需一个方法
public interface IDeviceRepository
{
    Task<IEnumerable<Device>> FindAsync(ISpecification<Device> spec);
}

// 使用
var spec = new DeviceByStatusAndTypeSpec(DeviceStatus.Online, "钻机");
var devices = await _repository.FindAsync(spec);
```

---

## 实用主义：什么时候加 Repository？

### 建议：先用直接的方式

```
阶段 1：单个 Controller，数据访问简单
   ↓
直接用 DbContext，不加 Repository
   ↓
当出现以下情况时，再重构出 Repository：
   • 测试需要 mock 数据访问层
   • 多个 Controller 共用查询逻辑
   • 数据访问逻辑变复杂（事务、批量操作）
```

### 阶段 1：直接用 DbContext（现在用这个）

```csharp
// MineWatch.Api/Controllers/DevicesController.cs
[ApiController]
[Route("api/[controller]")]
public class DevicesController : ControllerBase
{
    private readonly MineWatchDbContext _context;

    public DevicesController(MineWatchDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
        => Ok(await _context.Devices.ToListAsync());

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var device = await _context.Devices.FindAsync(id);
        return device == null ? NotFound() : Ok(device);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Device device)
    {
        device.Id = Guid.NewGuid();
        _context.Devices.Add(device);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = device.Id }, device);
    }

    // ... Update / Delete 类似
}
```

**什么时候这样做就够了**：
- 只有一个 Controller
- 数据访问逻辑简单（CRUD 一行搞定）
- 不需要单元测试（或用集成测试）
- 项目还在快速迭代期

### 阶段 2：当出现这些问题时，再重构

```csharp
// 问题 1：多个 Controller 共用查询逻辑
// DevicesController 和 AlertsController 都要查 Device
// 重复代码 → 该抽 Repository 了

// 问题 2：测试需要 mock
// ServiceLayer 的单元测试不想连数据库
// → 需要 Repository 接口来 mock

// 问题 3：数据访问变复杂
// 批量导入、事务操作、Outbox Pattern
// → 需要专门的 Repository 封装
```

### 重构信号检查清单

| 信号 | 说明 | 该不该加 Repository |
|------|------|---------------------|
| 数据访问就几行 | CRUD 很直接 | ❌ 不需要 |
| 只有一个 Controller | 没有代码复用需求 | ❌ 不需要 |
| 需要单元测试 | Service 层逻辑要 mock | ✅ 需要 |
| 多个 Controller 共用查询 | 重复代码 | ✅ 需要 |
| 复杂事务 / 批量操作 | 逻辑复杂需封装 | ✅ 需要 |
| 可能换数据源 | PostgreSQL → MySQL | ✅ 需要（接口隔离） |

### 什么时候真的不需要 Repository？

```csharp
// 简单 CRUD，数据访问就一行
public async Task<IActionResult> GetAll()
    => Ok(await _context.Devices.ToListAsync());

// 这时候 Repository 反而是负担：
// 多了一个接口、一个实现类、一个 DI 注册
// 但几乎没有额外价值
```

### 什么时候真的需要 Repository？

```csharp
// 场景 1：单元测试需要 mock
public class DeviceService
{
    private readonly IDeviceRepository _repository;  // 接口

    public DeviceService(IDeviceRepository repository)
    {
        _repository = repository;
    }

    public async Task DoSomething()
    {
        // 测试时可以直接 mock 这个
        var devices = await _repository.GetAllAsync();
    }
}

// 场景 2：数据访问逻辑复杂
public class DeviceRepository : IDeviceRepository
{
    public async Task ImportBulkAsync(IEnumerable<Device> devices)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            await _context.Devices.AddRangeAsync(devices);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}

// 场景 3：多个 Service 共用
// DeviceService、AlertService 都要查 Device
// → 一个 Repository，统一管理
```

---

## 常见数据访问方式对比

### 三种主流实现

| 特性 | EF Core | Dapper | ADO.NET |
|------|---------|--------|---------|
| **抽象程度** | 高（全 ORM） | 低（Micro ORM） | 无（手写 SQL） |
| **代码量** | 少 | 中 | 多 |
| **灵活性** | 低（有约束） | 高 | 最高 |
| **性能** | 较慢 | 快 | 最快 |
| **复杂查询** | 强（LINQ） | 弱（手写 SQL） | 强（手写 SQL） |
| **学习曲线** | 中 | 低 | 高 |
| **维护成本** | 低 | 中 | 高 |
| **适用场景** | 业务逻辑复杂 | 简单 CRUD / 高性能 | 极致性能 / 复杂存储过程 |

### EF Core vs Dapper vs ADO.NET

```
性能（从高到低）：
ADO.NET > Dapper >> EF Core

开发效率（从高到低）：
EF Core > Dapper >> ADO.NET

灵活性（从高到低）：
ADO.NET > Dapper > EF Core
```

### EF Core（我们现在用的）

```csharp
// 优点：代码少，LINQ 查询，类型安全，迁移方便
var devices = await _context.Devices
    .Where(d => d.Status == DeviceStatus.Online)
    .Include(d => d.TelemetryReadings)
    .OrderBy(d => d.Name)
    .ToListAsync();

// 缺点：有性能开销，复杂查询不如 SQL 直接
// 生成 SQL 示例：
// SELECT d.* FROM Devices d WHERE d.Status = 'Online' ORDER BY d.Name
```

### Dapper（Micro ORM）

```csharp
// 优点：性能接近原生 ADO.NET，SQL 可控
var devices = await _connection.QueryAsync<Device>(
    @"SELECT * FROM devices
      WHERE status = @Status
      ORDER BY name",
    new { Status = "Online" });

// 缺点：手写 SQL，类型安全弱，无迁移管理
```

### ADO.NET（手写 SQL）

```csharp
// 优点：完全控制，性能最佳
// 缺点：代码繁琐，手动映射，容易出错
using var cmd = _connection.CreateCommand();
cmd.CommandText = "SELECT * FROM devices WHERE status = @Status";
cmd.Parameters.AddWithValue("@Status", "Online");

using var reader = await cmd.ExecuteReaderAsync();
var devices = new List<Device>();
while (await reader.ReadAsync())
{
    devices.Add(new Device
    {
        Id = reader.GetGuid(0),
        Name = reader.GetString(1),
        // ...手动映射每个字段
    });
}
```

### 本项目为什么选 EF Core

| 考量 | 选择 EF Core 的原因 |
|------|---------------------|
| 开发效率 | Sprint 1 要快速出活，EF Core 代码量少 |
| 团队经验 | 学习曲线适中，有完整文档 |
| 后续 DDD | EF Core 支持 Fluent API、变更追踪、迁移 |
| Sprint 2 性能 | 先用 EF Core 出功能，Sprint 2 用 `Channel<T>` 优化批量写入 |
| Sprint 3 微服务 | 微服务间用 Outbox Pattern，EF Core 支持好 |

### 什么时候从 EF Core 迁移到 Dapper

```csharp
// 性能瓶颈分析后发现某几个查询慢
// 1. 用 EF Core 查，发现 100ms
// 2. 用 SQL 直接查，10ms
// 3. 差距明显，用 Dapper 优化这个具体查询

// 或者：
// 某个报表查询极其复杂，LINQ 无法优雅表达
// 直接手写 SQL 更清晰

// 做法：Repository 模式保留，只把特定方法换成 Dapper
public class EfCoreDeviceRepository : IDeviceRepository
{
    private readonly MineWatchDbContext _context;

    // 大部分方法用 EF Core
    public async Task<IEnumerable<Device>> GetAllAsync()
        => await _context.Devices.ToListAsync();

    // 性能关键查询用 Dapper
    public async Task<IEnumerable<Device>> GetHighPerformingDevicesAsync()
    {
        var sql = @"SELECT * FROM devices
                    WHERE status = 'Online'
                    AND last_reading_time > @Threshold
                    ORDER BY efficiency DESC
                    LIMIT 100";

        return await _context.Database
            .GetDbConnection()
            .QueryAsync<Device>(sql, new { Threshold = DateTime.UtcNow.AddHours(-1) });
    }
}
```

---

## 总结

| 场景 | 推荐方案 |
|------|----------|
| 快速原型 / Sprint 1 | EF Core |
| 业务逻辑复杂、需迁移、需测试 | EF Core |
| 高并发简单查询（IoT 数据写入） | EF Core + 批量优化，或 Dapper |
| 复杂报表 / 存储过程 | Dapper 或 ADO.NET |
| 极致性能核心模块 | ADO.NET + Dapper 混合 |
| Micro ORM（Dapper） | 本身是轻量 Repository，直接用就行 |

---

*最后更新：2026 年 4 月*
