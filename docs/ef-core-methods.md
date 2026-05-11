# EF Core 常用方法详解

## 核心概念：延迟执行

EF Core 的大多数操作都遵循**延迟执行（Deferred Execution）**原则：

| 类型 | 说明 |
|------|------|
| **查询组合** | LINQ 方法只是组装 SQL，**不执行** |
| **实际执行** | 遍历结果（`foreach`、`ToList()`、`First()`）或显式调用时，才真正查数据库 |
| **变更追踪** | `Add/Update/Remove` 只是标记为已更改，**不执行** SQL |
| **实际保存** | `SaveChanges()` 时才生成并执行 SQL |

---

## 一、查询方法

### 1.1 组合类方法（不执行）

这些方法只是**构建查询表达式**，不会触发数据库访问：

```csharp
// 只是组装 SQL，不查数据库
var query = context.Devices
    .Where(d => d.Status == DeviceStatus.Online)   // 组合
    .OrderBy(d => d.Name)                           // 组合
    .Select(d => new { d.Name, d.Status });          // 组合
// 此时还没有执行任何 SQL
```

| 方法 | 作用 | 返回类型 |
|------|------|----------|
| `Where()` | 添加过滤条件 | `IQueryable<T>` |
| `OrderBy()` / `OrderByDescending()` | 排序 | `IQueryable<T>` |
| `ThenBy()` / `ThenByDescending()` | 多字段排序 | `IQueryable<T>` |
| `Select()` | 投影（选择列） | `IQueryable<TResult>` |
| `SelectMany()` | 展开嵌套集合 | `IQueryable<TResult>` |
| `Join()` / `GroupJoin()` | 连接查询 | `IQueryable<TResult>` |
| `GroupBy()` | 分组 | `IQueryable<IGrouping<TKey, TElement>>` |
| `Distinct()` | 去重 | `IQueryable<T>` |
| `Skip()` / `Take()` | 分页 | `IQueryable<T>` |
| `Include()` / `ThenInclude()` | 预加载关联数据 | `IQueryable<T>` |
| `AsNoTracking()` | 禁用变更追踪 | `IQueryable<T>` |
| `IgnoreQueryFilters()` | 忽略全局过滤 | `IQueryable<T>` |

#### 执行点：什么时候真正查数据库？

```csharp
// 遍历时执行
foreach (var device in query) { }           // 第一次遍历时执行

// ToList() 时执行
var list = query.ToList();                   // 立即执行

// First/Single 方法时执行
var first = query.First();                   // 立即执行
var single = query.SingleOrDefault();        // 立即执行

// Count/Any/All 聚合时执行
var count = query.Count();                   // 立即执行
var exists = query.Any();                    // 立即执行

// ToDictionary/ToArray 时执行
var dict = query.ToDictionary(d => d.Id);   // 立即执行
```

#### 实际例子

```csharp
// 例1：组合查询，延迟执行
var onlineDevices = context.Devices
    .Where(d => d.Status == DeviceStatus.Online)
    .Where(d => d.Name.Contains("Truck"))
    .OrderByDescending(d => d.CreatedAt)
    .Select(d => new {
        d.Id,
        d.Name,
        StatusText = d.Status.ToString()
    });

// 此时 SQL 还没生成
Console.WriteLine(onlineDevices.Expression);  // 可以看到表达式树

// 遍历时一次性执行（参数化查询，防止 SQL 注入）
// SELECT id, name, status FROM devices WHERE status = 1 AND name LIKE '%Truck%' ORDER BY created_at DESC
foreach (var device in onlineDevices)
{
    Console.WriteLine($"{device.Id}: {device.Name}");
}

// 例2：分页查询
var page = 2;
var pageSize = 10;
var pagedDevices = context.Devices
    .OrderBy(d => d.Name)
    .Skip((page - 1) * pageSize)
    .Take(pageSize)
    .ToList();  // 执行 SQL: SELECT ... LIMIT 10 OFFSET 10
```

### 1.2 立即执行类方法

这些方法会**立即触发数据库访问**：

```csharp
// 立即执行，返回集合
ToList()              // SELECT * FROM ... => List<T>
ToListAsync()         // 异步版本

// 立即执行，返回单个元素
First()                // SELECT TOP 1 * FROM ...
FirstOrDefault()      // SELECT TOP 1 * FROM ... (无则返回 default)
Single()               // 期望恰好一个，否则抛异常
SingleOrDefault()     // 期望最多一个，无则返回 default

// 聚合立即执行
Count()                // SELECT COUNT(*)
LongCount()           // SELECT COUNT_BIG(*)
Any()                  // SELECT EXISTS(...)
All()                  // SELECT WHERE NOT EXISTS (NOT ...) (语义略有不同)
Sum() / Average()     // SELECT SUM(...) / AVG(...)
Min() / Max()         // SELECT MIN(...) / MAX(...)
```

#### 实际例子

```csharp
// 例1：判断存在性（推荐 Any() 而不是 Count() > 0）
bool hasOnline = context.Devices.Any(d => d.Status == DeviceStatus.Online);
// SQL: SELECT EXISTS(SELECT 1 FROM devices WHERE status = 1)

// 例2：取第一个或默认值
var device = context.Devices
    .Where(d => d.Id == id)
    .FirstOrDefault();

// 例3：聚合查询
var statusCounts = context.Devices
    .GroupBy(d => d.Status)
    .Select(g => new {
        Status = g.Key,
        Count = g.Count()
    })
    .ToList();
// SQL: SELECT status, COUNT(*) FROM devices GROUP BY status
```

---

## 二、变更追踪方法

### 2.1 追踪状态说明

EF Core 中的实体有四种状态：

| 状态 | 说明 | SaveChanges 行为 |
|------|------|------------------|
| `Detached` | 未被追踪 | 无 |
| `Unchanged` | 已被追踪，未修改 | 无 |
| `Modified` | 已被追踪，属性有修改 | UPDATE |
| `Added` | 标记为新增 | INSERT |
| `Deleted` | 标记为删除 | DELETE |

### 2.2 添加实体

```csharp
// 例1：添加单个实体
var device = new Device
{
    Id = Guid.NewGuid(),
    Name = "Truck-001",
    Status = DeviceStatus.Online,
    CreatedAt = DateTime.UtcNow
};

context.Devices.Add(device);
// 状态变为 Added，但不执行 SQL
// Entry(device).State => EntityState.Added

await context.SaveChangesAsync();
// 此时执行: INSERT INTO devices (id, name, status, created_at) VALUES (...)

// 例2：添加多个
var devices = new List<Device>
{
    new Device { Id = Guid.NewGuid(), Name = "Truck-002" },
    new Device { Id = Guid.NewGuid(), Name = "Truck-003" }
};

context.Devices.AddRange(devices);
// 状态都变为 Added

await context.SaveChangesAsync();
// 一次 SQL 批量插入

// 例3：DbSet.Add vs DbContext.Add
// 两者效果一样，DbSet 是泛型版本，更常用
context.Devices.Add(device);
context.Add(device);  // 非泛型版，编译时不知道类型

// 例4：添加后立即获取自增 ID
context.Devices.Add(device);
await context.SaveChangesAsync();
Console.WriteLine(device.Id);  // 如果是自增列，此时已有值
```

### 2.3 修改实体

```csharp
// 例1：查询后修改（推荐 - 自动追踪）
var device = await context.Devices.FindAsync(deviceId);
device.Status = DeviceStatus.Offline;
device.UpdatedAt = DateTime.UtcNow;
// 状态变为 Modified，追踪所有属性变化

await context.SaveChangesAsync();
// 执行: UPDATE devices SET status = @p0, updated_at = @p1 WHERE id = @p2

// 例2：只修改特定字段（更高效）
var device = await context.Devices.FindAsync(deviceId);
context.Entry(device).Property(d => d.Status).IsModified = true;
device.Status = DeviceStatus.Offline;

await context.SaveChangesAsync();
// 只更新 status 字段

// 例3：批量修改（不查询直接改）
await context.Devices
    .Where(d => d.Status == DeviceStatus.Online)
    .ExecuteUpdateAsync(s => s
        .SetProperty(d => d.Status, DeviceStatus.Offline)
        .SetProperty(d => d.UpdatedAt, DateTime.UtcNow));
// 直接执行: UPDATE devices SET status = 1, updated_at = ... WHERE status = 0
// 注意：这是直接 SQL 执行，不经过变更追踪

// 例4：Attach 已存在的实体（不查数据库）
var device = new Device
{
    Id = existingId,
    Name = "Updated Name",
    Status = DeviceStatus.Offline
};

context.Devices.Attach(device);
// 状态变为 Unchanged，需要手动标记修改
context.Entry(device).State = EntityState.Modified;

await context.SaveChangesAsync();
// 执行 UPDATE（所有属性都会被更新）
```

### 2.4 删除实体

```csharp
// 例1：查询后删除（常用）
var device = await context.Devices.FindAsync(deviceId);
context.Devices.Remove(device);
// 状态变为 Deleted

await context.SaveChangesAsync();
// 执行: DELETE FROM devices WHERE id = @p0

// 例2：直接删除（不查询）
await context.Devices
    .Where(d => d.Id == deviceId)
    .ExecuteDeleteAsync();
// 直接执行: DELETE FROM devices WHERE id = @p0

// 例3：标记删除（软删除模式）
var device = await context.Devices.FindAsync(deviceId);
device.IsDeleted = true;
device.DeletedAt = DateTime.UtcNow;

await context.SaveChangesAsync();
// 执行 UPDATE 而不是 DELETE（实体还在，只是标记了删除状态）
```

---

## 三、Find vs 其他查询方法

### 3.1 Find() 的特点

```csharp
// Find() 先查追踪器，再查数据库，最后查缓存
var device = await context.Devices.FindAsync(deviceId);
```

**查找顺序：**
1. 本地追踪器（DbContext 内存中已加载的实体）
2. 数据库查询
3. 写入本地缓存供后续使用

### 3.2 Find() vs 其他查询

| 对比 | `Find()` | `FirstOrDefault()` | `SingleOrDefault()` |
|------|----------|-------------------|---------------------|
| 查本地缓存 | ✅ | ❌ | ❌ |
| 查询缓存 | ✅ | ❌ | ❌ |
| 异常安全 | ✅（不会抛） | ✅ | ❌（超过1个抛异常） |
| 复合主键支持 | ✅ | ❌ | ❌ |
| 可组合 LINQ | ❌ | ✅ | ✅ |

```csharp
// Find 支持复合主键
var entity = await context.Set<Entity>()
    .FindAsync(keyPart1, keyPart2);

// First 更灵活，可组合 Where/Select
var name = await context.Devices
    .Where(d => d.Id == id)
    .Select(d => d.Name)
    .FirstOrDefaultAsync();
```

---

## 四、Include 和预加载

### 4.1 Include 单层关联

```csharp
// 预加载 TelemetryReadings
var devices = await context.Devices
    .Include(d => d.TelemetryReadings)
    .ToListAsync();

// 生成的 SQL 是 JOIN
// SELECT d.*, t.* FROM devices d LEFT JOIN telemetry_readings t ON d.id = t.device_id
```

### 4.2 ThenInclude 多层嵌套

```csharp
// Device -> TelemetryReading -> Device（闭环）
var readings = await context.TelemetryReadings
    .Include(t => t.Device)
        .ThenInclude(d => d.TelemetryReadings)  // 再次包含
    .ToListAsync();

// 加载集合类型的子项
var devices = await context.Devices
    .Include(d => d.TelemetryReadings)
        .ThenInclude(t => t.Location)
    .ToListAsync();
```

### 4.3 批量 Include

```csharp
// 多个关联
var devices = await context.Devices
    .Include(d => d.TelemetryReadings)
    .Include(d => d.Alerts)
    .ToListAsync();
```

### 4.4 条件 Include

```csharp
// 只 Include 满足条件的子项（EF Core 5.0+）
var devices = await context.Devices
    .Include(d => d.TelemetryReadings.Where(t => t.Timestamp > DateTime.UtcNow.AddHours(-1)))
    .ToListAsync();
```

### 4.5 预加载 vs 显式加载 vs 懒加载

| 方式 | 触发时机 | SQL 模式 | 说明 |
|------|----------|----------|------|
| **Include** | 查询时一起加载 | JOIN 或批量 SELECT | 推荐使用 |
| **Explicit Loading** | 显式调用 `Load()` | 额外 SELECT | 需要时加载 |
| **Lazy Loading** | 访问导航属性时 | N+1 查询 | 不推荐，性能差 |

```csharp
// 显式加载
var device = await context.Devices.FindAsync(id);
await context.Entry(device).Collection(d => d.TelemetryReadings).LoadAsync();
// 额外执行一条 SELECT

// 懒加载（需要代理）
var readings = device.TelemetryReadings;  // 访问时自动加载，可能导致 N+1
```

---

## 五、AsNoTracking 和 AsNoTrackingWithIdentityResolution

### 5.1 AsNoTracking()

```csharp
// 只读查询，不追踪实体，性能更好
var devices = await context.Devices
    .AsNoTracking()
    .Where(d => d.Status == DeviceStatus.Online)
    .ToListAsync();

// 实体状态都是 Detached，无法修改和保存
// 适用于：列表展示、数据导出等只读场景
```

### 5.2 AsNoTrackingWithIdentityResolution()

```csharp
// 不追踪，但会解析重复实体（同一 ID 共享引用）
var result = await context.Devices
    .AsNoTrackingWithIdentityResolution()
    .Include(d => d.TelemetryReadings)
    .ToListAsync();

// 如果多个 Device 引用同一个 Location，只会创建一份 Location 实例
// 适用于：不追踪但需要去重的 Include 查询
```

### 5.3 对比

| 模式 | 追踪 | 重复解析 | 性能 |
|------|------|----------|------|
| 默认追踪 | ✅ | ✅ | 较慢 |
| `AsNoTracking()` | ❌ | ❌ | 最快 |
| `AsNoTrackingWithIdentityResolution()` | ❌ | ✅ | 中等 |

---

## 六、SaveChanges 和批量操作

### 6.1 SaveChanges()

```csharp
// 单次保存所有变更（在一个事务中）
await context.SaveChangesAsync();

// 变更检测：自动检测所有追踪实体的变化
// 生成对应的 INSERT/UPDATE/DELETE SQL
// 在一个事务中执行（要么全成功，要么全回滚）
```

### 6.2 SaveChangesAsync()

```csharp
// 异步版本
await context.SaveChangesAsync();

// 可以指定 CancellationToken
await context.SaveChangesAsync(cancellationToken);
```

### 6.3 批量操作（ExecuteUpdate/ExecuteDelete）

```csharp
// 批量更新（直接 SQL，不经过变更追踪）
await context.Devices
    .Where(d => d.Status == DeviceStatus.Online)
    .ExecuteUpdateAsync(s => s
        .SetProperty(d => d.Status, DeviceStatus.Maintenance));

// 批量删除（直接 SQL）
await context.Devices
    .Where(d => d.IsDeleted && d.DeletedAt < DateTime.UtcNow.AddDays(-30))
    .ExecuteDeleteAsync();

// 区别：
// - ExecuteUpdate/ExecuteDelete 直接执行 SQL，不触发 SaveChanges
// - 不经过变更追踪器，不加载实体到内存
// - 适用于大批量操作，性能更好
```

### 6.4 批量添加（Bulk Extensions）

```csharp
// EF Core 原生不支持真正的批量插入
// 需要使用 EF Core Extensions 库（如 Z.EntityFramework.Extensions）

// 原生方式：AddRange + SaveChanges（还是会逐行插入）
var items = Enumerable.Range(1, 1000)
    .Select(i => new Device { Name = $"Device-{i}" })
    .ToList();

context.Devices.AddRange(items);
await context.SaveChangesAsync();  // 内部还是逐行 INSERT，但通过批次优化

// 真正的批量插入（需要第三方库）
// context.BulkInsert(devices);
```

---

## 七、异步方法

所有同步方法都有对应的异步版本：

| 同步 | 异步 | 返回 |
|------|------|------|
| `ToList()` | `ToListAsync()` | `Task<List<T>>` |
| `FirstOrDefault()` | `FirstOrDefaultAsync()` | `Task<T?>` |
| `Find()` | `FindAsync()` | `Task<T>` |
| `SaveChanges()` | `SaveChangesAsync()` | `Task<int>` |
| `Load()` | `LoadAsync()` | `Task` |

```csharp
// 推荐在 ASP.NET Core 中使用异步方法
public async Task<ActionResult<List<Device>>> GetDevices()
{
    var devices = await context.Devices
        .AsNoTracking()
        .ToListAsync();

    return Ok(devices);
}
```

---

## 八、执行时机总结

### 8.1 方法执行时机速查表

| 方法 | 是否执行 SQL | 实际执行时机 |
|------|-------------|-------------|
| `Where()` | ❌ | 枚举时 |
| `Select()` | ❌ | 枚举时 |
| `OrderBy()` | ❌ | 枚举时 |
| `Include()` | ❌ | 枚举时 |
| `ToList()` | ✅ | 调用时 |
| `First()` / `FirstOrDefault()` | ✅ | 调用时 |
| `Any()` / `Count()` | ✅ | 调用时 |
| `Find()` | ✅（先缓存后DB） | 调用时 |
| `Add()` | ❌（标记状态） | `SaveChanges()` 时 |
| `Remove()` | ❌（标记状态） | `SaveChanges()` 时 |
| `SaveChanges()` | ✅ | 调用时 |
| `ExecuteUpdate()` | ✅ | 调用时（直接 SQL） |
| `ExecuteDelete()` | ✅ | 调用时（直接 SQL） |

### 8.2 变更追踪流程

```
1. 查询：context.Devices.Where(...)      →  返回 IQueryable，不执行
2. 枚举：.ToList() / foreach            →  生成并执行 SQL，结果进入变更追踪器
3. 修改：device.Name = "new"            →  实体标记为 Modified
4. 保存：SaveChangesAsync()             →  生成并执行 UPDATE SQL
```

### 8.3 查询执行流程

```
1. 构建：IQueryable<T>                  →  表达式树，只组装
2. 翻译：EF Core 表达式翻译器            →  生成参数化 SQL
3. 执行：数据库                         →  返回结果
4. 追踪：结果进入 Change Tracker         →  实体状态为 Unchanged
```

---

*最后更新：2026 年 5 月*
