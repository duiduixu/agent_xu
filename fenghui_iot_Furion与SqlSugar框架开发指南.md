# IotPlatform 项目 Furion + SqlSugar 框架开发指南

> **适用对象：** IotPlatform 项目开发人员  
> **创建日期：** 2026-06-28  
> **框架版本：** Furion 4.9.8.18 / SqlSugarCore 5.1.4.210  
> **目标框架：** .NET 8 / PostgreSQL

---

## 目录

- [一、项目架构概述](#一项目架构概述)
- [二、Furion 框架详解](#二furion-框架详解)
  - [2.1 框架简介与核心能力](#21-框架简介与核心能力)
  - [2.2 项目入口与启动流程](#22-项目入口与启动流程)
  - [2.3 AppStartup 启动配置](#23-appstartup-启动配置)
  - [2.4 动态 API 控制器（核心模式）](#24-动态-api-控制器核心模式)
  - [2.5 依赖注入](#25-依赖注入)
  - [2.6 配置选项（ConfigurableOptions）](#26-配置选项configurableoptions)
  - [2.7 JWT 认证与授权](#27-jwt-认证与授权)
  - [2.8 友好异常处理](#28-友好异常处理)
  - [2.9 事件总线](#29-事件总线)
  - [2.10 任务调度（Schedule）](#210-任务调度schedule)
  - [2.11 远程请求（HttpRemote）](#211-远程请求httpremote)
  - [2.12 统一结果处理](#212-统一结果处理)
  - [2.13 CORS 跨域](#213-cors-跨域)
  - [2.14 文件上传与虚拟文件系统](#214-文件上传与虚拟文件系统)
- [三、SqlSugar ORM 框架详解](#三sqlsugar-orm-框架详解)
  - [3.1 框架简介](#31-框架简介)
  - [3.2 数据库连接配置](#32-数据库连接配置)
  - [3.3 实体定义规范](#33-实体定义规范)
  - [3.4 仓储模式](#34-仓储模式)
  - [3.5 查询操作大全](#35-查询操作大全)
  - [3.6 增删改操作](#36-增删改操作)
  - [3.7 事务与工作单元](#37-事务与工作单元)
  - [3.8 多租户数据隔离](#38-多租户数据隔离)
  - [3.9 AOP 拦截器](#39-aop-拦截器)
  - [3.10 种子数据](#310-种子数据)
  - [3.11 分表支持](#311-分表支持)
- [四、Furion + SqlSugar 集成实践](#四furion--sqlsugar-集成实践)
- [五、开发快速上手](#五开发快速上手)
  - [5.1 创建新业务模块](#51-创建新业务模块)
  - [5.2 创建新实体](#52-创建新实体)
  - [5.3 创建新 API 服务](#53-创建新-api-服务)
  - [5.4 常见问题排查](#54-常见问题排查)

---

## 一、项目架构概述

IotPlatform 是一个基于 .NET 8 构建的大型 IoT（物联网）SaaS 平台，采用分层架构：

```
01-架构核心/
  ├── IotPlatform.Core                    # 核心库（常量、枚举、工具、扩展）
  ├── Extras.DatabaseAccessor.SqlSugar     # SqlSugar ORM 封装层
  ├── Extras.MQTT                          # MQTT 引擎
  └── Extras.TDengine                      # TDengine 时序数据库

02-应用模块/
  ├── 00-Common/                           # 公共模块（用户管理器、事件总线、任务）
  ├── 01-OAuth/OAuth/                      # 认证授权
  ├── 02-System/                           # 系统管理（用户、角色、菜单、权限、配置等）
  ├── 03-BusApp/                           # 业务应用层
  ├── 04-DataWeaving/                      # 数据编织
  ├── 05-Message/                          # 消息服务
  ├── 06-Task/                             # 任务调度 / 工作流 / 程序块
  ├── 07-Thing/                            # 物模型、告警、统计、远程控制、视频
  ├── 08-VisualData/                       # 可视化数据
  ├── 09-Engine/                           # JS 脚本引擎 / 可视化开发引擎
  ├── 10-VisualDev/                        # 可视化开发
  ├── 11-Extend/                           # 文档服务 / 加密狗
  └── 13-Device/                           # 设备驱动（西门子、三菱、欧姆龙、Modbus）

03-应用服务/
  ├── IotPlatform/                         # Web API 入口（Furion）
  ├── IotPlatform.Web.Core/                # Web 核心（Startup、JWT、gRPC、SignalR）
  └── IotPlatform.CollectionService/       # 设备采集服务（独立进程，非 Furion）
```

**核心依赖关系：**

```
IotPlatform (Web 入口)
  └── IotPlatform.Web.Core (Web 核心，Furion AppStartup)
        ├── IotPlatform.Application (业务应用)
        ├── Systems.Core (系统核心服务)
        ├── IotPlatform.ThingModel (物模型)
        ├── ... (20+ 业务模块)
        └── Extras.DatabaseAccessor.SqlSugar (ORM 层)
```

---

## 二、Furion 框架详解

### 2.1 框架简介与核心能力

[Furion](https://furion.net/) 是一个国产 .NET 应用框架，基于 ASP.NET Core 进行了大量封装简化。在本项目中，Furion 通过以下包引入：

```xml
<!-- IotPlatform.Core.csproj 中的间接依赖 -->
<PackageReference Include="Furion.Pure" Version="4.9.8.18" />
<PackageReference Include="Furion.Extras.Authentication.JwtBearer" Version="4.9.8.18" />
<PackageReference Include="Furion.Extras.ObjectMapper.Mapster" Version="4.9.8.18" />
```

**本项目使用的 Furion 核心能力一览：**

| 能力 | 说明 | 使用位置 |
|------|------|----------|
| `Serve.Run()` | 应用入口，替代传统 `WebApplication` | Program.cs |
| `AppStartup` | 启动配置类，替代传统 Startup | Startup.cs |
| `IDynamicApiController` | 服务即控制器，自动生成 API 端点 | 所有业务服务 |
| `ITransient/IScoped/ISingleton` | 自动依赖注入注册 | 所有服务实现 |
| `ConfigurableOptions` | 配置选项自动绑定 JSON 配置 | 所有配置类 |
| `JWTEncryption` + `AppAuthorizeHandler` | JWT 认证与自动刷新 Token | JwtHandler.cs |
| `Oops.Oh()` / `Oops.Bah()` | 友好异常抛出 | 所有服务 |
| `UnifyResult` | 统一 API 返回格式 | 全局启用 |
| `EventBus` | 事件总线（内存/Redis） | Startup.cs |
| `Schedule` | 任务调度 | Startup.cs |

### 2.2 项目入口与启动流程

**文件位置：** `IotPlatform/Program.cs`

```csharp
using System.Reflection;
using IotPlatform.Core.Extension;

Serve.Run(RunOptions.Default.AddWebComponent<WebComponent>());

public class WebComponent : IWebComponent
{
    public void Load(WebApplicationBuilder builder, ComponentContext componentContext)
    {
        // 设置版本号
        StringExtension.Version = $"v{Assembly.GetExecutingAssembly().GetName().Version!.ToString()}";
        
        // 日志过滤：屏蔽 Microsoft 命名空间的日志
        builder.Logging.AddFilter((provider, category, logLevel) =>
        {
            return !new[] { "Microsoft.Hosting", "Microsoft.AspNetCore" }
                .Any(category.StartsWith) && logLevel >= LogLevel.Information;
        });

        // Kestrel 配置：超时时间、请求体大小
        builder.Configuration.Get<WebHostBuilder>().ConfigureKestrel(u =>
        {
            u.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(30);
            u.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(30);
            u.Limits.MaxRequestBodySize = null;  // 不限制上传大小
        });
    }
}
```

**关键要点：**
- `Serve.Run()` 是 Furion 的入口方法，替代了传统的 `WebApplication.CreateBuilder(args).Build().Run()`
- `RunOptions.Default.AddWebComponent<T>()` 注册自定义启动组件
- `IWebComponent.Load()` 在应用构建阶段执行，可用于 Kestrel、日志等底层配置
- 不需要显式的 `Program.Main()` 入口

### 2.3 AppStartup 启动配置

**文件位置：** `IotPlatform.Web.Core/Startup.cs`

`Startup` 继承自 `AppStartup`，Furion 通过程序集扫描自动发现并调用。包含两个核心方法：

```csharp
namespace IotPlatform.Web.Core;

public class Startup : AppStartup
{
    public void ConfigureServices(IServiceCollection services)
    {
        // 1. SnowFlake ID 初始化
        YitIdHelper.SetIdGenerator(new IdGeneratorOptions { WorkerId = 3 });

        // 2. 配置选项注册
        services.AddProjectOptions();

        // 3. 缓存、数据库、JWT、CORS 等基础服务
        services.AddCache();
        services.SqlSugarConfigure();
        services.AddJwt<JwtHandler>(enableGlobalAuthorize: true);
        services.AddCorsAccessor();
        services.AddHttpRemote();

        // 4. Furion 功能模块
        services.AddTaskQueue();
        services.AddSchedule(options => {
            options.LogEnabled = false;
            options.AddPersistence<DbJobPersistence>();
        });
        services.AddSensitiveDetection();

        // 5. MVC + 统一结果
        services.AddControllersWithViews()
            .AddMvcFilter<RequestActionFilter>()
            .AddAppLocalization()
            .AddNewtonsoftJson(options => { /* ... */ })
            .AddInjectWithUnifyResult();

        // 6. 事件总线
        services.AddEventBus(options => { /* ... */ });

        // 7. SignalR / gRPC
        services.AddSignalR();
        services.AddGrpc();

        // 8. 业务服务 Singleton 注册
        services.AddSingleton<ICollectionRuntimeClient, GrpcCollectionRuntimeClient>();
        services.AddHostedService<RuntimeVariableSnapshotHostedService>();
        // ... 更多
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
            app.UseDeveloperExceptionPage();
        else
            app.UseExceptionHandler("/Home/Error");

        app.UseForwardedHeaders();
        app.UseUnifyResultStatusCodes();    // Furion 统一状态码
        app.UseStaticFiles();
        app.UseRouting();
        app.EnableBuffering();
        app.UseCorsAccessor();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseScheduleUI();                // Furion 调度 UI
        app.UseInject(string.Empty);        // Furion 注入中间件

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapGrpcService<PlatformVariableIngestGrpcService>();
            endpoints.MapHubs();
            endpoints.MapHub<DeviceHub>("/device");
            endpoints.MapControllerRoute("default",
                "{controller=Home}/{action=Index}/{id?}");
        });
    }
}
```

### 2.4 动态 API 控制器（核心模式）

这是本项目**最重要的设计模式**。Furion 允许服务类直接变为 API 控制器，无需创建单独的 Controller 文件。

#### 基本用法

```csharp
// 实现 IDynamicApiController + ITransient 即可将服务变为 API 控制器
[ApiDescriptionSettings("系统服务", Order = 450)]
public class SysMenuService : IDynamicApiController, ITransient
{
    // 每个 PUBLIC 方法自动成为 API 端点
    
    [HttpGet("/sysMenu/list")]
    [DisplayName("系统菜单列表（树表）")]
    public async Task<List<SysMenu>> GetMenuList([FromQuery] GetMenuListInput input)
    {
        // GET /sysMenu/list
    }

    [HttpPost("/sysMenu/add")]
    [DisplayName("增加系统菜单")]
    public async Task AddMenu(AddMenuInput input)
    {
        // POST /sysMenu/add
    }

    // [NonAction] 标记的方法不会成为 API
    [NonAction]
    public async Task<bool> HasMenu(string appCode) { }
}
```

#### 常用特性

| 特性 | 用途 | 示例 |
|------|------|------|
| `[ApiDescriptionSettings("分组", Order = N)]` | 设置 Swagger 分组和排序 | `[ApiDescriptionSettings("物模型")]` |
| `[DisplayName("名称")]` | API 方法描述 | `[DisplayName("获取菜单列表")]` |
| `[HttpGet("/path")]` | 显式路由（推荐） | `[HttpGet("/thing/page")]` |
| `[HttpPost("/path")]` | POST 路由 | `[HttpPost("/thing/add")]` |
| `[AllowAnonymous]` | 允许匿名访问 | 登录接口 |
| `[NonAction]` | 不暴露为 API | 内部方法 |
| `[NonUnify]` | 不进行统一结果包装 | Swagger 登录检查 |
| `[Required]` | 参数必填校验 | `Login([Required] LoginInput input)` |
| `[FromQuery]` | 参数来自 QueryString | GET 请求参数 |
| `[ApiDescriptionSettings(Name = "Add")]` | 方法级 API 命名 | 区分同路由不同方法 |

#### 本项目中的路由约定

配置文件 `App.json` 中的 `DynamicApiControllerSettings`：
```json
{
  "DynamicApiControllerSettings": {
    "CamelCaseSeparator": "",
    "SplitCamelCase": false,
    "LowercaseRoute": false,
    "AsLowerCamelCase": false,
    "KeepVerb": false,
    "KeepName": false
  }
}
```

**最佳实践：** 本项目采用显式路由（`[HttpGet("/path")]`），不依赖 Furion 的自动路由约定，更清晰可控。

#### 认证模块示例

```csharp
[ApiDescriptionSettings(Tag = "OAuth", Order = 100)]
public class OAuthService : IDynamicApiController, ITransient
{
    private readonly ISqlSugarRepository<SysUser> _sysUserRep;
    private readonly IUserManager _userManager;

    public OAuthService(ISqlSugarRepository<SysUser> sysUserRep, IUserManager userManager)
    {
        _sysUserRep = sysUserRep;
        _userManager = userManager;
    }

    [AllowAnonymous]
    [HttpPost("/login")]
    [DisplayName("账号密码登录")]
    public async Task<LoginOutput> Login([Required] LoginInput input)
    {
        // 账号是否存在
        SysUser user = await _sysUserRep.AsQueryable()
            .Includes(t => t.SysOrg)
            .ClearFilter()
            .FirstAsync(u => u.Account.Equals(input.Account));
        _ = user ?? throw Oops.Oh(ErrorCode.D0009);

        // 密码校验
        if (!user.Password.Equals(input.Password))
            throw Oops.Oh(ErrorCode.D1000);

        // 生成 Token
        string accessToken = JWTEncryption.Encrypt(new Dictionary<string, object>
        {
            { ClaimConst.UserId, user.Id },
            { ClaimConst.TenantId, user.TenantId },
            { ClaimConst.Account, user.Account },
            { ClaimConst.RealName, user.Name },
            { ClaimConst.AdminType, user.AdminType },
            { ClaimConst.OrgId, user.OrgId },
        }, tokenExpire);

        return new LoginOutput { AccessToken = accessToken };
    }

    [HttpGet("/getLoginUser")]
    [DisplayName("获取登录账号")]
    public async Task<dynamic> GetUserInfo()
    {
        SysUser user = await _sysUserRep.GetFirstAsync(u => u.Id == _userManager.UserId)
            ?? throw Oops.Oh(ErrorCode.D1011).StatusCode(401);
        return new { user.Id, user.Account, RealName = user.Name, /* ... */ };
    }
}
```

### 2.5 依赖注入

Furion 提供了两种 DI 注册方式：**自动扫描注册** 和 **手动注册**。

#### 方式一：接口继承自动注册（推荐，项目主要方式）

实现以下接口之一，Furion 自动扫描并注册到 DI 容器：

```csharp
// 瞬时（每次获取创建新实例）— 项目中 IDynamicApiController 服务的默认选择
public class MyService : ITransient { }

// 作用域（每个请求创建一个实例）
public class MyService : IScoped { }

// 单例（全局唯一实例）
public class MyService : ISingleton { }
```

**最常见的组合：**
```csharp
public class SysConfigService : IDynamicApiController, ITransient
{
    // ITransient 确保服务自动注册到 DI
    // IDynamicApiController 确保服务成为 API 控制器
}
```

#### 方式二：手动注册（Startup.cs 中）

用于非通用模式的注册、HostedService、外部客户端等：

```csharp
// 配置选项
services.AddConfigurableOptions<CacheOptions>();

// 单例 gRPC 客户端
services.AddSingleton<ICollectionRuntimeClient, GrpcCollectionRuntimeClient>();

// 后台服务
services.AddHostedService<RuntimeVariableSnapshotHostedService>();

// JWT 认证（Furion 封装）
services.AddJwt<JwtHandler>(enableGlobalAuthorize: true);

// CORS（Furion 封装）
services.AddCorsAccessor();
```

#### 获取已注册的服务

```csharp
// 方式一：构造函数注入（最常用，推荐）
public class MyService(ISqlSugarRepository<SysUser> userRep) { }

// 方式二：静态获取（Furion 提供）
var service = App.GetService<CollectionVariableService>();

// 方式三：从 HttpContext 获取
var provider = App.HttpContext.RequestServices;
```

### 2.6 配置选项（ConfigurableOptions）

Furion 提供了一种优雅的强类型配置绑定方式。

#### 定义配置类

```csharp
// 使用 [OptionsSettings] 或实现 IConfigurableOptions
public sealed class ConnectionStringsOptions : IConfigurableOptions<ConnectionStringsOptions>
{
    public bool EnableConsoleSql { get; set; }
    public List<DbConnectionConfig> ConnectionConfigs { get; set; }
    
    // PostConfigure 在绑定后自动调用
    public void PostConfigure(ConnectionStringsOptions options, IConfiguration configuration) { }
}
```

#### 注册配置

```csharp
// ProjectOptions.cs
public static IServiceCollection AddProjectOptions(this IServiceCollection services)
{
    services.AddConfigurableOptions<CacheOptions>();
    services.AddConfigurableOptions<UploadOptions>();
    services.AddConfigurableOptions<TenantOptions>();
    services.AddConfigurableOptions<ConnectionStringsOptions>();
    services.AddConfigurableOptions<EventBusOptions>();
    return services;
}
```

#### 读取配置

```csharp
// 方式一：Furion 静态方法
ConnectionStringsOptions dbOptions = App.GetOptions<ConnectionStringsOptions>();

// 方式二：通过配置名称
CacheOptions cacheOptions = App.GetConfig<CacheOptions>("Cache", true);

// 方式三：构造函数注入（推荐）
public class MyService(IOptions<ConnectionStringsOptions> options) { }
```

#### 配置文件扫描

Furion 扫描 `appsettings.json` 中 `ConfigurationScanDirectories` 指定的目录：

```json
{
  "$schema": "https://gitee.com/dotnetchina/Furion/raw/v4/schemas/v4/furion-schema.json",
  "ConfigurationScanDirectories": ["Configuration", ""]
}
```

本项目配置文件列表：

| 文件 | 用途 |
|------|------|
| `App.json` | 应用设置、Kestrel、CORS、动态API、友好异常 |
| `Cache.json` | Redis/内存缓存配置 |
| `Database.json` | 数据库连接（PostgreSQL）、TDengine、MQTT |
| `JWT.json` | JWT 密钥、颁发者、受众、算法 |
| `Swagger.json` | Swagger 文档分组、枚举 |
| `Logging.json` | 日志级别、文件日志、数据库日志 |
| `Upload.json` | 文件上传类型、大小、保存路径 |
| `EventBus.json` | 事件总线模式（内存/Redis） |

### 2.7 JWT 认证与授权

**文件位置：** `IotPlatform.Web.Core/Handlers/JwtHandler.cs`

```csharp
public class JwtHandler : AppAuthorizeHandler
{
    public override async Task HandleAsync(AuthorizationHandlerContext context, DefaultHttpContext httpContext)
    {
        // 自动刷新 Token 机制
        int tokenExpire = await sysConfigService.GetTokenExpire();
        int refreshTokenExpire = await sysConfigService.GetRefreshTokenExpire();
        
        if (JWTEncryption.AutoRefreshToken(context, currentHttpContext, tokenExpire, refreshTokenExpire))
        {
            await AuthorizeHandleAsync(context);
        }
        else
        {
            context.Fail();
            currentHttpContext.SignoutToSwagger();
        }
    }

    public override async Task<bool> PipelineAsync(AuthorizationHandlerContext context, DefaultHttpContext httpContext)
    {
        return await CheckAuthorizeAsync(httpContext);
    }
}
```

**JWT Token 生成（OAuthService）：**
```csharp
string accessToken = JWTEncryption.Encrypt(new Dictionary<string, object>
{
    { ClaimConst.UserId, user.Id },
    { ClaimConst.TenantId, user.TenantId },
    { ClaimConst.Account, user.Account },
    { ClaimConst.RealName, user.Name },
    { ClaimConst.AdminType, user.AdminType },
    { ClaimConst.OrgId, user.OrgId },
}, tokenExpire);

// 设置响应头
httpContext.SetTokensOfResponseHeaders(accessToken, refreshToken);
```

### 2.8 友好异常处理

Furion 提供了 `Oops.Oh()` 和 `Oops.Bah()` 两种异常抛出方式：

```csharp
// 方式一：使用预定义错误码
throw Oops.Oh(ErrorCode.D0009);           // "账号不存在"
throw Oops.Oh(ErrorCode.D1000);           // "密码错误"
throw Oops.Oh(ErrorCode.D1017);           // "账号被冻结"

// 方式二：自定义消息
throw Oops.Bah("设备配置不存在");

// 方式三：带状态码
throw Oops.Oh(ErrorCode.D1011).StatusCode(401);

// 方式四：带消息
throw Oops.Oh("系统还未授权，请联系发行方");
```

配套启用 `UseUnifyResultStatusCodes()` 中间件，统一错误响应格式。

### 2.9 事件总线

事件总线支持内存和 Redis 两种模式：

```csharp
services.AddEventBus(options =>
{
    options.UseUtcTimestamp = false;
    options.LogEnabled = false;
    
    // 未处理异常处理
    options.UnobservedTaskExceptionHandler = (obj, args) =>
    {
        Log.Error($"EventBus 有未处理异常：{args.Exception?.Message}", args.Exception);
    };

    // Redis 模式（可选）
    EventBusOptions config = App.GetOptions<EventBusOptions>();
    if (config.EventBusType == EventBusType.Redis)
    {
        options.ReplaceStorer(serviceProvider =>
        {
            var cache = serviceProvider.GetRequiredService<ICacheProvider>();
            return new RedisEventSourceStorer(cache, config);
        });
    }
});
```

### 2.10 任务调度（Schedule）

基于 Furion 内置的任务调度模块（类似 Quartz）：

```csharp
services.AddSchedule(options =>
{
    options.LogEnabled = false;
    options.AddPersistence<DbJobPersistence>(); // 持久化到数据库
    // options.AddJob<LogJob>(Triggers.Workday()); // 添加作业
});
```

启用调度看板：
```csharp
app.UseScheduleUI(options =>
{
    options.DisplayEmptyTriggerJobs = false;
    options.DisplayHead = false;
});
```

### 2.11 远程请求（HttpRemote）

```csharp
services.AddHttpRemote();
```

Furion 的 `HttpRemote` 提供声明式的 HTTP 客户端调用，本项目用于与采集服务等外部服务通信。

### 2.12 统一结果处理

通过 `AddInjectWithUnifyResult()` 启用，所有 API 响应自动包装为统一格式：

```csharp
services.AddControllersWithViews()
    .AddInjectWithUnifyResult();
```

响应格式示例：
```json
{
  "succeeded": true,
  "code": 200,
  "message": "success",
  "data": { /* 实际数据 */ },
  "timestamp": 1620000000000
}
```

如果某个方法不想被统一包装，使用 `[NonUnify]` 特性。

### 2.13 CORS 跨域

一行代码即可配置 CORS：

```csharp
services.AddCorsAccessor();  // 注册
app.UseCorsAccessor();         // 中间件
```

CORS 策略在 `App.json` 中配置。

### 2.14 文件上传与虚拟文件系统

```csharp
// 获取文件类型提供器
FileExtensionContentTypeProvider contentTypeProvider = FS.GetFileExtensionContentTypeProvider();

app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = contentTypeProvider
});
```

---

## 三、SqlSugar ORM 框架详解

### 3.1 框架简介

[SqlSugar](https://www.donet5.com/) 是一个国产高性能 ORM 框架，支持 20+ 种数据库。本项目使用 SqlSugarCore **5.1.4.210** 版本。

核心依赖：
```xml
<PackageReference Include="SqlSugarCore" Version="5.1.4.210" />
```

项目在 `01-架构核心/Extras.DatabaseAccessor.SqlSugar/` 中封装了自定义的 SqlSugar 扩展层。

### 3.2 数据库连接配置

**配置文件：** `IotPlatform.Web.Core/Configuration/Database.json`

```json
{
  "ConnectionStrings": {
    "EnableConsoleSql": true,
    "ConnectionConfigs": [
      {
        "ConfigId": "1300000000001",
        "DBName": "FengCloudIotV5",
        "DBType": "PostgreSQL",
        "Host": "192.168.3.222",
        "Port": "5432",
        "UserName": "postgres",
        "Password": "Fh@201001.",
        "DBSchema": "public",
        "EnableInitTable": false,
        "EnableInitSeed": false
      },
      {
        "ConfigId": "16545203149510",
        "DBName": "CloudDataModeling",
        "DBType": "PostgreSQL",
        "Host": "192.168.3.222",
        "Port": "5432",
        "UserName": "postgres",
        "Password": "Fh@201001.",
        "DBSchema": "public",
        "EnableInitTable": false,
        "EnableInitSeed": false
      }
    ]
  }
}
```

**关键设计：**
- 支持**多数据库连接**，每个连接通过 `ConfigId` 区分
- `ConfigId = "1300000000001"` 为默认主库（`SqlSugarConst.MainConfigId`）
- 多个数据库满足不同场景：主业务库 + 数据分析库 + 租户专属库

**注册到 DI 容器：**

```csharp
// SqlSugarConfigureExtensions.cs
public static void SqlSugarConfigure(this IServiceCollection services)
{
    // 1. 自定义雪花 ID 生成
    StaticConfig.CustomSnowFlakeFunc = () => YitIdHelper.NextId();

    // 2. 读取配置
    ConnectionStringsOptions dbOptions = App.GetOptions<ConnectionStringsOptions>();
    dbOptions.ConnectionConfigs.ForEach(SetDbConfig);

    // 3. 创建 SqlSugarScope（单例）
    SqlSugarScope sqlSugar = new(dbOptions.ConnectionConfigs.Adapt<List<ConnectionConfig>>(), db =>
    {
        dbOptions.ConnectionConfigs.ForEach(config =>
        {
            SqlSugarScopeProvider dbProvider = db.GetConnectionScope(config.ConfigId);
            SetDbAop(dbProvider, dbOptions.EnableConsoleSql);  // 配置 AOP
        });
    });

    // 4. DI 注册
    services.AddSingleton<ISqlSugarClient>(sqlSugar);
    services.AddScoped(typeof(ISqlSugarRepository<>), typeof(SqlSugarRepository<>));
    services.AddUnitOfWork<SqlSugarUnitOfWork>();

    // 5. 初始化数据库表和种子数据
    dbOptions.ConnectionConfigs.ForEach(config => { InitDatabase(sqlSugar, config); });
}
```

**注意：** `ISqlSugarClient`（实际上是 `SqlSugarScope`）注册为**单例**，但仓储 `ISqlSugarRepository<T>` 注册为**作用域**。

### 3.3 实体定义规范

#### 基础实体继承体系

```
EntityBaseId              — 纯 ID（雪花ID）
  ├── EntityBase          — ID + 审计字段（创建/修改时间、创建/修改人）
  │     └── EntityTenant  — ID + 审计字段 + 租户ID
  └── EntityTenantId      — ID + 租户ID（轻量级租户实体）
```

```csharp
// EntityBaseId.cs — 所有实体的最底层基类
public abstract class EntityBaseId
{
    [SugarColumn(ColumnName = "Id", ColumnDescription = "主键Id",
                 IsPrimaryKey = true, IsIdentity = false)]
    public virtual long Id { get; set; }
}

// EntityBase.cs — 带审计字段
public abstract class EntityBase : EntityBaseId
{
    [SugarColumn(ColumnDescription = "创建时间", IsOnlyIgnoreUpdate = true)]
    public virtual DateTime? CreatedTime { get; set; }

    [SugarColumn(ColumnDescription = "更新时间", IsOnlyIgnoreInsert = true)]
    public virtual DateTime? UpdatedTime { get; set; }

    [SugarColumn(ColumnDescription = "创建者Id", IsOnlyIgnoreUpdate = true)]
    public virtual long? CreatedUserId { get; set; }

    [Navigate(NavigateType.OneToOne, nameof(CreatedUserId))]
    public virtual SysUser CreatedUser { get; set; }

    [SugarColumn(ColumnDescription = "创建者姓名", Length = 64, IsOnlyIgnoreUpdate = true)]
    public virtual string? CreatedUserName { get; set; }

    [SugarColumn(ColumnDescription = "修改者Id", IsOnlyIgnoreInsert = true)]
    public virtual long? UpdatedUserId { get; set; }

    [Navigate(NavigateType.OneToOne, nameof(UpdatedUserId))]
    public virtual SysUser UpdatedUser { get; set; }

    [SugarColumn(ColumnDescription = "修改者姓名", Length = 64, IsOnlyIgnoreInsert = true)]
    public virtual string? UpdatedUserName { get; set; }
}

// EntityTenant.cs — 租户隔离实体
public abstract class EntityTenant : EntityBase, ITenantIdFilter
{
    [SugarColumn(ColumnDescription = "租户Id")]
    public virtual long? TenantId { get; set; }
}
```

#### 具体实体示例

```csharp
// SysUser.cs — 系统用户
[SugarTable(null, "系统用户表")]   // null = 使用类名作表名，第二个参数是表注释
[SysTable]                            // 自定义标记：系统表
public class SysUser : EntityTenant    // 继承 EntityTenant = 自带审计 + 租户
{
    [SugarColumn(ColumnDescription = "账号", Length = 32)]
    [Required]
    [MaxLength(32)]
    public virtual string Account { get; set; }

    [SugarColumn(ColumnDescription = "密码", Length = 512)]
    [JsonIgnore]
    public virtual string Password { get; set; }

    [SugarColumn(ColumnDescription = "真实姓名", Length = 32)]
    public virtual string Name { get; set; }

    [SugarColumn(ColumnDescription = "账号类型")]
    public AdminTypeEnum AdminType { get; set; } = AdminTypeEnum.None;

    [SugarColumn(ColumnDescription = "直属机构Id")]
    public long OrgId { get; set; }

    // 导航属性 — 用于联表查询
    [Navigate(NavigateType.OneToOne, nameof(OrgId))]
    public SysOrg SysOrg { get; set; }
}
```

#### SugarColumn 常用配置

| 属性 | 说明 | 示例 |
|------|------|------|
| `ColumnName` | 列名 | `"Id"` |
| `ColumnDescription` | 列注释 | `"主键Id"` |
| `IsPrimaryKey` | 是否主键 | `true` |
| `IsIdentity` | 是否自增 | `false`（使用雪花ID） |
| `Length` | 字符串长度 | `32` |
| `IsNullable` | 是否可空 | 自动从 C# 可空类型推断 |
| `IsOnlyIgnoreInsert` | 新增时忽略 | `true`（如 UpdatedTime） |
| `IsOnlyIgnoreUpdate` | 更新时忽略 | `true`（如 CreatedTime） |
| `IsIgnore` | 完全忽略映射 | `true` |

#### Navigate 导航属性

```csharp
// 一对一：CreatedUserId → SysUser
[Navigate(NavigateType.OneToOne, nameof(CreatedUserId))]
public virtual SysUser CreatedUser { get; set; }

// 一对多（需在子表有外键）
[Navigate(NavigateType.OneToMany, nameof(SysMenu.Pid))]
public List<SysMenu> Children { get; set; }
```

### 3.4 仓储模式

项目封装了自定义的 `ISqlSugarRepository<T>`，继承自 `SimpleClient<T>`：

```csharp
// ISqlSugarRepository.cs
public partial interface ISqlSugarRepository<TEntity> : ISimpleClient<TEntity>
    where TEntity : class, new() { }

// SqlSugarRepository.cs
public class SqlSugarRepository<TEntity> : SimpleClient<TEntity>, ISqlSugarRepository<TEntity>
    where TEntity : class, new()
{
    public SqlSugarRepository(IServiceProvider serviceProvider, ISqlSugarClient context = null) : base(context)
    {
        Context = (SqlSugarScope)context;

        // 多租户：自动切换到当前租户的数据库连接
        string tenantId = connectionStrings.DefaultConnectionConfig.ConfigId.ToString();
        if (httpContext?.GetEndpoint()?.Metadata?.GetMetadata<AllowAnonymousAttribute>() == null)
        {
            Context = Context.AsTenant().GetConnectionScope(tenantId);
        }

        Context.Ado.CommandTimeOut = 30;
    }
}
```

**继承自 `SimpleClient<T>` 意味着直接拥有以下方法：**

| 方法 | 用途 |
|------|------|
| `GetListAsync()` | 获取全部列表 |
| `GetByIdAsync(id)` | 按主键获取 |
| `GetFirstAsync(exp)` | 获取第一个匹配项 |
| `InsertAsync(entity)` | 插入 |
| `UpdateAsync(entity)` | 更新 |
| `DeleteAsync(entity)` | 删除 |
| `IsAnyAsync(exp)` | 是否存在 |
| `AsQueryable()` | 获取查询构造器 |
| `AsInsertable(entity)` | 获取插入构造器 |
| `AsUpdateable(entity)` | 获取更新构造器 |
| `AsDeleteable()` | 获取删除构造器 |
| `AsSugarClient()` | 获取 SqlSugarClient 实例 |

**使用示例：**
```csharp
public class SysConfigService : IDynamicApiController, ITransient
{
    private readonly ISqlSugarRepository<SysConfig> _sysConfigRep;

    public SysConfigService(ISqlSugarRepository<SysConfig> sysConfigRep)
    {
        _sysConfigRep = sysConfigRep;
    }
    // ...使用 _sysConfigRep 进行数据库操作
}
```

### 3.5 查询操作大全

以下所有示例均来自本项目实际代码。

#### 3.5.1 基础查询

```csharp
// 获取全部
List<SysConfig> list = await _sysConfigRep.GetListAsync();

// 按 ID 获取
SysConfig config = await _sysConfigRep.GetFirstAsync(u => u.Id == input.Id);

// 是否存在
bool isExist = await _sysConfigRep.IsAnyAsync(u => u.Name == input.Name || u.Code == input.Code);

// 获取单个
SysUser user = await _sysUserRep.GetFirstAsync(u => u.Account.Equals(input.Account));

// 计数
int count = await _sysConfigRep.AsQueryable().CountAsync();
```

#### 3.5.2 条件过滤 — WhereIF

`WhereIF` 是 SqlSugar 的重要特性，条件为 true 时生效，**避免写大量 if-else**：

```csharp
return await _sysConfigRep.AsQueryable()
    .WhereIF(!string.IsNullOrWhiteSpace(input.Name?.Trim()), u => u.Name.Contains(input.Name))
    .WhereIF(!string.IsNullOrWhiteSpace(input.Code?.Trim()), u => u.Code.Contains(input.Code))
    .WhereIF(!string.IsNullOrWhiteSpace(input.GroupCode?.Trim()), u => u.GroupCode.Equals(input.GroupCode))
    .OrderByDescending(w => w.CreatedTime)
    .ToPagedListAsync(input.PageNo, input.PageSize);
```

#### 3.5.3 分页查询

```csharp
// 使用项目自定义的 SqlSugarPagedList
SqlSugarPagedList<SysConfig> result = await _sysConfigRep.AsQueryable()
    .Where(u => u.Status == 1)
    .OrderBy(u => u.CreatedTime, OrderByType.Desc)
    .ToPagedListAsync(pageNo, pageSize);

// 返回结果包含：
// result.Rows       — 当前页数据
// result.PageNo     — 当前页码
// result.PageSize   — 每页大小
// result.TotalRows  — 总记录数
// result.TotalPage  — 总页数
// result.HasPrevPage / result.HasNextPage
```

#### 3.5.4 联表查询（LEFT JOIN）

**两表联查：**
```csharp
// SysTenant + SysUser 联查
return await _sysTenantRep.AsQueryable()
    .LeftJoin<SysUser>((u, a) => u.UserId == a.Id)
    .WhereIF(!string.IsNullOrWhiteSpace(input.Phone), (u, a) => a.Phone.Contains(input.Phone.Trim()))
    .OrderBy(u => u.OrderNo)
    .Select((u, a) => new TenantOutput
    {
        Id = u.Id,
        UserName = a.Name,
        // ...
    })
    .ToPagedListAsync(input.PageNo, input.PageSize);
```

**三表联查：**
```csharp
// SysTenant + SysUser + SysOrg
return await _sysTenantRep.AsQueryable()
    .LeftJoin<SysUser>((u, a) => u.UserId == a.Id)
    .LeftJoin<SysOrg>((u, a, b) => u.OrgId == b.Id)
    .WhereIF(!string.IsNullOrWhiteSpace(input.Name), (u, a, b) => b.Name.Contains(input.Name.Trim()))
    .Select((u, a, b) => new TenantOutput { /* 三表字段 */ })
    .ToPagedListAsync(input.PageNo, input.PageSize);
```

#### 3.5.5 子查询（Subqueryable）

```csharp
// 在 Select 中使用子查询
SqlSugarPagedList<DataInterfaceListOutput> list = await _repository.AsSugarClient()
    .Queryable<DataInterfaceEntity>()
    .Select(a => new DataInterfaceListOutput
    {
        id = a.Id,
        fullName = a.FullName,
        // 子查询：获取创建者姓名
        creatorUser = SqlFunc.Subqueryable<SysUser>()
                        .Where(u => u.Id == a.CreatedUserId)
                        .Select(u => u.Name),
    })
    .ToPagedListAsync(page, pageSize);
```

#### 3.5.6 SqlFunc 条件函数

```csharp
// SqlFunc.IF — 条件判断（类似 SQL CASE WHEN）
.Select(a => new {
    type = SqlFunc.IF(a.Type == 1).Return("SQL操作")
                .ElseIF(a.Type == 2).Return("静态数据")
                .End("API操作")
})

// SqlFunc.IIF — 简化版条件（用于 Update SetColumns）
await _repository.AsUpdateable()
    .SetColumns(it => new DataInterfaceEntity
    {
        EnabledMark = SqlFunc.IIF(it.EnabledMark == 1, 0, 1),
        UpdatedTime = DateTime.Now,
    })
    .Where(it => it.Id == id)
    .ExecuteCommandHasChangeAsync();
```

#### 3.5.7 分组聚合

```csharp
// GroupBy + Select
List<string> groupCodes = await _sysConfigRep.AsQueryable()
    .GroupBy(u => u.GroupCode)
    .Select(u => u.GroupCode)
    .ToListAsync();

// Max 聚合
long? maxSortCode = await _repository.AsQueryable()
    .Where(w => w.CategoryId == input.categoryId)
    .MaxAsync(w => w.SortCode);
```

#### 3.5.8 包含导航属性（Includes）

```csharp
// 使用 Includes 预加载关联实体
SysUser user = await _sysUserRep.AsQueryable()
    .Includes(t => t.SysOrg)           // 加载机构信息
    .ClearFilter()                      // 清除全局过滤器（如租户过滤）
    .FirstAsync(u => u.Account.Equals(input.Account));
```

#### 3.5.9 跨实体查询（AsSugarClient）

```csharp
// 使用当前仓储的 SqlSugarClient 查询其他表
SysTenant tenant = await _sysUserRep.AsSugarClient()
    .Queryable<SysTenant>()
    .FirstAsync(u => u.Id == user.TenantId);

SysOrg org = await _sysUserRep.AsSugarClient()
    .Queryable<SysOrg>()
    .FirstAsync(u => u.Id == user.OrgId);
```

### 3.6 增删改操作

#### 新增

```csharp
// 基础插入
await _sysConfigRep.InsertAsync(input.Adapt<SysConfig>());

// 带忽略列的插入
int result = await _repository.AsInsertable(entity)
    .IgnoreColumns(true)  // 忽略值为 null 的列
    .ExecuteCommandAsync();

// 批量插入
await _sysConfigRep.InsertRangeAsync(entityList);
```

#### 更新

```csharp
// 基础更新（按实体）
await _sysConfigRep.AsUpdateable(config)
    .IgnoreColumns(true)
    .ExecuteCommandAsync();

// 指定列更新
await _sysTenantRep.AsUpdateable(tenant)
    .UpdateColumns(u => new { u.Status })
    .ExecuteCommandAsync();

// 表达式更新（无需先查询）
await _sysUserRep.UpdateAsync(
    u => new SysUser { Account = input.AdminAccount, Phone = input.Phone },
    u => u.Id == input.OrgId
);

// 条件更新（IIF）
await _repository.AsUpdateable()
    .SetColumns(it => new DataInterfaceEntity
    {
        EnabledMark = SqlFunc.IIF(it.EnabledMark == 1, 0, 1),
        UpdatedTime = DateTime.Now,
        UpdatedUserId = _userManager.UserId
    })
    .Where(it => it.Id == id)
    .ExecuteCommandHasChangeAsync();
```

#### 删除

```csharp
// 按实体删除
await _sysConfigRep.DeleteAsync(config);

// 按条件删除
await _sysTenantRep.DeleteAsync(u => u.Id == input.Id);

// 指定条件删除
bool isOk = await _repository.AsDeleteable()
    .Where(it => it.Id == id)
    .ExecuteCommandHasChangeAsync();
```

#### 批量插入或更新（Storageable/Upsert）

```csharp
// 按主键自动判断插入或更新
StorageableResult<DataInterfaceEntity> storResult =
    await _repository.AsSugarClient()
        .Storageable(data).Saveable().ToStorageAsync();
await storResult.AsInsertable.ExecuteCommandAsync();
await storResult.AsUpdateable.ExecuteCommandAsync();
```

### 3.7 事务与工作单元

项目使用 Furion 的 `[UnitOfWork]` 特性配合 SqlSugar 的事务支持。

**工作单元实现：**
```csharp
// SqlSugarUnitOfWork.cs
public sealed class SqlSugarUnitOfWork : IUnitOfWork
{
    private readonly ISqlSugarClient _sqlSugarClient;

    public SqlSugarUnitOfWork(ISqlSugarClient sqlSugarClient)
    {
        _sqlSugarClient = sqlSugarClient;
    }

    public void BeginTransaction(FilterContext context, UnitOfWorkAttribute unitOfWork)
    {
        _sqlSugarClient.AsTenant().BeginTran();
    }

    public void CommitTransaction(FilterContext resultContext, UnitOfWorkAttribute unitOfWork)
    {
        _sqlSugarClient.AsTenant().CommitTran();
    }

    public void RollbackTransaction(FilterContext resultContext, UnitOfWorkAttribute unitOfWork)
    {
        _sqlSugarClient.AsTenant().RollbackTran();
    }
}
```

**使用方式：**
```csharp
// 在方法上添加 [UnitOfWork] 特性，自动开启事务
[UnitOfWork]
[HttpPost("/order/create")]
public async Task CreateOrder(OrderInput input)
{
    await _orderRep.InsertAsync(order);
    await _orderItemRep.InsertRangeAsync(items);
    // 如果方法执行过程中抛出异常，事务自动回滚
}
```

### 3.8 多租户数据隔离

项目实现了三种租户隔离级别：

#### 级别 1：列过滤隔离（TenantId 列）

所有继承 `EntityTenant` 的实体自动被全局过滤器拦截：

```csharp
// 在 AOP 中配置的全局过滤器
db.QueryFilter.AddTableFilter<ITenantIdFilter>(
    u => u.TenantId == long.Parse(tenantId) || u.TenantId == null
);
```

效果：任何查询 `EntityTenant` 子类的操作，自动追加 `WHERE TenantId = @CurrentTenantId` 条件。

**注意事项：**
- 超管（`SuperAdmin`）自动排除过滤，可查看所有租户数据
- 需要使用 `ClearFilter()` 来临时移除过滤（如登录时）

```csharp
// 登录时需要跨租户查找用户
SysUser user = await _sysUserRep.AsQueryable()
    .ClearFilter()
    .FirstAsync(u => u.Account.Equals(input.Account));
```

#### 级别 2：数据库隔离

不同租户连接到不同的数据库（根据 `ConfigId`）：

```csharp
// SqlSugarRepository 构造函数中自动切换
Context = Context.AsTenant().GetConnectionScope(tenantId);
```

#### 级别 3：动态连接切换

通过 `DataBaseManager` 在运行时动态添加连接：

```csharp
public ISqlSugarClient GetTenantSqlSugarClient(string tenantId)
{
    if (!_sqlSugarClient.AsTenant().IsAnyConnection(tenantId))
    {
        _sqlSugarClient.AddConnection(new ConnectionConfig
        {
            ConfigId = tenantId,
            DbType = DbType.PostgreSQL,
            ConnectionString = connectionStr,
        });
    }
    _sqlSugarClient.ChangeDatabase(tenantId);
    return _sqlSugarClient;
}
```

#### 租户过滤器接口

```csharp
// IEntityFilter.cs
public interface ITenantIdFilter
{
    long? TenantId { get; set; }
}
```

### 3.9 AOP 拦截器

项目通过 SqlSugar AOP 实现了三个重要的横切关注点：

#### 1. 数据审计（自动填充字段）

```csharp
db.Aop.DataExecuting = (oldValue, entityInfo) =>
{
    if (entityInfo.OperationType == DataFilterType.InsertByObject)
    {
        // 主键自动生成雪花 ID（如果为 0 或 null）
        if (entityInfo.EntityColumnInfo.IsPrimarykey &&
            entityInfo.EntityColumnInfo.PropertyInfo.PropertyType == typeof(long))
        {
            var id = entityInfo.EntityColumnInfo.PropertyInfo.GetValue(entityInfo.EntityValue);
            if (id == null || (long)id == 0)
                entityInfo.SetValue(YitIdHelper.NextId());
        }

        // 自动设置创建时间
        if (entityInfo.PropertyName == "CreatedTime")
            entityInfo.SetValue(DateTime.Now);

        // 自动设置创建人（从 JWT Token 中获取）
        if (entityInfo.PropertyName == "CreatedUserId")
            entityInfo.SetValue(App.User.FindFirst(ClaimConst.UserId)?.Value);

        // 自动设置租户 ID
        if (entityInfo.PropertyName == "TenantId")
            entityInfo.SetValue(App.User?.FindFirst(ClaimConst.TenantId)?.Value);
    }

    if (entityInfo.OperationType == DataFilterType.UpdateByObject)
    {
        // 自动设置更新时间
        if (entityInfo.PropertyName == "UpdatedTime")
            entityInfo.SetValue(DateTime.Now);

        // 自动设置修改人
        if (entityInfo.PropertyName == "UpdatedUserId")
            entityInfo.SetValue(App.User?.FindFirst(ClaimConst.UserId)?.Value);
    }
};
```

**这意味着：** 开发者进行 Insert/Update 操作时，**无需手动设置** `CreatedTime`、`CreatedUserId`、`UpdatedTime`、`UpdatedUserId`、`TenantId` 等审计字段！

#### 2. SQL 日志输出

```csharp
db.Aop.OnLogExecuting = (sql, pars) =>
{
    // 按 SQL 类型使用不同颜色
    if (sql.StartsWith("SELECT")) Console.ForegroundColor = ConsoleColor.Green;
    if (sql.StartsWith("UPDATE") || sql.StartsWith("INSERT")) Console.ForegroundColor = ConsoleColor.Yellow;
    if (sql.StartsWith("DELETE")) Console.ForegroundColor = ConsoleColor.Red;

    Console.WriteLine($"【{DateTime.Now}——执行SQL】\r\n{UtilMethods.GetSqlString(config.DbType, sql, pars)}\r\n");
};

db.Aop.OnError = ex =>
{
    Console.ForegroundColor = ConsoleColor.DarkRed;
    Console.WriteLine($"【{DateTime.Now}——错误SQL】\r\n{UtilMethods.GetSqlString(config.DbType, ex.Sql, (SugarParameter[])ex.Parametres)}\r\n");
};
```

#### 3. 租户过滤器注入

```csharp
// 超管跳过所有租户过滤
if (App.User?.FindFirst(ClaimConst.AdminType)?.Value == ((int)AdminTypeEnum.SuperAdmin).ToString())
    return;

// 普通用户添加租户过滤
var tenantId = App.User?.FindFirst(ClaimConst.TenantId)?.Value;
if (!string.IsNullOrWhiteSpace(tenantId))
    db.QueryFilter.AddTableFilter<ITenantIdFilter>(
        u => u.TenantId == long.Parse(tenantId) || u.TenantId == null
    );
```

### 3.10 种子数据

项目提供了 `ISqlSugarEntitySeedData<T>` 接口用于初始化种子数据：

```csharp
// 接口定义
public interface ISqlSugarEntitySeedData<TEntity> where TEntity : class, new()
{
    IEnumerable<TEntity> HasData();
}

// 实现示例
public class SysTenantSeedData : ISqlSugarEntitySeedData<SysTenant>
{
    public IEnumerable<SysTenant> HasData()
    {
        return new[]
        {
            new SysTenant
            {
                Id = 1300000000001,
                UserId = 1300000000001,
                OrgId = 1300000000001,
                TenantType = TenantTypeEnum.Id,
                DbType = DbType.PostgreSQL,
                ConfigId = "1300000000001",
                // ...
            }
        };
    }
}
```

在 `InitDatabase()` 方法中通过反射自动发现并处理所有种子数据类。

### 3.11 分表支持

项目中使用 `[SplitTable]` 特性标记需要分表的实体，例如设备数据表可能按月分表：

```csharp
[SugarTable(null, "设备数据表")]
[SplitTable(SplitType.Month)]  // 按月分表
public class DeviceData : EntityBaseId
{
    public DateTime CreateTime { get; set; }
    public decimal Value { get; set; }
}
```

---

## 四、Furion + SqlSugar 集成实践

### 配置加载与数据库初始化流程

```
1. Furion Serve.Run() 启动
       │
2. AppStartup.ConfigureServices() 执行
       │
3. services.SqlSugarConfigure()
       │
       ▼
4. App.GetOptions<ConnectionStringsOptions>()  ← Furion 读取配置
       │
5. new SqlSugarScope(configs)                   ← SqlSugar 初始化
       │
6. SetDbConfig()                                ← 每库配置连接字符串、EntityService
       │
7. SetDbAop()                                   ← 配置日志、审计、租户过滤器
       │
8. InitDatabase()                                ← 自动建表 + 种子数据
       │
9. DI 注册：ISqlSugarClient (Singleton)
           ISqlSugarRepository<T> (Scoped)
           SqlSugarUnitOfWork
```

### 依赖关系图谱

```
API 请求
  └── Furion 路由 → IDynamicApiController 服务
        ├── 构造函数注入 ISqlSugarRepository<T>
        ├── 使用 .AsQueryable() / .InsertAsync() 等
        ├── AOP 自动处理审计字段 + 租户过滤
        └── [UnitOfWork] 处理事务

服务层内部：
  - Furion App.GetService<T>() 获取其他服务
  - Furion App.GetOptions<T>() 读取配置
  - Furion Oops.Oh() / Oops.Bah() 抛出业务异常
  - Furion JWTEncryption 处理 Token
```

---

## 五、开发快速上手

### 5.1 创建新业务模块

假设我们要创建一个 "设备巡检" 模块 `Inspection`。

**步骤 1：** 在 `02-应用模块/` 下创建项目：
```
02-应用模块/
  └── 12-Inspection/
        ├── Inspection.Entity/       # 实体和 DTO
        └── Inspection.Core/         # 业务服务
```

**步骤 2：** 在 `Inspection.Entity.csproj` 中添加引用：
```xml
<ProjectReference Include="..\..\..\01-架构核心\Extras.DatabaseAccessor.SqlSugar\Extras.DatabaseAccessor.SqlSugar.csproj" />
<ProjectReference Include="..\..\..\01-架构核心\IotPlatform.Core\IotPlatform.Core.csproj" />
```

**步骤 3：** 在 `Inspection.Core.csproj` 中添加引用：
```xml
<ProjectReference Include="..\Inspection.Entity\Inspection.Entity.csproj" />
```

**步骤 4：** 在 `IotPlatform.Web.Core.csproj` 中引用新模块：
```xml
<ProjectReference Include="..\..\02-应用模块\12-Inspection\Inspection.Core\Inspection.Core.csproj" />
```

### 5.2 创建新实体

```csharp
using Systems.Entity;

namespace Inspection.Entity;

/// <summary>
/// 巡检计划表
/// </summary>
[SugarTable(null, "巡检计划表")]
public class InspectionPlan : EntityTenant   // 继承 EntityTenant 获得 ID + 审计 + 租户
{
    [SugarColumn(ColumnDescription = "计划名称", Length = 128)]
    [Required]
    public string PlanName { get; set; }

    [SugarColumn(ColumnDescription = "计划类型")]
    public InspectionTypeEnum PlanType { get; set; }

    [SugarColumn(ColumnDescription = "开始日期")]
    public DateTime? StartDate { get; set; }

    [SugarColumn(ColumnDescription = "是否启用")]
    public bool IsEnabled { get; set; } = true;

    [SugarColumn(ColumnDescription = "负责部门Id")]
    public long? DeptId { get; set; }

    [Navigate(NavigateType.OneToOne, nameof(DeptId))]
    public SysOrg Dept { get; set; }
}
```

**继承选择指南：**

| 实体场景 | 继承基类 | 说明 |
|----------|----------|------|
| 需要完整审计 + 多租户 | `EntityTenant` | 最常见选择 |
| 需要审计但不需要租户 | `EntityBase` | 如系统配置表 |
| 只需要 ID + 租户 | `EntityTenantId` | 如关联表/中间表 |
| 只需要 ID | `EntityBaseId` | 如日志表 |
| 使用非 long 主键 | `EntityBase<TKey>` | 如 GUID 主键 |

### 5.3 创建新 API 服务

```csharp
using Extras.DatabaseAccessor.SqlSugar.Repositories;
using Inspection.Entity;

namespace Inspection.Core;

/// <summary>
/// 巡检计划服务
/// </summary>
[ApiDescriptionSettings("设备巡检", Order = 500)]
public class InspectionPlanService : IDynamicApiController, ITransient
{
    private readonly ISqlSugarRepository<InspectionPlan> _planRep;
    private readonly IUserManager _userManager;

    public InspectionPlanService(
        ISqlSugarRepository<InspectionPlan> planRep,
        IUserManager userManager)
    {
        _planRep = planRep;
        _userManager = userManager;
    }

    /// <summary>
    /// 获取巡检计划分页列表
    /// </summary>
    [HttpGet("/inspection/plan/page")]
    [DisplayName("获取巡检计划分页列表")]
    public async Task<SqlSugarPagedList<InspectionPlan>> GetPage(
        [FromQuery] PlanPageInput input)
    {
        return await _planRep.AsQueryable()
            .WhereIF(!string.IsNullOrWhiteSpace(input.PlanName),
                u => u.PlanName.Contains(input.PlanName))
            .WhereIF(input.PlanType.HasValue,
                u => u.PlanType == input.PlanType)
            .WhereIF(input.IsEnabled.HasValue,
                u => u.IsEnabled == input.IsEnabled)
            .OrderByDescending(u => u.CreatedTime)
            .ToPagedListAsync(input.PageNo, input.PageSize);
    }

    /// <summary>
    /// 新增巡检计划
    /// </summary>
    [HttpPost("/inspection/plan/add")]
    [DisplayName("新增巡检计划")]
    public async Task Add(PlanAddInput input)
    {
        // 检查名称是否已存在
        bool exist = await _planRep.IsAnyAsync(u => u.PlanName == input.PlanName);
        if (exist)
            throw Oops.Bah("巡检计划名称已存在");

        // 直接插入 — 审计字段由 AOP 自动处理
        await _planRep.InsertAsync(input.Adapt<InspectionPlan>());
    }

    /// <summary>
    /// 更新巡检计划
    /// </summary>
    [HttpPost("/inspection/plan/update")]
    [DisplayName("更新巡检计划")]
    public async Task Update(PlanUpdateInput input)
    {
        var plan = await _planRep.GetFirstAsync(u => u.Id == input.Id)
            ?? throw Oops.Bah("巡检计划不存在");

        // 名称唯一性校验（排除自身）
        bool exist = await _planRep.IsAnyAsync(
            u => u.PlanName == input.PlanName && u.Id != input.Id);
        if (exist)
            throw Oops.Bah("巡检计划名称已存在");

        var updated = input.Adapt(plan);  // Mapster 合并对象
        await _planRep.AsUpdateable(updated)
            .IgnoreColumns(true)
            .ExecuteCommandAsync();
    }

    /// <summary>
    /// 删除巡检计划
    /// </summary>
    [HttpPost("/inspection/plan/delete")]
    [DisplayName("删除巡检计划")]
    public async Task Delete([Required] long id)
    {
        var plan = await _planRep.GetFirstAsync(u => u.Id == id)
            ?? throw Oops.Bah("巡检计划不存在");
        await _planRep.DeleteAsync(plan);
    }

    /// <summary>
    /// 切换启用状态
    /// </summary>
    [HttpPost("/inspection/plan/toggle")]
    [DisplayName("切换启用状态")]
    public async Task ToggleStatus([Required] long id)
    {
        bool hasChange = await _planRep.AsUpdateable()
            .SetColumns(it => new InspectionPlan
            {
                IsEnabled = SqlFunc.IIF(it.IsEnabled, false, true),
                UpdatedTime = DateTime.Now
            })
            .Where(it => it.Id == id)
            .ExecuteCommandHasChangeAsync();

        if (!hasChange)
            throw Oops.Bah("巡检计划不存在");
    }
}
```

### 5.4 常见问题排查

#### Q1：新增 API 接口后 Swagger 不显示？

检查清单：
1. 服务类是否实现了 `IDynamicApiController` 和 `ITransient`（或 `IScoped`）？
2. 当前项目是否被 `IotPlatform.Web.Core` 引用（ProjectReference）？
3. 程序集是否在 `SingleFilePublish.IncludeAssemblyNames()` 中注册？
4. 方法是否被标记了 `[NonAction]`？

#### Q2：查询数据时租户过滤不正确？

排查步骤：
1. 确认实体是否继承了 `EntityTenant`（实现 `ITenantIdFilter`）
2. 如果不需要租户过滤，使用 `ClearFilter()`
3. 检查当前用户 JWT Token 中是否包含 `TenantId` Claim
4. 超管（`AdminType == SuperAdmin`）默认跳过所有过滤

#### Q3：新增实体后表没有自动创建？

1. 在实体类上添加 `[SugarTable]` 特性
2. 确认 `Database.json` 中对应该库的 `EnableInitTable` 是否为 `true`
3. 检查实体是否位于被 Furion 扫描的程序集中

#### Q4：雪花 ID 生成失败或重复？

1. 确认 `Startup.ConfigureServices()` 中调用了 `YitIdHelper.SetIdGenerator()`
2. 每个节点的 `WorkerId` 必须不同（分布式部署时）
3. `StaticConfig.CustomSnowFlakeFunc` 是否配置正确

#### Q5：AOP 审计字段没有自动填充？

1. 确认实体继承自 `EntityBase` 或 `EntityTenant`
2. 检查 AOP `DataExecuting` 中字段名是否与实体属性名完全匹配（区分大小写）
3. 确认当前请求上下文中 `App.User` 不为 null（即请求已通过 JWT 认证）

#### Q6：如何查看当前执行的 SQL 语句？

设置 `Database.json` → `ConnectionStrings.EnableConsoleSql` 为 `true`，然后查看控制台输出：
- 🟢 绿色：SELECT 语句
- 🟡 黄色：UPDATE/INSERT 语句
- 🔴 红色：DELETE 语句

#### Q7：Mapster 对象映射不生效？

```csharp
// 确保正确引入 Furion.Extras.ObjectMapper.Mapster
// 使用 .Adapt<T>() 进行映射
SysConfig config = input.Adapt<SysConfig>();

// 合并到已有对象
input.Adapt(existingConfig);

// 集合映射
List<Target> targets = sources.Adapt<List<Target>>();
```

#### Q8：Furion 配置不生效？

1. 确认配置文件放在 `Configuration/` 目录下
2. 确认 `appsettings.json` 中的 `ConfigurationScanDirectories` 包含 `"Configuration"`
3. 配置类是否使用 `[OptionsSettings]` 或实现 `IConfigurableOptions`？
4. 是否通过 `services.AddConfigurableOptions<T>()` 注册？

---

## 附录

### 附录 A：项目关键文件索引

| 文件 | 说明 |
|------|------|
| `IotPlatform/Program.cs` | 应用入口，Furion Serve.Run() |
| `IotPlatform/SingleFilePublish.cs` | 指定扫描的程序集 |
| `IotPlatform.Web.Core/Startup.cs` | Furion AppStartup，服务注册 |
| `IotPlatform.Web.Core/ProjectOptions.cs` | 配置选项注册 |
| `IotPlatform.Web.Core/Handlers/JwtHandler.cs` | JWT 认证处理器 |
| `IotPlatform.Web.Core/Extensions/SqlSugarConfigureExtensions.cs` | SqlSugar 配置 + AOP 设置 |
| `IotPlatform.Web.Core/Configuration/*.json` | 所有配置文件 |
| `Extras.DatabaseAccessor.SqlSugar/Repositories/SqlSugarRepository.cs` | 仓储实现 |
| `Extras.DatabaseAccessor.SqlSugar/Extensions/TenantLinkExtensions.cs` | 租户连接扩展 |
| `Extras.DatabaseAccessor.SqlSugar/Extensions/SqlSugarUnitOfWork.cs` | 工作单元实现 |
| `Extras.DatabaseAccessor.SqlSugar/Options/ConnectionStringsOptions.cs` | 连接配置选项 |
| `Systems.Entity/Entity/EntityBase.cs` | 实体基类体系 |
| `Systems.Entity/Entity/IEntityFilter.cs` | 租户过滤器接口 |

### 附录 B：NuGet 包版本参考

| 包名 | 版本 | 用途 |
|------|------|------|
| `Furion.Pure` | 4.9.8.18 | Furion 框架核心 |
| `Furion.Extras.Authentication.JwtBearer` | 4.9.8.18 | JWT 认证 |
| `Furion.Extras.ObjectMapper.Mapster` | 4.9.8.18 | 对象映射 |
| `SqlSugarCore` | 5.1.4.210 | SqlSugar ORM 核心 |
| `SqlSugar.TDengineCore` | 4.18.46 | TDengine 时序数据库 |
| `Yitter.IdGenerator` | — | 雪花 ID 生成器 |
| `NewLife.Caching` | — | Redis 缓存客户端 |
| `Newtonsoft.Json` | — | JSON 序列化（部分模块） |
| `NPOI` | — | Excel 导入导出 |

### 附录 C：常用开发命令

```bash
# 启动开发环境
dotnet run --project IotPlatform/IotPlatform.csproj --launch-profile Development

# 添加 NuGet 包（在对应项目目录下）
dotnet add package Furion.Pure --version 4.9.8.18

# 添加项目引用
dotnet add reference ../../01-架构核心/Extras.DatabaseAccessor.SqlSugar/Extras.DatabaseAccessor.SqlSugar.csproj

# 数据库迁移（启用 EnableInitTable 后自动执行）
# 或手动通过 CodeFirst 创建表
```

---

> **文档维护说明：** 如发现文档与代码不一致之处，请以实际代码为准并同步更新本文档。定期在项目结构变化后检查本文档的准确性。
