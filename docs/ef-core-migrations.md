# EF Core 数据库迁移指南

## 概述

本项目使用 Entity Framework Core (EF Core) 进行数据库版本管理，通过"迁移（Migrations）"机制跟踪数据库结构变更。

## 核心概念

### 什么是迁移？

迁移是一个基于代码的数据库变更记录，包含两个文件：
- `[Timestamp]_MigrationName.cs` - 向上操作（创建表/列）
- `[Timestamp]_MigrationName.Designer.cs` - EF Core 元数据

每次修改数据模型后，生成新迁移文件，再应用到数据库，实现数据库结构与代码模型同步。

### 设计时工厂（IDesignTimeDbContextFactory）

**为什么需要它？**

```
运行时：Program.cs → DI 容器 → DbContext（由 ASP.NET Core 管理生命周期）
设计时：dotnet ef 命令 → 纯设计时上下文，没有 DI 容器
```

`dotnet ef migrations` 和 `dotnet ef database update` 在设计时执行，不经过 `Program.cs`，因此没有 DI 容器来注入 `DbContext`。

`IDesignTimeDbContextFactory` 就是在没有 DI 容器的设计时，手动创建 `DbContext` 的方式：

```csharp
public class MineWatchDbContextFactory : IDesignTimeDbContextFactory<MineWatchDbContext>
{
    public MineWatchDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<MineWatchDbContext>();
        // 硬编码连接字符串，因为设计时无法访问 appsettings.json
        optionsBuilder.UseNpgsql("Host=localhost;Database=minewatch;Username=postgres;Password=postgres");
        return new MineWatchDbContext(optionsBuilder.Options);
    }
}
```

**设计时 vs 运行时连接字符串**

| 环境 | 连接字符串来源 | 说明 |
|------|--------------|------|
| 运行时 | `appsettings.Development.json` | 通过 `builder.Configuration` 读取 |
| 设计时 | `IDesignTimeDbContextFactory` 硬编码 | 工具执行时无法访问 `appsettings.json` |

## 迁移流程

### 1. 安装 EF Core 工具（仅首次）

```bash
dotnet tool install --global dotnet-ef
```

### 2. 添加迁移

当 `MineWatchDbContext` 中的模型定义变更后（新增实体、修改属性等），执行：

```bash
dotnet ef migrations add MigrationName --project src/MineWatch.Infrastructure/MineWatch.Infrastructure.csproj
```

生成的文件：
```
src/MineWatch.Infrastructure/Migrations/
├── 20260430055700_InitialCreate.cs
├── 20260430055700_InitialCreate.Designer.cs
└── MineWatchDbContextModelSnapshot.cs
```

### 3. 查看迁移内容

生成的 `.cs` 文件包含 `Up()` 和 `Down()` 方法：

```csharp
public partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Devices",
            columns: table => new {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                Status = table.Column<string>(type: "text", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table => {
                table.PrimaryKey("PK_Devices", x => x.Id);
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "Devices");
    }
}
```

### 4. 应用迁移到数据库

```bash
dotnet ef database update --project src/MineWatch.Infrastructure/MineWatch.Infrastructure.csproj
```

执行过程：
1. 检查 `__EFMigrationsHistory` 表
2. 按顺序应用所有未应用的迁移
3. 记录已应用的迁移

### 5. 查看迁移状态

```bash
dotnet ef migrations list --project src/MineWatch.Infrastructure/MineWatch.Infrastructure.csproj
```

### 6. 回滚迁移

移除最近一次迁移（保留数据库变更用于回滚）：

```bash
dotnet ef migrations remove --project src/MineWatch.Infrastructure/MineWatch.Infrastructure.csproj
```

## EF Core 管理工具完整命令

| 命令 | 作用 |
|------|------|
| `dotnet ef migrations add Name` | 生成新迁移文件 |
| `dotnet ef migrations list` | 查看所有迁移及状态 |
| `dotnet ef migrations remove` | 移除最新未应用的迁移 |
| `dotnet ef database update` | 应用所有待执行的迁移 |
| `dotnet ef database update Name` | 回滚到指定迁移 |
| `dotnet ef dbcontext info` | 查看 DbContext 配置信息 |
| `dotnet ef dbcontext list` | 列出所有 DbContext |
| `dotnet ef dbcontext scaffold` | 根据现有数据库生成代码模型 |

## 工具链概览

```
┌─────────────────────────────────────────────────────────┐
│                     dotnet ef                           │
│  (命令行工具，管理迁移和数据库操作)                        │
└─────────────────┬───────────────────────────────────────┘
                  │
                  │ --project 指向 MineWatch.Infrastructure
                  ▼
┌─────────────────────────────────────────────────────────┐
│              IDesignTimeDbContextFactory                │
│  (实现此接口以便设计时创建 DbContext)                     │
└─────────────────┬───────────────────────────────────────┘
                  │
                  │ DbContextOptions
                  ▼
┌─────────────────────────────────────────────────────────┐
│               MineWatchDbContext                         │
│  (继承 DbContext，配置实体和数据库映射)                    │
└─────────────────┬───────────────────────────────────────┘
                  │
                  │ OnModelCreating 配置
                  ▼
┌─────────────────────────────────────────────────────────┐
│                   迁移文件                               │
│  [Timestamp]_MigrationName.cs                            │
│  (Up() 应用变更，Down() 回滚变更)                        │
└─────────────────┬───────────────────────────────────────┘
                  │
                  │ dotnet ef database update
                  ▼
┌─────────────────────────────────────────────────────────┐
│                PostgreSQL 数据库                         │
│  __EFMigrationsHistory 表（记录已应用的迁移）             │
└─────────────────────────────────────────────────────────┘
```

## 项目中的相关文件

```
src/MineWatch.Infrastructure/
├── Data/
│   ├── MineWatchDbContext.cs          # DbContext 定义
│   ├── MineWatchDbContextFactory.cs  # 设计时工厂（实现 IDesignTimeDbContextFactory）
│   └── MineWatchDbContextModelSnapshot.cs  # 当前模型快照（自动生成）
├── Entities/
│   └── Device.cs                      # Device 实体
└── Migrations/
    └── 20260430055700_InitialCreate.cs  # 初始迁移
```

## 迁移内部流程详解

### 迁移执行三阶段

```
┌─────────────────────────────────────────────────────────────┐
│ 1. 模型对比阶段（Model Diff）                                 │
│    EF Core 对比：Current Model vs Last Snapshot             │
│    输出：需要执行的变更操作列表                               │
└───────────────────────────┬─────────────────────────────────┘
                            ▼
┌─────────────────────────────────────────────────────────────┐
│ 2. 迁移生成阶段（Migration Generation）                      │
│    根据变更操作生成 Up() 和 Down() 代码                      │
│    输出：.cs 迁移文件                                        │
└───────────────────────────┬─────────────────────────────────┘
                            ▼
┌─────────────────────────────────────────────────────────────┐
│ 3. 迁移应用阶段（Migration Apply）                          │
│    执行 Up() SQL，写入 __EFMigrationsHistory                 │
└─────────────────────────────────────────────────────────────┘
```

### 模型快照机制（Model Snapshot）

`MineWatchDbContextModelSnapshot.cs` 是当前模型的"快照"：

```csharp
modelBuilder.Entity<Device>(entity =>
{
    entity.HasKey(e => e.Id);
    entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
    // ... 反映创建快照时的完整模型
});
```

**工作原理：**
- 每次 `migrations add` 时，EF Core 生成新的迁移文件
- 同时更新快照文件，记录"此时此刻"的完整模型
- 下次 `migrations add` 时，对比**新模型 vs 快照**，生成差异迁移

### __EFMigrationsHistory 表

```sql
CREATE TABLE "__EFMigrationsHistory" (
    "MigrationId" VARCHAR(150) NOT NULL PRIMARY KEY,
    "ProductVersion" VARCHAR(32) NOT NULL
);

-- 示例内容：
-- MigrationId                          | ProductVersion
-- -------------------------------------|----------------
-- 20260430055700_InitialCreate         | 8.0.4
-- 20260501000000_AddTelemetry           | 8.0.4
```

---

## 多应用场景下的迁移策略

### 场景一：单解决方案多项目（推荐）

```
MineWatch.sln
├── src/
│   ├── MineWatch.Api/              # Web API
│   │   └── Program.cs
│   └── MineWatch.Infrastructure/   # 数据访问层（包含 DbContext 和 Migrations）
│       ├── Data/
│       │   └── MineWatchDbContext.cs
│       ├── Data/Migrations/        # 迁移文件放这里
│       └── MineWatch.Infrastructure.csproj
```

**迁移命令：**

```bash
# 迁移文件放在 Infrastructure 项目中
dotnet ef migrations add AddAlerts \
    --project src/MineWatch.Infrastructure/MineWatch.Infrastructure.csproj \
    --startup-project src/MineWatch.Api/MineWatch.Api.csproj

# --project：指定包含 DbContext 的项目（生成迁移文件的位置）
# --startup-project：指定启动项目（用于获取连接字符串、设计时工厂）
```

**为什么迁移放 Infrastructure？**

| 放置位置 | 优点 | 缺点 |
|----------|------|------|
| Infrastructure | 复用给多个 API；迁移与数据访问绑定 | API 项目需引用 Infrastructure |
| API 项目 | 直接访问 | 只有一个 API 时用；迁移与 API 耦合 |

### 场景二：多个 API 共用同一个数据库

```
MineWatch.sln
├── src/
│   ├── MineWatch.Api/              # API 1
│   ├── MineWatch.Api2/             # API 2
│   └── MineWatch.Infrastructure/   # 共享的数据层
│       └── Migrations/             # 唯一的迁移目录
```

**关键：统一迁移入口**

```csharp
// MineWatch.Infrastructure/Data/MineWatchDbContextFactory.cs
public class MineWatchDbContextFactory : IDesignTimeDbContextFactory<MineWatchDbContext>
{
    public MineWatchDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<MineWatchDbContext>();

        // 优先从命令行参数读取连接字符串
        var connectionString = args.Length > 0 ? args[0] : null;

        // 回退：从环境变量读取（API2 可能用不同的 env var）
        connectionString ??= Environment.GetEnvironmentVariable("MINEWATCH_CONNECTION");

        // 再回退：默认值
        connectionString ??= "Host=localhost;Database=minewatch;Username=postgres;Password=postgres";

        optionsBuilder.UseNpgsql(connectionString);
        return new MineWatchDbContext(optionsBuilder.Options);
    }
}
```

**各 API 执行迁移的方式：**

```bash
# API 1 执行迁移
dotnet ef database update \
    --project src/MineWatch.Infrastructure/MineWatch.Infrastructure.csproj \
    --startup-project src/MineWatch.Api/MineWatch.Api.csproj

# API 2 执行迁移（用不同的 startup project）
dotnet ef database update \
    --project src/MineWatch.Infrastructure/MineWatch.Infrastructure.csproj \
    --startup-project src/MineWatch.Api2/MineWatch.Api2.csproj
```

### 场景三：类和库分离（Domain 与 Persistence）

```
MineWatch.sln
├── src/
│   ├── MineWatch.Domain/           # 纯业务模型，无 EF Core
│   │   └── Entities/
│   │       └── Device.cs           # 业务实体（POCO）
│   ├── MineWatch.Infrastructure/   # EF Core 实现
│   │   ├── Data/
│   │   │   └── MineWatchDbContext.cs
│   │   └── Migrations/            # 迁移文件
│   └── MineWatch.Api/              # Web API
```

**Domain 层注意事项：**

```csharp
// Domain/Entities/Device.cs - 纯 POCO，无 EF Core 依赖
public class Device
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public DeviceStatus Status { get; set; }

    // 导航属性（但在 Domain 中只是普通引用，不带 virtual）
    // public virtual ICollection<TelemetryReading> TelemetryReadings { get; set; }
}
```

**这种分离适合：**
- Domain 层要被多个不同 ORM 使用
- Domain 层不能引用任何 ORM 库

---

## 常用迁移命令详解

### dotnet ef migrations add

```bash
# 基本语法
dotnet ef migrations add MigrationName [options]

# 示例
dotnet ef migrations add AddDeviceLocation \
    --project src/MineWatch.Infrastructure/MineWatch.Infrastructure.csproj \
    --startup-project src/MineWatch.Api/MineWatch.Api.csproj \
    --output-dir Data/Migrations       # 可选：自定义输出目录
```

**常用选项：**

| 选项 | 说明 |
|------|------|
| `--output-dir <path>` | 迁移文件输出目录 |
| `--namespace <ns>` | 迁移类的命名空间 |
| `--context <name>` | 指定 DbContext（多 Context 时需要） |

### dotnet ef database update

```bash
# 应用所有待执行迁移
dotnet ef database update

# 回滚到指定迁移（保留数据库内容，只回滚结构）
dotnet ef database update PreviousMigrationName

# 示例：回滚到最后一次迁移
dotnet ef database update 20260430055700_InitialCreate
```

**`database update` 内部执行流程：**

```
1. 读取 __EFMigrationsHistory 表
2. 获取当前数据库已应用的迁移列表
3. 对比 Migrations 目录中的迁移文件
4. 确定哪些迁移需要应用（按时间戳顺序）
5. 依次执行每个迁移的 Up() 方法
6. 每个 Up() 成功后，写入 __EFMigrationsHistory
```

### dotnet ef migrations remove

```bash
# 移除最新未应用的迁移（已应用的不能直接 remove）
dotnet ef migrations remove \
    --project src/MineWatch.Infrastructure/MineWatch.Infrastructure.csproj \
    --startup-project src/MineWatch.Api/MineWatch.Api.csproj
```

**前提条件：**
- 该迁移**未**执行过 `database update`
- 如果已执行，必须先 `database update` 回滚到上一个

### dotnet ef dbcontext scaffold

**根据现有数据库生成代码（逆向工程）：**

```bash
dotnet ef dbcontext scaffold \
    "Host=localhost;Database=minewatch;Username=postgres;Password=postgres" \
    Npgsql.EntityFrameworkCore.PostgreSQL \
    --project src/MineWatch.Infrastructure/MineWatch.Infrastructure.csproj \
    --output-dir Entities \
    --context-dir Data \
    --namespace MineWatch.Infrastructure.Entities \
    --context-namespace MineWatch.Infrastructure.Data
```

**常用选项：**

| 选项 | 说明 |
|------|------|
| `--tables <names>` | 只生成指定表 |
| `--schema <names>` | 只生成指定 schema |
| `--data-annotations` | 用 Data Annotations 而不是 Fluent API |
| `--force` | 覆盖已存在的文件 |
| `--no-onconfiguring` | 不生成 OnConfiguring |

### dotnet ef dbcontext info

```bash
# 查看 DbContext 的设计时信息（诊断用）
dotnet ef dbcontext info \
    --project src/MineWatch.Infrastructure/MineWatch.Infrastructure.csproj
```

**输出示例：**

```
Options:
  UseNpgsql(connectionString)

Entities:
  Device
    Id (Guid) PK
    Name (string)
    Status (DeviceStatus)
    CreatedAt (DateTime)
    UpdatedAt (DateTime?)
    TelemetryReadings (ICollection<TelemetryReading>) FK -> TelemetryReadings.DeviceId
```

---

## 实战场景

### 场景 A：添加新实体

```csharp
// 1. 定义实体
public class Alert
{
    public Guid Id { get; set; }
    public Guid DeviceId { get; set; }
    public string Message { get; set; }
    public AlertLevel Level { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsResolved { get; set; }

    // 导航属性
    public Device Device { get; set; }
}

// 2. DbContext 中添加 DbSet
public DbSet<Alert> Alerts => Set<Alert>();

// 3. 配置关系（Fluent API）
modelBuilder.Entity<Alert>(entity =>
{
    entity.HasKey(e => e.Id);
    entity.Property(e => e.Message).IsRequired().HasMaxLength(500);

    entity.HasOne(a => a.Device)
        .WithMany(d => d.Alerts)
        .HasForeignKey(a => a.DeviceId)
        .OnDelete(DeleteBehavior.Cascade);
});

// 4. 生成迁移
dotnet ef migrations add AddAlerts \
    --project src/MineWatch.Infrastructure/MineWatch.Infrastructure.csproj \
    --startup-project src/MineWatch.Api/MineWatch.Api.csproj

// 5. 应用迁移
dotnet ef database update \
    --project src/MineWatch.Infrastructure/MineWatch.Infrastructure.csproj \
    --startup-project src/MineWatch.Api/MineWatch.Api.csproj
```

### 场景 B：修改现有列

```csharp
// 1. 修改实体属性
public class Device
{
    // ...
    [MaxLength(200)]  // 原来 MaxLength(100)
    public string Name { get; set; }
}

// 2. 生成迁移（只改 MaxLength，不会删表重建）
dotnet ef migrations add ChangeDeviceNameLength \
    --project src/MineWatch.Infrastructure/MineWatch.Infrastructure.csproj \
    --startup-project src/MineWatch.Api/MineWatch.Api.csproj

// 3. 查看生成的迁移（确认是 ALTER COLUMN）
// migrationBuilder.AlterColumn<string>(
//     name: "name",
//     table: "devices",
//     maxLength: 200,
//     ...
// );

// 4. 应用
dotnet ef database update
```

### 场景 C：给现有表添加非空列（带默认值）

```csharp
// 1. 添加属性
public class Device
{
    public string Location { get; set; }  // 新增，默认 null
}

// 2. EF Core 会报错，因为现有行没有值
// 解决方案：在迁移中手动添加带默认值的列

// 3. 自定义迁移
dotnet ef migrations add AddDeviceLocation

// 4. 编辑生成的迁移，手动指定默认值
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.AddColumn<string>(
        name: "location",
        table: "devices",
        type: "character varying(100)",
        maxLength: 100,
        nullable: false,
        defaultValue: "Unknown");  // 新增这行
}

// 5. 应用
dotnet ef database update
```

### 场景 D：重命名表或列

```csharp
// 重命名表（不能直接改，需要手动 SQL）
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.RenameTable(
        name: "Devices",
        newName: "Devices_v2",
        schema: "public");

    // 或者用 Sql() 执行原始 SQL
    migrationBuilder.Sql("ALTER TABLE devices RENAME TO devices_v2;");
}

// 回滚
protected override void Down(MigrationBuilder migrationBuilder)
{
    migrationBuilder.RenameTable(
        name: "Devices_v2",
        newName: "Devices",
        schema: "public");
}
```

---

## 常见问题

### Q: 为什么连接字符串要硬编码在工厂里？

因为 `dotnet ef` 设计时执行，不会读取 `appsettings.json`。工厂的连接字符串可以与运行时不同（通常设计时指向本地数据库）。

### Q: 如何让设计时工厂读取 appsettings.json？

可以解析 `appsettings.json` 文件，但会增加复杂性。硬编码是简单直接的做法。

### Q: 迁移文件需要提交到 Git 吗？

**需要**。迁移文件是数据库结构的完整记录，所有开发者共享同一个数据库结构时必须保持一致。

### Q: 生产环境如何应用迁移？

通常在应用启动时自动应用，或通过 CI/CD 管道在部署前执行 `dotnet ef database update`。

### Q: 如何修改现有迁移？

不要直接修改已存在的迁移文件。正确做法：
1. `dotnet ef migrations remove` 移除最新迁移
2. 修改实体代码
3. `dotnet ef migrations add NewMigrationName` 重新生成

### Q: 多个 API 共用 Infrastructure，迁移谁来执行？

任意一个 API 执行即可。迁移文件存在 Infrastructure 中，`__EFMigrationsHistory` 表记录已应用的迁移，不会重复执行。通常的做法：
- 开发时：任一开发者执行
- 部署时：在 CI/CD 中选择一个项目执行，或启动时自动执行

### Q: 生产数据库和开发数据库结构不一致怎么办？

使用 `dotnet ef dbcontext scaffold` 逆向工程：

```bash
# 根据生产数据库生成代码（谨慎使用，会覆盖现有文件）
dotnet ef dbcontext scaffold \
    "Host=prod-db;Database=minewatch;Username=admin;Password=xxx" \
    Npgsql.EntityFrameworkCore.PostgreSQL \
    --project src/MineWatch.Infrastructure/MineWatch.Infrastructure.csproj \
    --force
```

### Q: 迁移执行失败怎么办？

1. 检查错误信息（通常是 SQL 执行错误）
2. 如果是列不存在、约束冲突等，手动修复数据库或调整迁移
3. 如果迁移已部分执行，查看 `__EFMigrationsHistory` 表状态
4. 必要时手动删除最后一条记录，从迁移文件中移除已执行的 SQL

### Q: 如何在启动时自动应用迁移？

```csharp
// Program.cs
using var scope = app.Services.CreateScope();
var dbContext = scope.ServiceProvider.GetRequiredService<MineWatchDbContext>();
await dbContext.Database.MigrateAsync();  // 自动应用 Pending Migrations
```

### Q: 何时用 MigrateAsync vs EnsureCreated？

| 方法 | 适用场景 |
|------|----------|
| `MigrateAsync()` | 使用迁移系统（版本化管理） |
| `EnsureCreated()` | 快速原型、测试库、从不迁移的场景 |

`EnsureCreated()` 不会创建 `__EFMigrationsHistory` 表，也不会使用迁移文件。

### Q: 删除了迁移文件但数据库已有记录怎么办？

1. 从 `__EFMigrationsHistory` 表中手动删除对应记录
2. 如果文件被误删，从 Git 恢复

---

*最后更新：2026 年 5 月*
