# JWT 认证详解

## 什么是 JWT？

JWT（JSON Web Token）是一种开放标准，用于在客户端和服务器之间安全地传递身份信息。

它的本质是一个**加密签名的字符串**，分为三段，用 `.` 分隔：

```
Header.Payload.Signature

eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiJhZG1pbiJ9.abc123xyz
```

| 段 | 内容 | 说明 |
|----|------|------|
| Header | 算法类型（如 HS256） | Base64 编码 |
| Payload | 用户信息、过期时间等 Claims | Base64 编码，**不加密，可解码** |
| Signature | 用密钥对前两段签名 | 防篡改 |

> **注意**：Payload 是 Base64 编码，不是加密 — 任何人都可以解码读取内容。**不要在 Payload 里放密码等敏感信息。**

---

## JWT 的工作流程

```
1. 客户端 POST /api/auth/login（用户名 + 密码）
         │
         ▼
2. 服务器验证用户身份，生成 JWT Token，返回给客户端
         │
         ▼
3. 客户端保存 Token（通常存在内存或 localStorage）
         │
         ▼
4. 客户端每次请求受保护接口时，在 Header 里带上 Token：
   Authorization: Bearer <token>
         │
         ▼
5. 服务器验证 Token 合法性（签名、过期、Issuer、Audience）
         │
         ▼
6. 验证通过 → 执行业务逻辑；失败 → 返回 401
```

---

## 为什么用 JWT？

传统的 Session 认证需要服务器**保存用户状态**（存在内存或 Redis）。JWT 把状态存在 Token 里，服务器**无需存储**，天然支持水平扩展。

| | Session | JWT |
|--|---------|-----|
| 状态存储 | 服务器端 | 客户端（Token 里） |
| 水平扩展 | 需要共享 Session（Redis） | 天然无状态 |
| Token 撤销 | 直接删 Session | 麻烦（需黑名单机制） |
| 适合场景 | 传统 Web 应用 | REST API、微服务 |

---

## MineWatch 中的实现

### appsettings.json

```json
{
  "Jwt": {
    "Key": "minewatch-super-secret-key-12345678",
    "Issuer": "MineWatch",
    "Audience": "MineWatchApi"
  },
  "TestUser": {
    "Username": "admin",
    "Password": "password123"
  }
}
```

- **Key**：签名密钥，至少 32 字符。用来生成和验证 Token 的签名。
- **Issuer**：Token 的颁发者，验证时检查 Token 是不是这里发出的。
- **Audience**：Token 的接收方，验证时检查 Token 是不是发给这个 API 的。

---

### Program.cs — 服务注册阶段

```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
    });
```

这一步是**注册规则**，告诉 ASP.NET Core：

| 代码 | 含义 |
|------|------|
| `AddAuthentication(JwtBearerDefaults.AuthenticationScheme)` | 默认认证方案用 JWT Bearer，即从 `Authorization: Bearer <token>` 读取 token |
| `ValidateIssuer = true` | 验证 token 的 Issuer 必须匹配 `ValidIssuer` |
| `ValidateAudience = true` | 验证 token 的 Audience 必须匹配 `ValidAudience` |
| `ValidateLifetime = true` | 验证 token 没有过期 |
| `ValidateIssuerSigningKey = true` | 验证签名，防止 token 被篡改 |
| `SymmetricSecurityKey` | 把字符串密钥转成字节数组，供 JWT 库做签名验证 |

---

### Program.cs — 中间件管道阶段

```csharp
app.UseAuthentication();  // 第一步：解析 Token，识别"你是谁"
app.UseAuthorization();   // 第二步：检查"你能做什么"
```

**顺序必须固定**，原因：

```
请求进来
   │
   ▼
UseAuthentication()
   把 Token 解析出来，把用户身份（Claims）写入 HttpContext.User
   │
   ▼
UseAuthorization()
   读取 HttpContext.User，判断是否满足 [Authorize] 的要求
   如果 UseAuthentication 没先跑，HttpContext.User 是空的，授权永远失败
```

---

### 两个阶段的关系

```
builder.Services.Add...()   →  配置规则（程序启动时执行一次）
app.Use...()                →  应用规则（每个请求都执行）
```

类比：`builder.Services` 是制定交通规则，`app.Use` 是在路上设检查站。规则不配就没有检查站；检查站顺序错了就拦不住人。

---

## 当前方案的局限性

MineWatch Sprint 1 使用的是**最简单的 JWT 实现**：

- 密钥硬编码在配置文件里，不会自动轮换
- 用户硬编码（TestUser），没有真实用户数据库
- 没有 Refresh Token 机制，Token 过期只能重新登录
- 没有 Token 撤销机制

这对学习和开发阶段足够用。生产环境应替换为 **AWS Cognito** 或 **Auth0** 等专业身份服务。
