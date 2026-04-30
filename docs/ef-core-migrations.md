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
