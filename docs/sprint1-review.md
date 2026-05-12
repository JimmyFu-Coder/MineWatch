# Sprint 1 — Review & Learning Notes

> 基于项目中实际编写的代码整理，涵盖语法、.NET 结构、开发实践三个维度。

---

## 一、C# 语法

### 1.1 对象初始化器 `{}` vs 构造函数参数 `()`

**踩坑代码**（Swagger 配置时写错）：

```csharp
// ❌ 混搭：圆括号 + 大括号
new OpenApiSecurityScheme({
    Name = "Authorization",
    Type = SecuritySchemeType.Http
});
```

**正确写法**：

```csharp
// ✓ 用大括号给属性赋值（对象初始化器语法）
new OpenApiSecurityScheme
{
    Name = "Authorization",
    Type = SecuritySchemeType.Http
};
```

**为什么**：`OpenApiSecurityScheme` 只有无参构造函数，`Name`、`Type` 等是**属性**不是构造函数参数，只能用 `{}` 初始化器赋值。

**记忆方式**：
- `()` = 构造函数传参（按位置）
- `{}` = 属性赋值（按名字）

---

### 1.2 主构造函数（Primary Constructor，C# 12）

**项目中的实际代码**：

```csharp
// Controllers/DevicesController.cs
public class DevicesController(IDeviceService deviceService) : ControllerBase
{ }

// Services/DeviceService.cs
public class DeviceService(MineWatchDbContext context) : IDeviceService
{ }

// Controllers/AuthController.cs
public class AuthController(IConfiguration configuration) : ControllerBase
{ }
```

**等价于传统写法**：

```csharp
public class DeviceService : IDeviceService
{
    private readonly MineWatchDbContext _context;

    public DeviceService(MineWatchDbContext context)
    {
        _context = context;
    }
}
```

**踩坑经历**：DeviceService 声明了 `private readonly MineWatchDbContext context;` 字段但没有构造函数赋值，导致 `_context` 永远是 null，运行时 `NullReferenceException`。

**教训**：
- 主构造函数参数直接可用，不需要再声明字段
- 传统写法必须手动写构造函数赋值，否则字段就是默认值 null

---

### 1.3 record 类型

**项目中的实际代码**：

```csharp
// DTOs/DeviceDTOs.cs
public record CreateDeviceRequest(string Name, string Type);
public record UpdateDeviceRequest(string? Name, string? Type);
public record DeviceResponse(Guid Id, string Name, string Type, DeviceStatus Status, DateTime CreatedAt, DateTime? UpdatedAt);

// Controllers/AuthController.cs
public record LoginRequest(string Username, string Password);
```

**record 自动生成**：构造函数、`Equals()`、`ToString()`、解构方法。

**record vs class 选择**：
- **DTO / 请求数据 / 值对象** → 用 `record`（不可变、简洁）
- **有行为的业务对象** → 用 `class`（可变、支持继承）

---

### 1.4 泛型 `<T>`

**项目中的实际代码**：

```csharp
// DTOs/DeviceDTOs.cs
public record PageResponse<T>(List<T> Items, int TotalCount, int Page, int PageSize)
{
    public int TotalPage => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasNextPage => Page < TotalPage;
    public bool HasPreviousPage => Page > 1;
}
```

`<T>` 是类型参数，表示"任何类型"：
- `PageResponse<Device>` — 设备分页
- `PageResponse<TelemetryReading>` — 遥测数据分页
- 一套代码，所有实体通用

---

### 1.5 nullable `?` 与空合并 `??`

**项目中的实际代码**：

```csharp
// Controllers/AuthController.cs
var jwtKey = configuration["Jwt:Key"]
             ?? throw new InvalidOperationException("Jwt:Key is not configured");
```

**原理**：
- `configuration["Jwt:Key"]` 返回 `string?`（可能为 null）
- `??` 左边如果是 null，就用右边的值
- `?? throw` 让编译器知道 `jwtKey` 一定不为 null，消除警告

**项目中的其他 nullable 用法**：

```csharp
// DTOs — string? 表示可选字段
public record UpdateDeviceRequest(string? Name, string? Type);

// Service — ?? 做空合并默认值
device.Name = request.Name ?? device.Name;   // 没传就保持原值
```

---

### 1.6 async/await 使用规则

**项目中的正反例**：

```csharp
// ❌ AuthController 最初写的（没有 await，不需要 async）
public async Task<IActionResult> Login(LoginRequest request)
{
    // 全是同步操作，没有 await
    return Ok(tokenString);
}

// ✓ 改正后：没有 await 就去掉 async
public IActionResult Login(LoginRequest request)
{ ... }

// ✓ DeviceService 有 await，需要 async
public async Task<Device?> GetByIdAsync(Guid id)
{
    return await context.Devices.FindAsync(id);
}
```

**规则**：方法体内有 `await` → 用 `async`；没有 `await` → 去掉 `async`，返回类型也改为同步版本。

---

### 1.7 元组解构

**项目中的实际代码**：

```csharp
// Service 返回元组
public async Task<(IEnumerable<Device> Items, int Total)> GetAllAsync(int page, int pageSize)
{
    var query = context.Devices.AsQueryable();
    var total = await query.CountAsync();
    var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
    return (items, total);
}

// Controller 解构元组
var (items, total) = await deviceService.GetAllAsync(page, pageSize);
```

元组 `(Items, Total)` 比专门定义一个返回类型简洁，适合内部传递少量数据。

---

## 二、.NET 结构

### 2.1 依赖注入（DI）

**项目中的注册代码**：

```csharp
// Program.cs
builder.Services.AddDbContext<MineWatchDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IDeviceService, DeviceService>();
```

**注入流程**：

```
Program.cs 注册服务
    ↓
DI 容器自动创建实例
    ↓
通过构造函数（或主构造函数）注入
    ↓
直接使用
```

**三种生命周期**：

| 生命周期 | 方法 | 场景 |
|----------|------|------|
| Scoped | `AddScoped` | 每个 HTTP 请求一个实例（DbContext、Service） |
| Transient | `AddTransient` | 每次注入新建（轻量无状态服务） |
| Singleton | `AddSingleton` | 整个应用共享（配置、缓存） |

**踩坑**：没注册或没声明构造函数 → `null` → `NullReferenceException`。

---

### 2.2 中间件管道（Middleware Pipeline）

**项目中的注册顺序**（Program.cs）：

```csharp
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
```

**请求流向**：

```
HTTP 请求
  → ExceptionHandlingMiddleware（捕获后续所有异常）
    → Authentication（检查 JWT Token，验证身份）
      → Authorization（检查 [Authorize]，判断权限）
        → Controller（执行业务逻辑）
```

**顺序很重要**：
- 异常中间件放最前面 → 包住所有后续中间件
- Authentication 在 Authorization 前面 → 先验证身份再判断权限
- `MapControllers` 放最后 → 路由到具体的 Controller

---

### 2.3 NuGet 包 vs 命名空间

**踩坑经历**：

```
安装了 Swashbuckle.AspNetCore（NuGet 包）
但 using Microsoft.OpenApi.Models 报找不到
```

**原因**：包和命名空间不是一一对应的。

```
Swashbuckle.AspNetCore（NuGet 包）
  └── 依赖 Microsoft.OpenApi.dll（另一个 DLL）
        └── Microsoft.OpenApi.Models（命名空间）
              └── OpenApiSecurityScheme（类）
              └── OpenApiSecurityRequirement（类）
              └── OpenApiReference（类）
```

**教训**：装了包只是有了 DLL，`using` 要指向类实际所在的命名空间，不是包名。

另外注意版本问题：`Swashbuckle.AspNetCore` 10.x 依赖 `Microsoft.OpenApi` 2.x，API 和 1.x 不兼容（比如 `OpenApiSecuritySchemeReference` 替代了 `OpenApiSecurityScheme` + `OpenApiReference`）。不要同时引用 `Microsoft.AspNetCore.OpenApi` preview 包，会版本冲突。

---

### 2.4 配置系统（IConfiguration）

**appsettings.json 中的配置**：

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=minewatch;..."
  },
  "Jwt": {
    "Key": "...",
    "Issuer": "MineWatch",
    "Audience": "MineWatchAPI"
  }
}
```

**两处读取，必须一致**：

```csharp
// Program.cs — JWT 中间件验证 Token 时读的配置
ValidIssuer = builder.Configuration["Jwt:Issuer"],
ValidAudience = builder.Configuration["Jwt:Audience"],
IssuerSigningKey = new SymmetricSecurityKey(
    Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))

// AuthController — 签发 Token 时读的配置
issuer: configuration["Jwt:Issuer"],
audience: configuration["Jwt:Audience"],
key: configuration["Jwt:Key"]
```

两边读同一份 `appsettings.json`，所以签发的 Token 能被验证通过。如果一边写死一边读配置，Token 就会验证失败。

---

### 2.5 Controller 基类与方法

**项目中的 Controller 都继承 ControllerBase**：

```csharp
// ✓ 正确
public class AuthController(IConfiguration configuration) : ControllerBase
{ }

// ❌ 最初写的（忘了继承）
public class AuthController
{ }
// 报错：Ok()、Unauthorized() 等方法不存在
```

**ControllerBase 提供的常用方法**：

| 方法 | HTTP 状态码 | 用途 |
|------|------------|------|
| `Ok()` | 200 | 成功返回 |
| `Created()` | 201 | 创建成功 |
| `NoContent()` | 204 | 删除成功 |
| `NotFound()` | 404 | 资源不存在 |
| `Unauthorized()` | 401 | 认证失败 |
| `BadRequest()` | 400 | 请求参数错误 |

---

## 三、开发最佳实践

### 3.1 分层架构

**项目结构**：

```
src/
├── MineWatch.Api/                    ← 表现层（Controller、DTO、Middleware）
│   ├── Controllers/
│   ├── DTOs/
│   ├── Middleware/
│   └── Services/
└── MineWatch.Infrastructure/         ← 数据层（Entity、DbContext、Migration）
    ├── Data/
    ├── Entities/
    └── Migrations/
```

**依赖方向**：Api → Infrastructure（上层依赖下层，下层不知道上层）

**各层职责**：

| 层 | 职责 | 不应该做的 |
|----|------|-----------|
| Controller | 接收请求、返回响应 | 不写业务逻辑、不直接操作 DbContext |
| Service | 业务逻辑、数据协调 | 不关心 HTTP 状态码 |
| DTO | 数据传输格式 | 不包含业务逻辑 |
| Entity | 数据库映射 | 不引用上层类型 |
| DbContext | 数据库访问 | 不包含业务规则 |

---

### 3.2 DTO 模式

**数据流向**：

```
请求 → DTO（CreateDeviceRequest）
         ↓ Service 转换
       Entity（Device）
         ↓ EF Core
       数据库

数据库 → Entity（Device）
         ↓ Service 返回
       DTO（DeviceResponse / PageResponse）
         ↓ Controller 序列化
       JSON 响应
```

**项目中的 DTO**：

```csharp
// 请求 DTO
public record CreateDeviceRequest(string Name, string Type);
public record UpdateDeviceRequest(string? Name, string? Type);
public record LoginRequest(string Username, string Password);

// 响应 DTO
public record DeviceResponse(Guid Id, string Name, string Type, DeviceStatus Status, DateTime CreatedAt, DateTime? UpdatedAt);
public record PageResponse<T>(List<T> Items, int TotalCount, int Page, int PageSize);
```

**当前不足**：DevicesController 直接返回 Entity 而不是 DTO，后续应改为返回 `DeviceResponse`，只暴露需要的字段。

---

### 3.3 关注点分离

**项目中的例子——三个机制各管各的**：

| 关注点 | 谁负责 | 代码位置 |
|--------|--------|---------|
| 异常处理 | `ExceptionHandlingMiddleware` | Middleware/ |
| 身份验证 | JWT 中间件 + `app.UseAuthentication()` | Program.cs |
| 权限控制 | `[Authorize]` 特性 | Controllers/ |
| 业务逻辑 | `DeviceService` | Services/ |
| 数据访问 | `MineWatchDbContext` | Infrastructure/ |

**原则**：不混淆、不越界。比如异常中间件不管 401 错误，那是认证中间件的职责。

---

### 3.4 配置与代码分离

**反例（踩过的坑）**：

```csharp
// ❌ 硬编码，和 Program.cs 的配置对不上
Encoding.UTF8.GetBytes()           // key 都没传
issuer: "jimmy"                    // 和配置文件不一致
audience: "jimmy"
```

**正例**：

```csharp
// ✓ 从配置读
var jwtKey = configuration["Jwt:Key"]
             ?? throw new InvalidOperationException("Jwt:Key is not configured");
var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
```

**原则**：敏感信息（JWT Key、数据库密码）和环境相关的值（连接字符串、端口）永远不硬编码，放 `appsettings.json` 或环境变量。

---

### 3.5 接口 + 实现模式

**项目中的代码**：

```csharp
// 定义接口
public interface IDeviceService
{
    Task<Device?> GetByIdAsync(Guid id);
    Task<(IEnumerable<Device> Items, int Total)> GetAllAsync(int page, int pageSize);
    Task<Device> CreateAsync(CreateDeviceRequest request);
    Task<Device?> UpdateAsync(Guid id, UpdateDeviceRequest request);
    Task<bool> DeleteAsync(Guid id);
}

// 实现类
public class DeviceService(MineWatchDbContext context) : IDeviceService
{ ... }

// Program.cs 注册（接口 → 实现）
builder.Services.AddScoped<IDeviceService, DeviceService>();

// Controller 注入接口（不依赖具体实现）
public class DevicesController(IDeviceService deviceService) : ControllerBase
{ ... }
```

**好处**：
- Controller 依赖接口，方便替换实现（比如 mock 测试）
- DI 容器统一管理生命周期

---

## 四、踩坑速查表

| 踩坑 | 现象 | 原因 | 解决 |
|------|------|------|------|
| `OpenApiSecurityScheme` 找不到 | CS0246 编译错误 | 缺 `using Microsoft.OpenApi.Models` | 加 using；删掉 `Microsoft.AspNetCore.OpenApi` preview 包避免版本冲突 |
| `AddSecurityDefinition` vs `AddSecurityRequirement` | Swagger 没有锁图标 | 方法写错了 | Definition 定义方案，Requirement 应用方案 |
| `_context` 是 null | NullReferenceException | 没有构造函数注入 DbContext | 用主构造函数：`class DeviceService(MineWatchDbContext context)` |
| `UnauthorizedAccessException` | 编译错误或异常 | 用错了方法 | Controller 里用 `Unauthorized()`，不是 `throw UnauthorizedAccessException` |
| `async` 没有对应的 `await` | 警告 | 不需要异步 | 去掉 `async`，返回类型改为同步 |
| `OpenApiSecurityScheme` 不能赋给 `OpenApiSecuritySchemeReference` | 编译错误 | Swashbuckle 10.x API 变了 | 用 `new OpenApiSecuritySchemeReference("Bearer", doc)` |
| PostgreSQL 连接失败 | Connection refused 5432 | 数据库没启动 | `docker compose up -d` |
| `string[]` 不能赋给 `List<string>` | 编译错误 | Swashbuckle 10.x 类型变了 | 用 `new List<string>()` |

---

*最后更新：2026 年 5 月*
