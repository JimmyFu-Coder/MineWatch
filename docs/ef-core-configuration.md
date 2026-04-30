# EF Core 配置方式详解

## 两种配置方式

在 Entity Framework Core 中，实体和数据库的映射有两种配置方式：

| 方式 | 配置位置 | 适用场景 |
|------|----------|----------|
| Data Annotations | 实体类属性上（`[Required]`、`[MaxLength]`） | 简单配置 |
| Fluent API | `OnModelCreating` 方法内 | 复杂配置、分离关注点 |

---

## Data Annotations（数据注解）

写在实体类上，随类定义一起存在：

```csharp
public class Device
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; }

    [Required]
    [MaxLength(50)]
    public string Type { get; set; }

    public DeviceStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
```

---

## Fluent API（流式接口）

写在 `DbContext` 的 `OnModelCreating` 方法中：

```csharp
public class MineWatchDbContext : DbContext
{
    public DbSet<Device> Devices => Set<Device>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Device>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Type).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Status).HasConversion<string>();
        });

        base.OnModelCreating(modelBuilder);
    }
}
```

---

## 为什么这个项目用 Fluent API

### 1. 实体类保持干净

实体类只关注业务逻辑，不混入数据库映射的"横切关注点"。

```csharp
// Fluent API：实体类很干净
public class Device
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public DeviceStatus Status { get; set; }
    // ...
}

// Data Annotations：混入了很多数据库相关的东西
public class Device
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("name")]
    public string Name { get; set; }
    // ...
}
```

### 2. Fluent API 是超集

有些配置只能用 Fluent API：

```csharp
// 配置关系（一对多）
modelBuilder.Entity<Device>()
    .HasMany(d => d.TelemetryReadings)
    .WithOne(t => t.Device)
    .HasForeignKey(t => t.DeviceId)
    .OnDelete(DeleteBehavior.Cascade);

// 配置唯一索引
entity.HasIndex(e => e.Name).IsUnique();

// 配置表名（和类名不一致时）
entity.ToTable("MyDevices");

// 配置复合索引
entity.HasIndex(e => new { e.Name, e.Type });
```

### 3. 可动态配置

Fluent API 里的代码是程序逻辑，可以加条件判断：

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    // 根据环境动态配置
    if (Database.IsNpgsql())
    {
        modelBuilder.Entity<Device>()
            .Property(e => e.Name)
            .HasColumnType("varchar(100)");
    }

    // 批量配置所有实体
    foreach (var entityType in modelBuilder.Model.GetEntityTypes())
    {
        modelBuilder.Entity(entityType.ClrType)
            .Property<DateTime>("CreatedAt")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
    }
}
```

---

## 本项目 Fluent API 配置详解

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Device>(entity =>
    {
        // 指定 Id 为主键
        entity.HasKey(e => e.Id);

        // Name 列为 NOT NULL，最大长度 100
        entity.Property(e => e.Name).IsRequired().HasMaxLength(100);

        // Type 列为 NOT NULL，最大长度 50
        entity.Property(e => e.Type).IsRequired().HasMaxLength(50);

        // Status 枚举存为字符串（Online/Offline）而不是整数（0/1）
        entity.Property(e => e.Status).HasConversion<string>();
    });

    // 调用基类方法（确保 EF Core 内部配置正常执行）
    base.OnModelCreating(modelBuilder);
}
```

---

## 常用 Fluent API 配置项

### 列配置
| 方法 | 作用 |
|------|------|
| `HasColumnName("name")` | 指定列名 |
| `HasColumnType("varchar(100)")` | 指定列类型 |
| `IsRequired()` | 非空约束 |
| `HasMaxLength(100)` | 最大长度 |
| `HasDefaultValue(0)` | 默认值 |
| `HasPrecision(10, 2)` | 精度（用于 decimal） |

### 主键和索引
| 方法 | 作用 |
|------|------|
| `HasKey(e => e.Id)` | 指定主键 |
| `HasIndex(e => e.Name)` | 创建索引 |
| `IsUnique()` | 唯一约束 |

### 关系配置
| 方法 | 作用 |
|------|------|
| `HasOne(e => e.Related)` | 配置一侧 |
| `HasMany(e => e.RelatedList)` | 配置多侧 |
| `HasForeignKey(e => e.Fk)` | 外键 |
| `OnDelete(DeleteBehavior.Cascade)` | 级联删除 |

### 值转换
| 方法 | 作用 |
|------|------|
| `HasConversion<string>()` | 枚举存为字符串 |
| `HasConversion<ValueConverter>()` | 自定义转换器 |

---

*最后更新：2026 年 4 月*