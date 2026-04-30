# ASP.NET Core 生命周期

## 整体架构图

```
┌─────────────────────────────────────────────────────────────────────┐
│                         启动阶段（Startup）                            │
├─────────────────────────────────────────────────────────────────────┤
│  Main()                                                             │
│       ↓                                                             │
│  WebApplication.CreateBuilder(args)                                 │
│       ├── 加载配置                                                   │
│       │    ├── appsettings.json                                     │
│       │    ├── appsettings.{Environment}.json                       │
│       │    ├── 环境变量（ASPNETCORE_xxx）                            │
│       │    └── 命令行参数（--urls）                                  │
│       ├── 创建 ServiceCollection                                    │
│       └── 返回 WebApplicationBuilder                                │
│       ↓                                                             │
│  注册服务（builder.Services.AddXxx）                                 │
│       ├── AddDbContext           → Scoped                           │
│       ├── AddControllers         → Singleton                        │
│       ├── AddSwaggerGen          → Singleton                        │
│       ├── AddHostedService<T>    → 特殊生命周期                      │
│       └── AddAuthentication      → Singleton                        │
│       ↓                                                             │
│  builder.Build()                                                    │
│       └── 生成 WebApplication                                        │
│              ├── 配置 Kestrel HTTP Server                            │
│              ├── 注册中间件管道                                       │
│              └── 触发 ApplicationStarting 事件                       │
└─────────────────────────────────────────────────────────────────────┘
                                  ↓
┌─────────────────────────────────────────────────────────────────────┐
│                       请求处理阶段（Request）                          │
├─────────────────────────────────────────────────────────────────────┤
│  HTTP Request                                                       │
│       ↓                                                             │
│  ┌──────────────────────────────────────────────────────────────┐   │
│  │                    Kestrel HTTP Server                        │   │
│  │  • 监听端口（默认 5000/5001）                                  │   │
│  │  • TLS  termination                                            │   │
│  │  • 连接管理（Keep-Alive / WebSocket）                          │   │
│  └──────────────────────────────────────────────────────────────┘   │
│       ↓                                                             │
│  ┌──────────────────────────────────────────────────────────────┐   │
│  │              Middleware Pipeline（中间件管道）                  │   │
│  │                                                               │   │
│  │   Request  ──►  [日志]  ──►  [认证]  ──►  [授权]  ──►  [端点] │   │
│  │                ◄────────────  Response  ◄────────────         │   │
│  └──────────────────────────────────────────────────────────────┘   │
│       ↓                                                             │
│  ┌──────────────────────────────────────────────────────────────┐   │
│  │                  Routing（路由匹配）                            │   │
│  │   /api/devices  →  DevicesController.GetAll()                 │   │
│  └──────────────────────────────────────────────────────────────┘   │
│       ↓                                                             │
│  ┌──────────────────────────────────────────────────────────────┐   │
│  │                Controller / Action 执行                       │   │
│  │   Filter Pipeline  →  Action  →  Result                       │   │
│  └──────────────────────────────────────────────────────────────┘   │
│       ↓                                                             │
│  HTTP Response                                                      │
└─────────────────────────────────────────────────────────────────────┘
                                  ↓
┌─────────────────────────────────────────────────────────────────────┐
│                        关闭阶段（Shutdown）                           │
├─────────────────────────────────────────────────────────────────────┤
│  应用停止信号（SIGTERM / Ctrl+C）                                    │
│       ↓                                                             │
│  触发 ApplicationStopping 事件                                      │
│       ↓                                                             │
│  CancellationToken 广播到所有 HostedService                         │
│       ↓                                                             │
│  HostedService.StopAsync()  ──►  清理资源                           │
│       ↓                                                             │
│  Kestrel 关闭监听                                                    │
│       ↓                                                             │
│  DI 容器 Dispose 所有 IDisposable 服务                              │
│       ↓                                                             │
│  触发 ApplicationStopped 事件                                       │
└─────────────────────────────────────────────────────────────────────┘
```

---

## 1. Program.cs 解析

### 完整代码

```csharp
using Microsoft.EntityFrameworkCore;
using MineWatch.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

// 1. 注册 DbContext
builder.Services.AddDbContext<MineWatchDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. 注册 Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() { Title = "MineWatch API", Version = "v1" });
});

// 3. 注册控制器
builder.Services.AddControllers();

// 4. 注册后台服务
builder.Services.AddHostedService<MqttSubscriberService>();

var app = builder.Build();

// 5. 配置中间件管道
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

app.Run();
```

### 每行解析

| 行号 | 代码 | 作用 |
|------|------|------|
| 7–8 | `AddDbContext` | 注册 DbContext 到 DI 容器 |
| 11 | `AddEndpointsApiExplorer` | 发现 Minimal API 端点（Swagger 用） |
| 12–15 | `AddSwaggerGen` | 注册 Swagger 生成器 |
| 17 | `AddControllers` | 注册控制器工厂 |
| 19 | `builder.Build()` | 构建应用实例 |
| 21–25 | `UseSwagger / UseSwaggerUI` | 配置中间件 |
| 27 | `MapControllers` | 映射路由 |

---

## 2. DI 生命周期（Dependency Injection）

### 三种生命周期

| 注册方式 | 生命周期 | 适用场景 | 线程安全 |
|----------|----------|----------|----------|
| `AddSingleton` | 整个应用只有一个实例 | 配置对象、Logger、Swagger 生成器 | 安全 |
| `AddScoped` | 每个 HTTP 请求一个实例 | DbContext、Repository | 安全 |
| `AddTransient` | 每次注入创建新实例 | 轻量级服务 | 安全 |
| `AddHostedService` | 应用启动创建，停止销毁 | 后台任务 | — |

### 生命周期时序图

```
时间线 ────────────────────────────────────────────────────────────────────→

Singleton:  ┌───────────────────────────────────────────────────────────────┐
            │                      创建一次                                │
            └───────────────────────────────────────────────────────────────┘

Scoped:     ├─────────────────┬─────────────────┬─────────────────┬────────┤
            │    Request 1     │    Request 2     │    Request 3     │ ...
            ├─────────────────┴─────────────────┴─────────────────┴────────┤
            │ 每个请求独立实例                                               │
            └───────────────────────────────────────────────────────────────┘

Transient:  ├──┬──┬──┬──┬──┬──┬──┬──┬──┬──┬──┬──┬──┬──┬──┬──┬──┬──┬──┬──┤
            │  每次注入   │  每次注入   │  每次注入   │  每次注入   │  │
            └──┴──┴──┴──┴──┴──┴──┴──┴──┴──┴──┴──┴──┴──┴──┴──┴──┴──┴──┘

HostedService: ┌───────────────────────────────────────────────────────┐
               │           应用启动 → StartAsync()                     │
               │                       ↓                               │
               │              ExecuteAsync() 循环                      │
               │                       ↓                               │
               │           应用停止 → StopAsync()                      │
               └───────────────────────────────────────────────────────┘
```

### DbContext 为什么是 Scoped

```csharp
builder.Services.AddDbContext<MineWatchDbContext>(...);

// 不是 Singleton，因为：
// 1. 每个请求需要独立的 Change Tracker
// 2. 数据库连接不能跨请求共享
// 3. 避免并发问题（脏读）
```

### 获取服务的三种方式

```csharp
// 1. 构造器注入（最常用）
public class DevicesController : ControllerBase
{
    private readonly IDeviceRepository _repo;
    public DevicesController(IDeviceRepository repo) => _repo = repo;
}

// 2. FromServices（Action 参数）
[HttpGet]
public async Task<IActionResult> Get([FromServices] IDeviceRepository repo)
    => Ok(await repo.GetAllAsync());

// 3. HttpContext.RequestServices
var repo = HttpContext.RequestServices.GetRequiredService<IDeviceRepository>();
```

---

## 3. Middleware Pipeline（中间件管道）

### 执行顺序（请求 → 响应）

```
HTTP Request
      │
      ▼
┌─────────────────────────────────────────────────────────────────┐
│  Middleware 1（自定义日志）                                        │
│      │                                                           │
│      ▼                                                           │
│  Middleware 2（认证 Authentication）                               │
│      │                                                           │
│      ▼                                                           │
│  Middleware 3（授权 Authorization）                                │
│      │                                                           │
│      ▼                                                           │
│  Endpoint（MapControllers / MapGet 等）                            │
│      │                                                           │
│      ▼                                                           │
│  Middleware 3'（响应阶段）                                         │
│      │                                                           │
│  Middleware 2'（响应阶段）                                         │
│      │                                                           │
│  Middleware 1'（响应阶段）                                         │
└─────────────────────────────────────────────────────────────────┘
      │
      ▼
HTTP Response
```

### 本项目的中间件配置

```csharp
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();        // 生成 OpenAPI JSON
    app.UseSwaggerUI();      // 返回 Swagger UI HTML
}

app.MapControllers();        // 路由到 Controller Action
```

### 自定义中间件方式

```csharp
// 方式 1：Inline 匿名中间件（适合简单场景）
app.Use(async (context, next) =>
{
    Console.WriteLine($"请求: {context.Request.Path}");
    await next();  // 调用下一个中间件
    Console.WriteLine($"响应: {context.Response.StatusCode}");
});

// 方式 2：类中间件（推荐，职责分离）
app.UseMiddleware<RequestLoggingMiddleware>();

// 方式 3：Extension Method（最清晰）
app.UseRequestLogging();
```

### 短路中间件

```csharp
app.Use(async (context, next) =>
{
    if (context.Request.Path == "/health")
    {
        context.Response.StatusCode = 200;
        await context.Response.WriteAsJsonAsync(new { status = "ok" });
        return;  // 短路：不调用 next()，请求不继续往下走
    }
    await next();
});
```

---

## 4. Hosted Service（后台服务）

### BackgroundService 基类

```csharp
public class MqttSubscriberService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;

    public MqttSubscriberService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 业务逻辑，循环运行直到收到停止信号
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1000, stoppingToken);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        // 清理资源：关闭连接、保存状态等
        await base.StopAsync(cancellationToken);
    }
}
```

### 生命周期时序

```
应用启动
    │
    ▼
builder.Services.AddHostedService<MqttSubscriberService>()
    │
    ▼
app.Run()
    │
    ▼
IHostedService.StartAsync()
    │
    ▼
ExecuteAsync(CancellationToken)  ← 你的业务循环在这里
    │
    │  （应用运行中...）
    │
    ▼
应用收到停止信号（SIGTERM / Ctrl+C）
    │
    ▼
IHostedService.StopAsync()
    │
    ▼
ExecuteAsync 收到 CancellationToken.IsCancellationRequested = true
    │
    ▼
资源清理 → Dispose
```

### 注册到 DI

```csharp
// Program.cs
builder.Services.AddHostedService<MqttSubscriberService>();
```

### 注意事项

```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    // ✅ 正确：检查 CancellationToken
    while (!stoppingToken.IsCancellationRequested)
    {
        await Task.Delay(1000, stoppingToken);
    }

    // ❌ 错误：不检查 CancellationToken，优雅关闭失效
    while (true)
    {
        await Task.Delay(1000);
    }
}
```

---

## 5. 请求处理完整流程

```
HTTP Request
      │
      ▼
┌─────────────────────────────────────────────────────────────────┐
│ Kestrel（跨平台 HTTP 服务器）                                      │
│   • 绑定端口 5000/5001（可配置 --urls）                            │
│   • TLS termination（HTTPS 证书）                                 │
│   • 连接管理（Keep-Alive、超时）                                   │
└─────────────────────────────────────────────────────────────────┘
      │
      ▼
┌─────────────────────────────────────────────────────────────────┐
│ Middleware Pipeline                                             │
│   1. UseExceptionHandler（全局异常）                               │
│   2. UseHsts（安全头部）                                          │
│   3. UseHttpsRedirection（HTTP → HTTPS）                          │
│   4. UseStaticFiles（静态文件）                                    │
│   5. UseRouting（路由匹配）                                        │
│   6. UseAuthentication（认证）                                     │
│   7. UseAuthorization（授权）                                      │
│   8. MapControllers / MapEndpoints（端点）                         │
└─────────────────────────────────────────────────────────────────┘
      │
      ▼
┌─────────────────────────────────────────────────────────────────┐
│ Routing（路由）                                                   │
│   • 匹配 URL → Controller + Action                               │
│   • 约束（constraints）、默认值、默认值                            │
│   • 例如：/api/devices → DevicesController.GetAll()              │
└─────────────────────────────────────────────────────────────────┘
      │
      ▼
┌─────────────────────────────────────────────────────────────────┐
│ Controller Factory                                              │
│   • 创建 Controller 实例（通过 DI）                                │
│   • 注入构造器依赖                                                │
└─────────────────────────────────────────────────────────────────┘
      │
      ▼
┌─────────────────────────────────────────────────────────────────┐
│ Filter Pipeline                                                 │
│   Authorization Filter  ──────────────────────────────────────┐ │
│   Resource Filter       ──────────────────────────────────────┐ │
│   Action Filter         ──────────────────────────────────────┐ │
│   Action（你的业务代码）                                       ◄─┼─┘
│   Exception Filter      ──────────────────────────────────────┐ │
│   Result Filter         ──────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────┘
      │
      ▼
┌─────────────────────────────────────────────────────────────────┐
│ Result Execution                                                │
│   • return Ok(devices)  → 200 OK + JSON                         │
│   • return NotFound()   → 404                                   │
│   • return View()       → MVC View 渲染                         │
└─────────────────────────────────────────────────────────────────┘
      │
      ▼
HTTP Response
```

### 依赖注入在 Controller 中的使用

```csharp
[ApiController]
[Route("api/[controller]")]
public class DevicesController : ControllerBase
{
    // 构造器注入（Controller 创建时由 DI 容器注入）
    public DevicesController(
        MineWatchDbContext context,
        IDeviceRepository repository,
        ILogger<DevicesController> logger)
    {
        _context = context;
        _repository = repository;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        // 业务逻辑
        var devices = await _repository.GetAllAsync();
        return Ok(devices);
    }
}
```

---

## 6. Filter Pipeline（过滤器管道）

### 执行顺序

```
请求进入
    │
    ├──►  Authorization Filter  ← 权限检查（最先执行）
    │
    ├──►  Resource Filter       ← 资源级别的处理（如缓存）
    │
    ├──►  Action Filter         ──► OnActionExecuting
    │                                 │
    │                           Action（你的代码）
    │                                 │
    │                            OnActionExecuted ◄─┘
    │
    ├──►  Exception Filter      ← 捕获 Action 抛出的异常
    │
    └──►  Result Filter         ──► OnResultExecuting
                                      │
                                Result Execution
                                      │
                                 OnResultExecuted ◄─┘
    │
    ▼
响应离开
```

### 常用内置过滤器

```csharp
[ApiController]           // 自动模型验证 + API 约定
[Authorize]               // 需要登录
[AllowAnonymous]          // 允许匿名访问（跳过授权检查）
[RequiredScope("read")]   // OAuth2 Scope 要求
[ServiceFilter(typeof(MyFilter))]  // 指定 Filter 类
```

### 自定义 Action Filter 示例

```csharp
public class RequestTimingFilter : IActionFilter
{
    private readonly ILogger _logger;

    public RequestTimingFilter(ILogger<RequestTimingFilter> logger)
    {
        _logger = logger;
    }

    public void OnActionExecuting(ActionExecutingContext context)
    {
        // Action 执行前
        context.HttpContext.Items["StartTime"] = DateTime.UtcNow;
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
        // Action 执行后
        var elapsed = DateTime.UtcNow - (DateTime)context.HttpContext.Items["StartTime"]!;
        _logger.LogInformation("Action 耗时: {ElapsedMs}ms", elapsed.TotalMilliseconds);
    }
}
```

---

## 7. 应用生命周期事件（Application Lifetime Events）

```csharp
var app = builder.Build();

// 应用启动后（监听端口后）
app.ApplicationStarted.Register(() => Console.WriteLine("应用已启动"));

// 应用正在停止（开始关闭流程）
app.ApplicationStopping.Register(() => Console.WriteLine("正在停止..."));

// 应用已停止
app.ApplicationStopped.Register(() => Console.WriteLine("已停止"));

// 也可以用事件
app.Lifetime.ApplicationStarted += (sender, args) => { };
app.Lifetime.ApplicationStopping += (sender, args) => { };
app.Lifetime.ApplicationStopped += (sender, args) => { };
```

### 事件触发时机

```
app.Run()  ← 阻塞直到应用退出
      │
      ▼
触发 ApplicationStarted
      │
      ▼
HTTP 请求开始处理...
      │
      ▼  （收到 SIGTERM / Ctrl+C）
触发 ApplicationStopping
      │
      ▼
HostedService.StopAsync() 执行
      │
      ▼
所有请求处理完毕
      │
      ▼
触发 ApplicationStopped
      │
      ▼
进程退出
```

---

## 8. 配置加载顺序

```json
// appsettings.json（基础配置）
{
  "Logging": { "LogLevel": "Information" },
  "AllowedHosts": "*"
}

// appsettings.Development.json（开发环境覆盖）
{
  "Logging": { "LogLevel": "Debug" }  // 开发环境更详细的日志
}

// appsettings.Production.json（生产环境）
{
  "Logging": { "LogLevel": "Warning" }  // 生产环境只记录警告+
}
```

### 覆盖顺序（后面的覆盖前面的）

```
appsettings.json
    ↓  覆盖
appsettings.{Environment}.json
    ↓  覆盖
环境变量（ASPNETCORE_xxx）
    ↓  覆盖
命令行参数（--environment=Production）
```

### 环境检测

```csharp
// Program.cs 中
if (app.Environment.IsDevelopment())
{
    // 开发环境代码
}

if (app.Environment.IsProduction())
{
    // 生产环境代码
}

if (app.Environment.IsEnvironment("Staging"))
{
    // Staging 环境代码
}
```

---

## 9. Health Checks（健康检查）

### 注册

```csharp
// Program.cs
builder.Services.AddHealthChecks()
    .AddNpgSql("Server=localhost;Database=minewatch;User=postgres;Password=postgres");

app.MapHealthChecks("/health");
```

### 生命周期

```
应用启动
    ↓
MapHealthChecks("/health")  ← 注册健康检查端点
    ↓
GET /health 请求进入
    ↓
执行所有注册的 HealthCheck
    ↓
返回健康状态
   • Healthy（200）
   • Unhealthy（503）
   • Degraded（200，但有问题）
```

---

## 10. Startup vs Minimal Hosting 对比

### .NET 6 之前：Startup.cs 模式

```csharp
public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddDbContext<MineWatchDbContext>();
        services.AddControllers();
    }

    public void Configure(IApplicationBuilder app)
    {
        app.UseRouting();
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
        });
    }
}
```

### .NET 6+：Minimal API 模式（我们现在用的）

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<MineWatchDbContext>();
builder.Services.AddControllers();

var app = builder.Build();

app.UseRouting();
app.MapControllers();

app.Run();
```

**对比**：

| 特性 | Startup.cs | Minimal API |
|------|-----------|-------------|
| 代码行数 | 多（需要两个类） | 少（一个文件） |
| 功能 | 完全相同 | 完全相同 |
| 可测试性 | 好 | 好 |
| 推荐度 | 旧项目继续用 | 新项目首选 |

---

## 知识点检查清单

- [x] Program.cs 启动流程
- [x] DI 三种生命周期
- [x] Middleware Pipeline
- [x] Hosted Service 生命周期
- [x] 请求处理完整流程
- [x] Filter Pipeline
- [x] Application Lifetime Events
- [x] 配置加载顺序
- [x] Health Checks
- [x] Startup vs Minimal API

---

*最后更新：2026 年 4 月*