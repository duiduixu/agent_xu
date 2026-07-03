# 插件管理模块技术方案

## 一、项目背景与目标

### 1.1 背景

当前 IotPlatform v5 平台基于 .NET 8 + Furion 框架构建，采用模块化架构，通过 Furion 的 `AppStartup` 自动发现机制实现模块注册。随着平台应用场景的不断扩展（注塑、冲压、机加工、新能源等），需要一个统一、灵活、可扩展的插件管理模块来支撑：

- **通道协议**：不同工业设备使用的通信协议（MQTT、Modbus、Siemens S7、Omron Fins、Melsec MC 等），需支持按需挂载新的协议驱动。
- **后台服务**：面向特定行业/场景的数据处理、定时任务、规则引擎等后台逻辑，需支持动态安装与启停。
- **页面组件**：前端管理面板中的业务页面、看板、报表等 UI 组件，需支持动态注册路由与菜单。

### 1.2 目标

1. 建立统一的插件标准和生命周期管理（安装、启用、禁用、卸载）。
2. 支持三种插件类型（通道协议、后台服务、页面组件）的热插拔。
3. 将 `InjectionApsCpSatDemo`（注塑生产计划排程 Demo）封装为可插拨的插件集成到平台中。
4. 提供插件管理的 REST API 和管理界面。

---

## 二、现状分析

### 2.1 IotPlatform 现有架构

```
[独立 SPA 前端]  <-- HTTP / SignalR -->  [IotPlatform 主应用 :9081]
                                              |
                                              | gRPC (双向流)
                                              v
                                     [CollectionService :5100]
                                              |
                                +-------------+-------------+
                                |             |             |
                           [Modbus]    [Siemens S7]   [Omron Fins]
```

| 维度 | 技术栈 |
|------|--------|
| 运行时 | .NET 8 |
| 上层框架 | Furion 4.9.x（动态 API、AOP、统一结果、AppStartup 模块化） |
| ORM | SqlSugar（PostgreSQL 15，多租户分库） |
| 时序库 | TDengine 3.x |
| 缓存 | Redis（NewLife.Caching） |
| 实时通信 | SignalR（DeviceHub）+ gRPC |
| 认证 | JWT Bearer Token |
| 配置 | Furion 多 JSON 配置扫描 |
| 前端 | 独立 SPA（Vue.js/React），通过 REST API + WebSocket 与后端通信 |
| 后台服务 | 大量使用 `BackgroundService` / `IHostedService` 模式 |
| 模块化 | 60+ 个 .csproj，通过 Furion `AppStartup` 自动发现注册 |

### 2.2 InjectionApsCpSatDemo 分析

| 维度 | 现状 |
|------|------|
| 运行时 | .NET 9（需适配到 .NET 8） |
| 项目类型 | 控制台应用（单文件 Program.cs，556 行） |
| 核心依赖 | Google.OrTools 9.15（CP-SAT 约束规划求解器） |
| 业务领域 | 注塑机生产计划排程（APS: Advanced Planning & Scheduling） |
| 调度算法 | 两阶段：贪心启发式（Phase 1）+ CP-SAT 优化（Phase 2） |
| 约束模型 | 机台产能、模具互斥、换模/换料/换色 Setup 时间、颜色回溯惩罚、物料库存、交期 |
| 优化目标 | 最小化加权（拖期 + Makespan + Setup 成本 + 颜色回溯） |
| 数据层 | 无——全部为内存硬编码 Demo 数据 |
| 前端 | 无——纯 Console.WriteLine 输出 |
| DI | 无——手工构造函数注入（Poor Man's DI） |

**改造要点**：需从控制台 Demo 改造为符合 IotPlatform 插件规范的可部署组件，包括分层重构、DI 集成、动态 API 暴露、数据持久化、前端页面等。

---

## 三、总体架构设计

### 3.1 插件系统架构总览

```
┌──────────────────────────────────────────────────────────────────┐
│                      IotPlatform 主应用                           │
│                                                                  │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────────────┐   │
│  │ 插件管理 API  │  │ 插件管理器    │  │ 插件加载上下文        │   │
│  │ (动态API)    │  │ PluginManager │  │ PluginLoadContext    │   │
│  └──────┬───────┘  └──────┬───────┘  └──────────┬───────────┘   │
│         │                 │                      │               │
│         └─────────────────┼──────────────────────┘               │
│                           │                                      │
│                    ┌──────┴──────┐                               │
│                    │ 插件存储层   │                               │
│                    │ (PostgreSQL)│                               │
│                    └─────────────┘                               │
└──────────────────────────────────────────────────────────────────┘
                           │
          ┌────────────────┼────────────────┐
          │                │                │
   ┌──────┴──────┐ ┌──────┴──────┐ ┌──────┴──────┐
   │ 通道协议插件 │ │ 后台服务插件 │ │ 页面组件插件 │
   │             │ │             │ │             │
   │ - Modbus    │ │ - APS排程   │ │ - 排程看板  │
   │ - Siemens   │ │ - OEE统计   │ │ - 设备监控  │
   │ - Omron     │ │ - 数据清洗  │ │ - 报表面板  │
   │ - 自定义协议│ │ - 规则引擎  │ │ - 自定义页面│
   └─────────────┘ └─────────────┘ └─────────────┘
```

### 3.2 插件标准目录结构

```
Plugins/
├── Plugin.ApsScheduling/              # 注塑排程插件
│   ├── plugin.json                   # 插件清单文件（必需）
│   ├── Plugin.ApsScheduling.dll      # 插件主程序集
│   ├── Plugin.ApsScheduling.Core.dll # 插件核心逻辑
│   ├── Google.OrTools.dll            # 第三方依赖
│   └── frontend/                     # 前端静态资源
│       └── aps-scheduling.js         # 前端组件包
│
├── Plugin.ModbusTcp/                  # Modbus TCP 通道协议插件
│   ├── plugin.json
│   ├── Plugin.ModbusTcp.dll
│   └── ...
│
└── Plugin.CustomDashboard/            # 自定义看板页面插件
    ├── plugin.json
    ├── Plugin.CustomDashboard.dll
    └── frontend/
        └── custom-dashboard.js
```

### 3.3 插件清单文件（plugin.json）

```json
{
  "id": "com.iotplatform.plugin.aps-scheduling",
  "name": "注塑生产排程插件",
  "version": "1.0.0",
  "description": "基于 Google OR-Tools CP-SAT 的注塑机生产计划排程",
  "author": "IotPlatform Team",
  "type": "BackgroundService",
  "targetFramework": "net8.0",
  "assembly": "Plugin.ApsScheduling.dll",
  "startupClass": "Plugin.ApsScheduling.Startup",
  "dependencies": {
    "Google.OrTools": "9.15.6755"
  },
  "frontend": {
    "entry": "frontend/aps-scheduling.js",
    "routes": [
      {
        "path": "/aps/scheduling",
        "name": "ProductionScheduling",
        "component": "ApsSchedulingView",
        "meta": {
          "title": "生产排程",
          "icon": "schedule",
          "menuGroup": "生产管理"
        }
      },
      {
        "path": "/aps/scheduling/result",
        "name": "SchedulingResult",
        "component": "SchedulingResultView",
        "meta": {
          "title": "排程结果",
          "icon": "table",
          "hidden": true
        }
      }
    ]
  },
  "permissions": [
    "aps.scheduling.view",
    "aps.scheduling.execute",
    "aps.scheduling.config"
  ]
}
```

---

## 四、核心模块设计

### 4.1 插件契约（IPlugin 接口）

在 `01-架构核心/IotPlatform.Core` 中定义核心插件接口：

```csharp
// IotPlatform.Core/Plugins/IPlugin.cs
namespace IotPlatform.Core.Plugins;

/// <summary>
/// 插件基础接口，所有插件必须实现
/// </summary>
public interface IPlugin
{
    /// <summary>插件唯一标识</summary>
    string Id { get; }

    /// <summary>插件显示名称</summary>
    string Name { get; }

    /// <summary>插件版本</summary>
    string Version { get; }

    /// <summary>插件类型</summary>
    PluginType Type { get; }

    /// <summary>初始化插件（注册服务、配置等）</summary>
    Task InitializeAsync(PluginContext context);

    /// <summary>启动插件</summary>
    Task StartAsync(PluginContext context);

    /// <summary>停止插件</summary>
    Task StopAsync(PluginContext context);

    /// <summary>卸载插件</summary>
    Task UnloadAsync(PluginContext context);
}

/// <summary>
/// 插件类型枚举
/// </summary>
public enum PluginType
{
    /// <summary>通道协议插件</summary>
    ChannelProtocol = 1,

    /// <summary>后台服务插件</summary>
    BackgroundService = 2,

    /// <summary>页面组件插件</summary>
    PageComponent = 3
}

/// <summary>
/// 插件上下文（提供插件与主程序的交互能力）
/// </summary>
public class PluginContext
{
    /// <summary>插件配置（JSON 字符串）</summary>
    public string? Configuration { get; set; }

    /// <summary>IServiceCollection（仅初始化阶段可用）</summary>
    public IServiceCollection? Services { get; set; }

    /// <summary>IApplicationBuilder（仅启动阶段可用）</summary>
    public IApplicationBuilder? App { get; set; }

    /// <summary>插件工作目录</summary>
    public string PluginDirectory { get; set; } = default!;

    /// <summary>日志记录器</summary>
    public ILogger? Logger { get; set; }
}
```

### 4.2 通道协议插件接口

```csharp
// IotPlatform.Core/Plugins/IChannelProtocolPlugin.cs
namespace IotPlatform.Core.Plugins;

/// <summary>
/// 通道协议插件接口
/// </summary>
public interface IChannelProtocolPlugin : IPlugin
{
    /// <summary>协议名称（如 ModbusTcp, S7-1200）</summary>
    string ProtocolName { get; }

    /// <summary>支持的设备型号列表</summary>
    IReadOnlyList<string> SupportedModels { get; }

    /// <summary>连接设备</summary>
    Task<bool> ConnectAsync(string connectionString, CancellationToken cancellationToken);

    /// <summary>断开设备</summary>
    Task DisconnectAsync();

    /// <summary>读取变量值</summary>
    Task<Dictionary<string, object>> ReadVariablesAsync(
        IReadOnlyList<string> variableAddresses,
        CancellationToken cancellationToken);

    /// <summary>写入变量值</summary>
    Task<bool> WriteVariableAsync(
        string address, object value,
        CancellationToken cancellationToken);

    /// <summary>获取连接状态</summary>
    bool IsConnected { get; }
}
```

### 4.3 页面组件插件接口

```csharp
// IotPlatform.Core/Plugins/IPageComponentPlugin.cs
namespace IotPlatform.Core.Plugins;

/// <summary>
/// 页面组件插件接口（前端路由 + 菜单注册）
/// </summary>
public interface IPageComponentPlugin : IPlugin
{
    /// <summary>获取前端路由配置列表</summary>
    IReadOnlyList<PluginRoute> GetRoutes();

    /// <summary>获取前端静态资源路径</summary>
    string? FrontendEntryPath { get; }
}

/// <summary>
/// 前端路由定义
/// </summary>
public class PluginRoute
{
    /// <summary>路由路径</summary>
    public string Path { get; set; } = default!;

    /// <summary>路由名称</summary>
    public string Name { get; set; } = default!;

    /// <summary>前端组件名称</summary>
    public string Component { get; set; } = default!;

    /// <summary>元数据</summary>
    public PluginRouteMeta Meta { get; set; } = new();
}

public class PluginRouteMeta
{
    /// <summary>菜单标题</summary>
    public string Title { get; set; } = default!;

    /// <summary>菜单图标</summary>
    public string Icon { get; set; } = "appstore-add";

    /// <summary>菜单分组</summary>
    public string? MenuGroup { get; set; }

    /// <summary>排序号</summary>
    public int Order { get; set; }

    /// <summary>是否隐藏</summary>
    public bool Hidden { get; set; }

    /// <summary>所需权限码列表</summary>
    public string[]? Permissions { get; set; }
}
```

### 4.4 插件管理器（PluginManager）

```csharp
// IotPlatform.Web.Core/Plugins/PluginManager.cs
namespace IotPlatform.Web.Core.Plugins;

/// <summary>
/// 插件管理器 - 核心组件，负责插件的加载、启用、禁用、卸载
/// </summary>
public sealed class PluginManager : ISingleton
{
    private readonly ConcurrentDictionary<string, PluginEntry> _plugins = new();
    private readonly IPluginStore _store;
    private readonly ILogger<PluginManager> _logger;
    private readonly IServiceProvider _serviceProvider;

    /// <summary>获取所有已注册的插件</summary>
    public IReadOnlyCollection<PluginEntry> Plugins => _plugins.Values.ToList().AsReadOnly();

    /// <summary>
    /// 从 plugins 目录扫描并加载所有插件清单
    /// </summary>
    public async Task ScanPluginsAsync(string pluginsDirectory);

    /// <summary>
    /// 安装插件（从 .zip 包解压到 plugins 目录）
    /// </summary>
    public async Task<PluginEntry> InstallAsync(Stream packageStream);

    /// <summary>
    /// 启用指定插件
    /// </summary>
    public async Task EnableAsync(string pluginId);

    /// <summary>
    /// 禁用指定插件
    /// </summary>
    public async Task DisableAsync(string pluginId);

    /// <summary>
    /// 卸载指定插件
    /// </summary>
    public async Task UninstallAsync(string pluginId);

    /// <summary>
    /// 获取指定类型的所有已启用插件
    /// </summary>
    public IReadOnlyList<T> GetEnabledPlugins<T>() where T : IPlugin;
}

/// <summary>
/// 插件条目（运行时状态）
/// </summary>
public class PluginEntry
{
    public IPlugin Plugin { get; set; } = default!;
    public PluginManifest Manifest { get; set; } = default!;
    public PluginState State { get; set; }
    public AssemblyLoadContext? LoadContext { get; set; }
    public DateTime InstalledAt { get; set; }
}

public enum PluginState
{
    Installed,    // 已安装（未启用）
    Enabled,      // 已启用
    Disabled,     // 已禁用
    Error         // 错误状态
}
```

### 4.5 插件加载上下文（PluginLoadContext）

```csharp
// IotPlatform.Web.Core/Plugins/PluginLoadContext.cs
namespace IotPlatform.Web.Core.Plugins;

/// <summary>
/// 独立程序集加载上下文，实现插件程序集隔离
/// </summary>
internal sealed class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;
    private readonly AssemblyLoadContext _defaultContext;

    public PluginLoadContext(string pluginPath)
        : base(isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(pluginPath);
        _defaultContext = GetLoadContext(Assembly.GetExecutingAssembly())!;
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // 1. 尝试从插件目录加载
        var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        if (assemblyPath is not null)
        {
            return LoadFromAssemblyPath(assemblyPath);
        }

        // 2. 回退到默认上下文（共享框架程序集）
        // 避免加载重复的 ASP.NET Core / Furion 等框架程序集
        if (_defaultContext.Assemblies.Any(a =>
                AssemblyName.ReferenceMatchesDefinition(a.GetName(), assemblyName)))
        {
            return null; // 由默认上下文处理
        }

        return null;
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        if (libraryPath is not null)
        {
            return LoadUnmanagedDllFromPath(libraryPath);
        }
        return IntPtr.Zero;
    }
}
```

---

## 五、数据模型设计

### 5.1 数据库表结构（PostgreSQL）

```sql
-- 插件信息表
CREATE TABLE sys_plugin (
    id              BIGINT PRIMARY KEY,             -- 雪花 ID
    plugin_code     VARCHAR(128) NOT NULL UNIQUE,   -- 插件唯一编码（com.iotplatform.plugin.xxx）
    plugin_name     VARCHAR(128) NOT NULL,          -- 插件名称
    plugin_version  VARCHAR(32) NOT NULL,           -- 插件版本号
    plugin_type     INT NOT NULL,                   -- 1=通道协议, 2=后台服务, 3=页面组件
    description     VARCHAR(512),                   -- 插件描述
    author          VARCHAR(128),                   -- 作者
    assembly_name   VARCHAR(256) NOT NULL,          -- 主程序集文件名
    startup_class   VARCHAR(256),                   -- 启动类全名
    plugin_dir      VARCHAR(512) NOT NULL,          -- 插件目录路径
    frontend_entry  VARCHAR(512),                   -- 前端入口文件路径
    config_json     TEXT,                           -- 插件配置 JSON
    state           INT NOT NULL DEFAULT 1,         -- 0=已安装, 1=已启用, 2=已禁用, 3=错误
    priority        INT NOT NULL DEFAULT 0,         -- 加载优先级
    installed_at    TIMESTAMP NOT NULL,             -- 安装时间
    enabled_at      TIMESTAMP,                      -- 启用时间
    created_at      TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMP
);

-- 插件前端路由表
CREATE TABLE sys_plugin_route (
    id              BIGINT PRIMARY KEY,
    plugin_id       BIGINT NOT NULL REFERENCES sys_plugin(id),
    route_path      VARCHAR(256) NOT NULL,          -- 前端路由路径
    route_name      VARCHAR(128) NOT NULL,          -- 路由名称
    component_name  VARCHAR(256) NOT NULL,          -- 前端组件名
    menu_title      VARCHAR(64) NOT NULL,           -- 菜单标题
    menu_icon       VARCHAR(64) DEFAULT 'appstore-add',
    menu_group      VARCHAR(64),                    -- 菜单分组
    sort_order      INT DEFAULT 0,                  -- 排序号
    is_hidden       BOOLEAN DEFAULT FALSE,          -- 是否隐藏
    permission_code VARCHAR(256),                   -- 权限码（逗号分隔）
    created_at      TIMESTAMP NOT NULL DEFAULT NOW()
);

-- 插件权限表
CREATE TABLE sys_plugin_permission (
    id              BIGINT PRIMARY KEY,
    plugin_id       BIGINT NOT NULL REFERENCES sys_plugin(id),
    permission_code VARCHAR(256) NOT NULL,       -- 权限码
    permission_name VARCHAR(128) NOT NULL,       -- 权限名称
    created_at      TIMESTAMP NOT NULL DEFAULT NOW()
);

-- 索引
CREATE INDEX idx_plugin_type ON sys_plugin(plugin_type);
CREATE INDEX idx_plugin_state ON sys_plugin(state);
CREATE INDEX idx_plugin_route_plugin ON sys_plugin_route(plugin_id);
```

### 5.2 实体类（SqlSugar ORM 映射）

插件实体遵循现有项目的实体命名规范，放置在 `02-应用模块/00-Common/` 下或作为独立模块：

```csharp
// SysPlugin.cs
[SugarTable("sys_plugin")]
public class SysPlugin : EntityBase
{
    [SugarColumn(ColumnName = "plugin_code", Length = 128)]
    public string PluginCode { get; set; }

    [SugarColumn(ColumnName = "plugin_name", Length = 128)]
    public string PluginName { get; set; }

    [SugarColumn(ColumnName = "plugin_version", Length = 32)]
    public string PluginVersion { get; set; }

    [SugarColumn(ColumnName = "plugin_type")]
    public PluginType PluginType { get; set; }

    [SugarColumn(ColumnName = "description", Length = 512, IsNullable = true)]
    public string? Description { get; set; }

    [SugarColumn(ColumnName = "assembly_name", Length = 256)]
    public string AssemblyName { get; set; }

    [SugarColumn(ColumnName = "startup_class", Length = 256, IsNullable = true)]
    public string? StartupClass { get; set; }

    [SugarColumn(ColumnName = "plugin_dir", Length = 512)]
    public string PluginDir { get; set; }

    [SugarColumn(ColumnName = "frontend_entry", Length = 512, IsNullable = true)]
    public string? FrontendEntry { get; set; }

    [SugarColumn(ColumnName = "config_json", ColumnDataType = "TEXT", IsNullable = true)]
    public string? ConfigJson { get; set; }

    [SugarColumn(ColumnName = "state")]
    public PluginState State { get; set; }

    [SugarColumn(ColumnName = "priority")]
    public int Priority { get; set; }

    [SugarColumn(ColumnName = "installed_at")]
    public DateTime InstalledAt { get; set; }

    [SugarColumn(ColumnName = "enabled_at", IsNullable = true)]
    public DateTime? EnabledAt { get; set; }

    // 导航属性
    [Navigate(NavigateType.OneToMany, nameof(SysPluginRoute.PluginId))]
    public List<SysPluginRoute> Routes { get; set; }
}
```

---

## 六、API 设计

### 6.1 插件管理动态 API（Furion IDynamicApiController）

```csharp
// PluginAppService.cs
public class PluginAppService : IDynamicApiController
{
    /// <summary>获取插件列表</summary>
    [HttpGet("/api/plugins")]
    public Task<PagedList<PluginDto>> GetPlugins([FromQuery] PluginQueryInput input);

    /// <summary>获取插件详情</summary>
    [HttpGet("/api/plugins/{pluginId}")]
    public Task<PluginDto> GetPlugin(long pluginId);

    /// <summary>上传并安装插件</summary>
    [HttpPost("/api/plugins/install")]
    public Task<PluginDto> InstallPlugin([Required] IFormFile file);

    /// <summary>启用插件</summary>
    [HttpPost("/api/plugins/{pluginId}/enable")]
    public Task EnablePlugin(long pluginId);

    /// <summary>禁用插件</summary>
    [HttpPost("/api/plugins/{pluginId}/disable")]
    public Task DisablePlugin(long pluginId);

    /// <summary>卸载插件</summary>
    [HttpDelete("/api/plugins/{pluginId}")]
    public Task UninstallPlugin(long pluginId);

    /// <summary>更新插件配置</summary>
    [HttpPut("/api/plugins/{pluginId}/config")]
    public Task UpdatePluginConfig(long pluginId, [Required][FromBody] string configJson);

    /// <summary>获取已启用的前端路由列表（供前端动态注册）</summary>
    [HttpGet("/api/plugins/frontend-routes")]
    public Task<List<PluginRouteDto>> GetFrontendRoutes();
}
```

### 6.2 排程插件业务 API

```csharp
// ApsSchedulingAppService.cs（排程插件提供）
public class ApsSchedulingAppService : IDynamicApiController
{
    /// <summary>获取生产任务列表</summary>
    [HttpGet("/api/aps/jobs")]
    public Task<List<JobDto>> GetJobs([FromQuery] JobQueryInput input);

    /// <summary>获取机台列表</summary>
    [HttpGet("/api/aps/machines")]
    public Task<List<MachineDto>> GetMachines();

    /// <summary>获取模具列表</summary>
    [HttpGet("/api/aps/molds")]
    public Task<List<MoldDto>> GetMolds();

    /// <summary>执行排程计算</summary>
    [HttpPost("/api/aps/schedule")]
    public Task<ScheduleResultDto> RunScheduling([FromBody] SchedulingInput input);

    /// <summary>获取排程结果</summary>
    [HttpGet("/api/aps/schedules/{scheduleId}")]
    public Task<ScheduleResultDto> GetScheduleResult(long scheduleId);

    /// <summary>获取排程历史</summary>
    [HttpGet("/api/aps/schedules")]
    public Task<PagedList<ScheduleHistoryDto>> GetScheduleHistory([FromQuery] PageInput input);

    /// <summary>获取物料库存</summary>
    [HttpGet("/api/aps/material-stocks")]
    public Task<List<MaterialStockDto>> GetMaterialStocks();

    /// <summary>获取换模/换料/换色规则配置</summary>
    [HttpGet("/api/aps/setup-config")]
    public Task<SetupConfigDto> GetSetupConfig();

    /// <summary>更新换模/换料/换色规则配置</summary>
    [HttpPut("/api/aps/setup-config")]
    public Task UpdateSetupConfig([FromBody] SetupConfigDto config);

    /// <summary>导出排程结果为 Excel</summary>
    [HttpPost("/api/aps/schedules/{scheduleId}/export")]
    public Task<IActionResult> ExportSchedule(long scheduleId);
}
```

---

## 七、前端插件加载机制

### 7.1 前端插件架构

```
┌───────────────────────────────────────────────┐
│                IotPlatform SPA                 │
│                                               │
│  ┌─────────────────────────────────────────┐  │
│  │           插件路由注册器                  │  │
│  │   PluginRouteRegistry                   │  │
│  │   - 启动时从 /api/plugins/frontend-     │  │
│  │     routes 获取动态路由                  │  │
│  │   - 调用 router.addRoute() 动态注册     │  │
│  └─────────────────────────────────────────┘  │
│                    │                          │
│  ┌─────────────────┴───────────────────────┐  │
│  │           组件动态加载器                  │  │
│  │   ComponentLoader                       │  │
│  │   - 从插件静态资源 URL 动态加载 JS/CSS   │  │
│  │   - 注册为全局 Vue/React 组件            │  │
│  └─────────────────────────────────────────┘  │
│                                               │
│  ┌─────────────────────────────────────────┐  │
│  │           菜单动态构建器                  │  │
│  │   MenuBuilder                           │  │
│  │   - 合并静态菜单 + 插件注册的菜单项      │  │
│  └─────────────────────────────────────────┘  │
└───────────────────────────────────────────────┘
```

### 7.2 Vue 3 前端示例代码

```typescript
// src/plugins/plugin-loader.ts

interface PluginRoute {
  path: string;
  name: string;
  componentName: string;
  meta: {
    title: string;
    icon: string;
    menuGroup?: string;
    order: number;
    hidden: boolean;
    permissions?: string[];
  };
}

class PluginRouteRegistry {
  private loadedPlugins = new Map<string, boolean>();

  /**
   * 从后端获取已启用的插件路由并动态注册
   */
  async loadPluginRoutes(): Promise<void> {
    const routes = await http.get<PluginRoute[]>('/api/plugins/frontend-routes');

    for (const route of routes) {
      // 动态加载前端组件脚本
      await this.loadComponentScript(route.componentName);

      // 动态注册路由
      router.addRoute({
        path: route.path,
        name: route.name,
        component: () => import(/* @vite-ignore */ `/plugins/${route.componentName}.js`),
        meta: route.meta
      });

      // 动态注册菜单项
      this.registerMenuItem(route);
    }
  }

  /**
   * 动态加载插件的前端 JS 包
   */
  private async loadComponentScript(componentName: string): Promise<void> {
    if (this.loadedPlugins.has(componentName)) return;

    return new Promise((resolve, reject) => {
      const script = document.createElement('script');
      script.src = `/api/plugins/assets/${componentName}.js`;
      script.onload = () => {
        this.loadedPlugins.set(componentName, true);
        resolve();
      };
      script.onerror = reject;
      document.head.appendChild(script);
    });
  }

  /**
   * 动态注册菜单项到侧边栏
   */
  private registerMenuItem(route: PluginRoute): void {
    const menuStore = useMenuStore();
    menuStore.addMenuItem({
      path: route.path,
      title: route.meta.title,
      icon: route.meta.icon,
      group: route.meta.menuGroup || '插件',
      order: route.meta.order,
      hidden: route.meta.hidden,
      permissions: route.meta.permissions
    });
  }
}

// 在应用启动时调用
export function setupPlugins() {
  const registry = new PluginRouteRegistry();
  registry.loadPluginRoutes();
}
```

### 7.3 插件前端静态资源托管

```csharp
// Startup.cs 中配置插件静态文件中间件
public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
{
    // ... 现有中间件 ...

    // 为每个已启用的页面插件注册静态文件目录
    var pluginManager = app.ApplicationServices.GetRequiredService<PluginManager>();
    foreach (var plugin in pluginManager.GetEnabledPlugins<IPageComponentPlugin>())
    {
        var frontendDir = Path.Combine(plugin.PluginDirectory, "frontend");
        if (Directory.Exists(frontendDir))
        {
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(frontendDir),
                RequestPath = $"/plugins/{plugin.Id}/assets"
            });
        }
    }

    // ... MapControllers, MapHubs ...
}
```

---

## 八、InjectionApsCpSatDemo 插件化改造方案

### 8.1 改造策略

将单文件控制台 Demo 拆分为符合 IotPlatform 插件规范的多层项目结构：

```
Plugin.ApsScheduling/                          # 注塑排程插件
├── plugin.json                               # 插件清单
├── Plugin.ApsScheduling.csproj               # 插件主项目（.NET 8）
│
├── Domain/                                    # 领域层
│   ├── Job.cs                                # 生产任务实体
│   ├── Machine.cs                            # 机台实体
│   ├── Mold.cs                               # 模具实体
│   ├── Material.cs                           # 物料实体
│   ├── ScheduledOperation.cs                 # 调度结果
│   ├── SchedulingSettings.cs                 # 调度参数
│   ├── SetupConfig.cs                        # Setup 规则配置
│   └── ChangeRules.cs                        # 换模/换料/换色规则
│
├── Services/                                  # 业务服务层
│   ├── HeuristicScheduler.cs                 # Phase 1: 贪心启发式调度器
│   ├── CpSatOptimizer.cs                     # Phase 2: CP-SAT 优化调度器
│   ├── SetupCalculator.cs                    # Setup 时间计算器
│   ├── ObjectiveCalculator.cs                # 目标函数计算器
│   └── MaterialValidator.cs                  # 物料库存校验
│
├── Api/                                       # 动态 API 控制器
│   ├── ApsSchedulingAppService.cs            # 排程业务 API
│   └── Dtos/                                  # 数据传输对象
│       ├── JobDto.cs
│       ├── MachineDto.cs
│       ├── ScheduleResultDto.cs
│       └── SetupConfigDto.cs
│
├── Repository/                                # 数据访问层
│   ├── JobRepository.cs                      # 生产任务仓储
│   ├── MachineRepository.cs                  # 机台仓储
│   ├── ScheduleResultRepository.cs           # 排程结果仓储
│   └── SetupConfigRepository.cs              # Setup 配置仓储
│
├── PluginStartup.cs                           # 插件启动类（AppStartup）
│
└── frontend/                                  # 前端资源
    ├── aps-scheduling.js                     # 排程管理页面组件
    ├── scheduling-result.js                  # 排程结果甘特图组件
    └── components/
        ├── GanttChart.js                     # 甘特图可视化组件
        ├── JobForm.js                        # 任务编辑表单
        └── SetupConfigPanel.js               # Setup 规则配置面板
```

### 8.2 插件启动类（PluginStartup.cs）

```csharp
// Plugin.ApsScheduling/PluginStartup.cs
using Furion;
using Microsoft.Extensions.DependencyInjection;

namespace Plugin.ApsScheduling;

/// <summary>
/// 注塑排程插件启动类 - 遵循 Furion AppStartup 规范
/// </summary>
public class PluginStartup : AppStartup, IPlugin
{
    public string Id => "com.iotplatform.plugin.aps-scheduling";
    public string Name => "注塑生产排程";
    public string Version => "1.0.0";
    public PluginType Type => PluginType.BackgroundService;

    public override void ConfigureServices(IServiceCollection services)
    {
        // 注册调度服务
        services.AddSingleton<HeuristicScheduler>();
        services.AddSingleton<CpSatOptimizer>();
        services.AddSingleton<SetupCalculator>();
        services.AddSingleton<ObjectiveCalculator>();

        // 注册仓储
        services.AddScoped<IJobRepository, JobRepository>();
        services.AddScoped<IMachineRepository, MachineRepository>();
        services.AddScoped<IScheduleResultRepository, ScheduleResultRepository>();
        services.AddScoped<ISetupConfigRepository, SetupConfigRepository>();

        // 注册动态 API
        services.AddScoped<ApsSchedulingAppService>();

        // 将调度器注册为后台服务（异步排程任务队列）
        services.AddHostedService<SchedulingBackgroundService>();
    }

    public Task InitializeAsync(PluginContext context)
    {
        // 初始化数据库表（首次安装时）
        // 加载默认配置
        return Task.CompletedTask;
    }

    public Task StartAsync(PluginContext context)
    {
        // 插件启动逻辑（预热调度器、加载缓存等）
        return Task.CompletedTask;
    }

    public Task StopAsync(PluginContext context)
    {
        // 停止正在进行的排程任务
        return Task.CompletedTask;
    }

    public Task UnloadAsync(PluginContext context)
    {
        // 清理资源
        return Task.CompletedTask;
    }
}
```

### 8.3 核心调度器改造

将 `CpSatInjectionScheduler` 从单文件提取并适配 DI 模式：

```csharp
// Plugin.ApsScheduling/Services/CpSatOptimizer.cs
namespace Plugin.ApsScheduling.Services;

/// <summary>
/// CP-SAT 优化调度器（改造自 InjectionApsCpSatDemo）
/// </summary>
public sealed class CpSatOptimizer
{
    private readonly ILogger<CpSatOptimizer> _logger;

    public CpSatOptimizer(ILogger<CpSatOptimizer> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 使用 OR-Tools CP-SAT 求解器优化排程
    /// </summary>
    /// <param name="jobs">待排程的生产任务列表</param>
    /// <param name="machines">可用机台列表</param>
    /// <param name="materialStocks">物料库存字典</param>
    /// <param name="settings">调度参数（权重等）</param>
    /// <param name="setupConfig">换线规则配置</param>
    /// <param name="hint">启发式初始解（可选）</param>
    /// <param name="timeoutSeconds">求解超时时间（秒）</param>
    /// <returns>优化后的排程结果</returns>
    public ScheduleResult Optimize(
        IReadOnlyList<Job> jobs,
        IReadOnlyList<Machine> machines,
        IReadOnlyDictionary<string, int> materialStocks,
        SchedulingSettings settings,
        SetupConfig setupConfig,
        ScheduleResult? hint = null,
        int timeoutSeconds = 30)
    {
        // 1. 物料库存校验
        MaterialValidator.Validate(jobs, materialStocks);

        // 2. 构建 CP-SAT 模型（基于原有算法）
        var model = new CpModel();
        // ... 构建决策变量、约束、目标函数 ...
        // （保持原有核心算法逻辑不变）

        // 3. 注入启发式初始解
        if (hint is not null)
        {
            AddHeuristicHint(model, hint);
        }

        // 4. 求解
        var solver = new CpSolver
        {
            StringParameters = $"max_time_in_seconds:{timeoutSeconds}," +
                               "num_search_workers:8," +
                               "log_search_progress:false"
        };

        var status = solver.Solve(model);
        _logger.LogInformation("APS scheduling completed. Status: {Status}, " +
                               "Objective: {Objective}",
                               status, solver.ObjectiveValue);

        // 5. 解析结果
        return ExtractResult(jobs, machines, solver, status);
    }

    // ... 其他私有方法 ...
}
```

### 8.4 数据库表设计（排程业务）

```sql
-- 生产任务表
CREATE TABLE aps_job (
    id              BIGINT PRIMARY KEY,
    job_code        VARCHAR(64) NOT NULL,          -- 任务编码
    material_code   VARCHAR(64) NOT NULL,          -- 物料编码
    color_name      VARCHAR(64),                   -- 颜色名称
    color_level     INT DEFAULT 0,                 -- 颜色等级
    mold_code       VARCHAR(64) NOT NULL,          -- 模具编码
    duration_min    INT NOT NULL,                  -- 加工时长（分钟）
    material_qty    INT NOT NULL,                  -- 物料需求量
    due_time        TIMESTAMP NOT NULL,            -- 交期
    eligible_machines TEXT,                        -- 可用机台（JSON 数组）
    status          INT DEFAULT 0,                 -- 0=待排程, 1=已排程, 2=生产中, 3=已完成
    tenant_id       BIGINT,
    created_at      TIMESTAMP NOT NULL DEFAULT NOW()
);

-- 机台表
CREATE TABLE aps_machine (
    id              BIGINT PRIMARY KEY,
    machine_code    VARCHAR(64) NOT NULL,          -- 机台编码
    machine_name    VARCHAR(128),                  -- 机台名称
    group_name      VARCHAR(64),                   -- 机台组（吨位等级）
    status          INT DEFAULT 1,                 -- 0=不可用, 1=可用
    tenant_id       BIGINT,
    created_at      TIMESTAMP NOT NULL DEFAULT NOW()
);

-- 排程结果表
CREATE TABLE aps_schedule_result (
    id              BIGINT PRIMARY KEY,
    schedule_code   VARCHAR(64) NOT NULL,          -- 排程编号
    schedule_name   VARCHAR(128),                  -- 排程名称
    solver_status   VARCHAR(32),                   -- OPTIMAL / FEASIBLE / TIMEOUT
    objective_value DOUBLE PRECISION,              -- 目标函数值
    total_tardiness INT DEFAULT 0,                 -- 总拖期（分钟）
    makespan        INT,                            -- 最大完工时间
    detail_json     TEXT,                           -- 详细结果（JSON）
    created_at      TIMESTAMP NOT NULL DEFAULT NOW()
);

-- 排程明细表
CREATE TABLE aps_schedule_detail (
    id              BIGINT PRIMARY KEY,
    schedule_id     BIGINT NOT NULL REFERENCES aps_schedule_result(id),
    job_id          BIGINT NOT NULL,
    machine_id      BIGINT NOT NULL,
    start_time      TIMESTAMP NOT NULL,            -- 计划开始时间
    end_time        TIMESTAMP NOT NULL,            -- 计划结束时间
    tardiness_min   INT DEFAULT 0,                 -- 拖期分钟数
    setup_min       INT DEFAULT 0,                 -- Setup 时间
    created_at      TIMESTAMP NOT NULL DEFAULT NOW()
);
```

### 8.5 前端页面设计

| 页面 | 路由 | 功能 |
|------|------|------|
| 生产任务管理 | `/aps/jobs` | 任务的 CRUD、导入/导出、状态管理 |
| 机台管理 | `/aps/machines` | 机台信息维护、产能设定 |
| 排程执行 | `/aps/scheduling` | 选择任务、设置参数、触发排程计算 |
| 排程结果 | `/aps/scheduling/result` | 甘特图展示、拖拽调整、导出 Excel |
| Setup 规则配置 | `/aps/setup-config` | 换模/换料/换色规则矩阵配置 |
| 物料库存 | `/aps/material-stocks` | 物料库存查看与更新 |

排程结果页面建议使用 **ECharts 甘特图** 或 **dhtmlx-gantt** 进行可视化展示。

---

## 九、插件生命周期管理

### 9.1 完整生命周期流程

```
┌──────────┐    ┌──────────┐    ┌──────────┐    ┌──────────┐    ┌───────────┐
│  上传安装  │ -> │   启用    │ -> │   运行    │ -> │   禁用    │ -> │   卸载     │
│ Install  │    │ Enable   │    │ Running  │    │ Disable  │    │ Uninstall │
└──────────┘    └──────────┘    └──────────┘    └──────────┘    └───────────┘
     │               │               │               │               │
     v               v               v               v               v
 ┌───────┐      ┌───────┐      ┌───────┐      ┌───────┐      ┌───────┐
 │ 解压包 │      │加载程序集│     │执行逻辑│      │释放资源│      │删除文件│
 │ 注册DB│      │初始化服务│     │       │      │停止服务│      │清理DB │
 │ 校验签名│     │注册路由 │      │       │      │卸载程序集│     │       │
 └───────┘      └───────┘      └───────┘      └───────┘      └───────┘
```

### 9.2 应用启动时的插件加载流程

```csharp
// Program.cs 中集成插件加载
Serve.Run(RunOptions.Default
    .AddWebComponent<WebComponent>()
    .ConfigureServices((builder, services) =>
    {
        // 注册插件管理器为单例
        services.AddSingleton<PluginManager>();
        services.AddSingleton<IPluginStore, PluginStore>();
    })
    .Configure(app =>
    {
        // 应用启动后自动扫描并加载已启用的插件
        var lifetime = app.ApplicationServices.GetRequiredService<IHostApplicationLifetime>();
        lifetime.ApplicationStarted.Register(async () =>
        {
            var pluginManager = app.ApplicationServices.GetRequiredService<PluginManager>();
            var pluginsDir = Path.Combine(AppContext.BaseDirectory, "Plugins");

            await pluginManager.ScanPluginsAsync(pluginsDir);

            // 自动启用标记为 Enabled 的插件
            foreach (var plugin in pluginManager.Plugins
                         .Where(p => p.State == PluginState.Enabled))
            {
                await pluginManager.EnableAsync(plugin.Manifest.Id);
            }
        });
    }));
```

---

## 十、安全性设计

### 10.1 插件签名验证

```csharp
// 安装插件时验证数字签名
public async Task<PluginEntry> InstallAsync(Stream packageStream)
{
    // 1. 验证插件包的 RSA 签名
    if (!await VerifySignatureAsync(packageStream))
    {
        throw new InvalidOperationException("插件包签名验证失败");
    }

    // 2. 检查插件 ID 是否冲突
    // 3. 检查依赖项兼容性
    // 4. 解压到隔离目录
    // 5. 注册到数据库
}
```

### 10.2 权限控制

- 插件管理操作（安装/卸载/启用/禁用）需要 `plugin.manage` 权限。
- 每个插件可定义自己的权限码，与平台现有的 JWT + 权限系统集成。
- 通道协议插件的设备连接操作需校验设备归属权限。

### 10.3 程序集隔离

- 每个插件使用独立的 `PluginLoadContext`，基于 `AssemblyLoadContext`（`isCollectible: true`）。
- 框架共用程序集（Furion、ASP.NET Core、SqlSugar 等）回退到默认加载上下文。
- 第三方依赖从插件本地目录优先加载，避免版本冲突。

---

## 十一、实施计划

| 阶段 | 内容 | 预计工期 |
|------|------|----------|
| **Phase 1** | 核心基础设施 | 5 天 |
| | - 定义插件接口（IPlugin、IPluginManager） | |
| | - 实现 PluginManager + PluginLoadContext | |
| | - 创建数据库表（sys_plugin 系列） | |
| | - 实现插件管理动态 API | |
| **Phase 2** | APS 排程插件改造 | 8 天 |
| | - 将 Demo 代码拆分为多层架构 | |
| | - 适配 .NET 8 + Furion 框架 | |
| | - 实现数据持久化（PostgreSQL + SqlSugar） | |
| | - 实现排程业务 API | |
| | - 开发前端甘特图页面 | |
| **Phase 3** | 前端插件框架 | 5 天 |
| | - 实现前端 PluginRouteRegistry | |
| | - 实现前端组件动态加载器 | |
| | - 实现菜单动态注册 | |
| | - 适配现有前端框架 | |
| **Phase 4** | 通道协议插件化 | 5 天 |
| | - 将现有 PLC 驱动（Modbus、Siemens 等）适配为插件格式 | |
| | - 实现通道协议管理界面 | |
| **Phase 5** | 测试与文档 | 3 天 |
| | - 集成测试 | |
| | - 插件开发指南文档 | |
| | - 性能测试与优化 | |
| **合计** | | **约 26 天** |

---

## 十二、技术选型对比

### 12.1 插件加载方式对比

| 方案 | 优点 | 缺点 | 选择 |
|------|------|------|------|
| **AssemblyLoadContext** | 真正的程序集隔离；支持卸载回收；.NET 官方推荐 | 跨插件通信需接口抽象；调试稍复杂 | ✅ **推荐** |
| MEF (System.Composition) | 微软官方 DI 友好的插件框架 | .NET 8+ 社区不活跃；不支持卸载 | ❌ |
| Natasha 动态编译 | 脚本级灵活度 | 安全性差；编译性能开销 | ❌ |
| 编译时引用 | 简单直接 | 无热插拔；耦合紧密 | ❌ 仅用于核心模块 |

### 12.2 前端微前端方案对比

| 方案 | 优点 | 缺点 | 选择 |
|------|------|------|------|
| **动态 Script + 路由注册** | 实现简单；对现有代码侵入小 | 需约定组件接口；版本管理需手动处理 | ✅ **推荐**（轻量场景） |
| Module Federation | 标准化；支持依赖共享 | 需 Webpack 5；配置复杂；对 Vite 支持不完善 | 备选 |
| qiankun / micro-app | 成熟的微前端框架 | 引入太重；子应用需独立部署 | ❌ |
| iframe 隔离 | 最强隔离 | 通信复杂；体验差 | ❌ |

---

## 十三、风险与应对

| 风险 | 影响 | 应对措施 |
|------|------|----------|
| .NET 9 → .NET 8 降级兼容 | Google.OrTools 9.15 可能需要适配 | 验证 Google.OrTools 在 .NET 8 上的可用性；必要时使用 netstandard2.0 版本 |
| 插件卸载导致内存泄漏 | 长期运行后内存增长 | 使用 `AssemblyLoadContext.Unload()` + GC 回收；添加监控指标 |
| 插件版本兼容性冲突 | 不同插件依赖不同版本的同一库 | PluginLoadContext 优先从本地加载；版本冲突检测 |
| 前端插件与宿主框架版本不兼容 | 前端组件加载失败 | 约定前端组件接口规范；版本号校验 |
| CP-SAT 求解耗时过长 | 排程请求超时 | 异步后台执行 + 进度通知；支持超时配置；提供启发式结果兜底 |

---

## 十四、附录

### A. 插件开发模板

提供插件开发脚手架模板（`dotnet new` 模板或脚本），包含：

```
PluginTemplate/
├── plugin.json              # 清单模板
├── Plugin.Template.csproj   # 项目文件模板
├── PluginStartup.cs         # 启动类模板
├── Api/
│   └── SampleAppService.cs  # 示例 API
├── Models/
│   └── SampleEntity.cs      # 示例实体
└── frontend/
    └── sample-component.js  # 示例前端组件
```

### B. 参考资源

- [Furion 框架文档](https://furion.net/)
- [.NET AssemblyLoadContext 官方文档](https://learn.microsoft.com/en-us/dotnet/core/dependency-loading/understanding-assemblyloadcontext)
- [Google OR-Tools .NET 参考](https://developers.google.com/optimization/reference/dotnet)
- [Vue Router 动态路由](https://router.vuejs.org/guide/advanced/dynamic-routing.html)
- [ECharts 甘特图示例](https://echarts.apache.org/examples/)
