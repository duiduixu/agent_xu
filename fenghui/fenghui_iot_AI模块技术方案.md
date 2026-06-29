# IoT Platform AI 模块技术方案

---

## 文档信息

| 项目 | 内容 |
|------|------|
| 文档版本 | v1.0 |
| 创建日期 | 2026-06-29 |
| 适用项目 | IotPlatform (.NET 8 + Furion) |
| 目标读者 | 后端开发人员 |

---

## 目录

1. [当前架构概述](#1-当前架构概述)
2. [AI 模块总体设计](#2-ai-模块总体设计)
3. [核心功能设计](#3-核心功能设计)
   - [3.1 AI 聊天面板与多轮对话](#31-ai-聊天面板与多轮对话)
   - [3.2 自然语言查询与智能 API 调用](#32-自然语言查询与智能-api-调用)
   - [3.3 AI 自动创建表单](#33-ai-自动创建表单)
4. [多智能体架构设计（MAF）](#4-多智能体架构设计maf)
5. [详细实施步骤](#5-详细实施步骤)
6. [与现有框架解耦设计](#6-与现有框架解耦设计)
7. [项目结构与文件清单](#7-项目结构与文件清单)
8. [数据库设计](#8-数据库设计)
9. [部署与配置](#9-部署与配置)
10. [附录](#10-附录)

---

## 1. 当前架构概述

### 1.1 技术栈

| 层次 | 技术 | 说明 |
|------|------|------|
| 应用框架 | **Furion 4.9.8.18** | 基于 .NET 8 的 Web 应用框架 |
| ORM | **SqlSugar** | 关系数据库访问，通过 `Extras.DatabaseAccessor.SqlSugar` 集成 |
| 关系数据库 | **PostgreSQL** | 业务数据存储（通过 SqlSugar） |
| 时序数据库 | **TDengine** | 设备时序数据存储（通过 `Extras.TDengine` 模块） |
| 缓存 | **Redis (NewLife.Redis)** | 分布式缓存与消息队列 |
| 消息队列 | **EventBus + Redis** | 事件总线 |
| 实时通信 | **SignalR** | 前后端实时推送 |
| RPC | **gRPC** | 采集服务与主服务的通信 |

### 1.2 项目分层结构

```
📦 IotPlatform 解决方案
├── 📂 01-架构核心 (Architecture Core)
│   ├── IotPlatform.Core              # 核心框架封装（Furion 扩展）
│   ├── Extras.DatabaseAccessor.SqlSugar  # SqlSugar ORM 封装（PostgreSQL）
│   ├── Extras.TDengine                # TDengine 时序数据库客户端
│   ├── Extras.MQTT                    # MQTT 协议支持
│   └── Extras.Thridparty              # 第三方集成
│
├── 📂 02-应用模块 (Application Modules)
│   ├── 00-Common/Common.Core          # 公共基础设施（EventBus/Job/Logging）
│   ├── 01-OAuth                       # 认证授权
│   ├── 02-System                      # 系统管理
│   ├── 03-BusApp/IotPlatform.Application  # 业务应用层
│   ├── 04-DataWeaving                 # 数据编织
│   ├── 05-Message                     # 消息模块
│   ├── 06-Task                        # 任务调度/工作流/程序块
│   ├── 07-Thing/01-Warning            # 报警管理与统计
│   ├── 07-Thing/02-StatisticalRule    # 统计规则
│   ├── 08-VisualData                  # 可视化数据
│   ├── 09-Engine/VisualDev.Engine     # 可视化开发引擎（模板解析）
│   ├── 09-Engine/JsScript.Engine      # JavaScript 脚本引擎
│   ├── 10-VisualDev/VisualDev         # 可视化表单/列表设计器服务
│   ├── 10-VisualDev/VisualDev.Entity  # 可视化开发实体/DTO
│   └── 11-Extend                      # 扩展模块
│
└── 📂 03-应用服务 (Application Services)
    ├── IotPlatform                    # 主 Web 应用入口
    ├── IotPlatform.Web.Core           # Web 核心配置层（Startup/Services）
    └── IotPlatform.CollectionService  # 设备数据采集服务
```

### 1.3 现有服务注册模式

项目使用 Furion 框架的声明式服务注册：

```csharp
// 单例服务
public class MyService : ISingleton { }

// 瞬时服务
public class MyService : ITransient { }

// 作用域服务
public class MyService : IScoped { }

// 动态 API 控制器（自动生成 RESTful 接口）
[ApiDescriptionSettings("分组名", Tag = "TagName", Order = 100)]
[Route("api/prefix/")]
public class MyService : IDynamicApiController, ITransient
{
    [HttpGet("action")]
    public async Task<dynamic> Get() { ... }
}
```

### 1.4 表单设计器核心数据结构

当前 VisualDev 模块通过 `VisualDevEntity`（表 `BASE_VISUAL_DEV`）管理表单，关键字段：

| 字段 | 类型 | 说明 |
|------|------|------|
| `F_FORM_DATA` | JSON (大文本) | 表单设计器生成的完整配置 JSON，包含字段列表、布局等 |
| `F_COLUMN_DATA` | JSON (大文本) | 列表视图配置 JSON，包含列定义、搜索字段 |
| `F_TABLES_DATA` | JSON (大文本) | 数据表结构定义 JSON，包含主表/子表/字段映射 |
| `F_WEB_TYPE` | int | 页面类型：1=纯表单, 2=表单+列表, 4=数据视图 |

**`F_FORM_DATA` JSON 结构示例（关键字段）**：
```json
{
  "fields": [
    {
      "__vModel__": "deviceName",        // 字段绑定值名
      "__config__": {
        "jnpfKey": "input",              // 控件类型
        "label": "设备名称",              // 显示标签
        "tableName": "mt_xxx",           // 所属表名
        "children": [...],               // 子表控件嵌套
        "required": true                  // 是否必填
      }
    }
  ],
  "primaryKeyPolicy": 1                  // 主键策略：1=雪花ID, 2=自增
}
```

**`F_TABLES_DATA` JSON 结构示例**：
```json
[
  {
    "table": "mt_xxx",       // 表名
    "tableName": "设备台账",  // 表描述
    "typeId": "1",            // 1=主表, 0=子表
    "fields": [
      {
        "field": "f_id",            // 数据库字段名
        "fieldName": "主键",        // 字段描述
        "dataType": "varchar",      // 数据类型
        "DataLength": 50,
        "PrimaryKey": true
      },
      {
        "field": "deviceName",
        "fieldName": "设备名称",
        "dataType": "varchar",
        "DataLength": 500
      }
    ]
  }
]
```

> **关键发现**：表单的控件类型通过 `jnpfKey` 标识（如 `input`、`numInput`、`date`、`select` 等），在前端表单设计器代码中定义了完整的控件类型清单。详见 [附录 B](#附录-b-控件类型-jnpfkey-映射表)。

---

## 2. AI 模块总体设计

### 2.1 设计原则

1. **松耦合**：AI 模块作为独立类库项目，只依赖 `IotPlatform.Core` 和 `VisualDev`、`Thing.Warning` 等已有模块的接口/服务，不修改现有模块代码
2. **可拔插**：通过 Furion 的 `AppStartup` 机制注入，可通过配置开关 AI 功能
3. **多智能体编排**：使用 MAF (Multi-Agent Framework) 模式实现多智能体协作
4. **流式优先**：AI 响应默认采用 SSE (Server-Sent Events) 流式传输
5. **数据安全**：LLM 调用不直接暴露数据库连接字符串，通过受控的 API/SDK 调用查询

### 2.2 模块整体架构

```
┌─────────────────────────────────────────────────────────────┐
│                    前端 (另一团队负责)                         │
│                  AI Chat Panel · SSE 消费                       │
└──────────────────────┬──────────────────────────────────────┘
                       │ HTTP/SSE
┌──────────────────────┴──────────────────────────────────────┐
│              IotPlatform.Web.Core (主 Web 应用)                │
│                                                               │
│  ┌─────────────────────────────────────────────────────┐    │
│  │           AI 模块（新增类库项目）                        │    │
│  │                                                        │    │
│  │  ┌───────────┐  ┌──────────────┐  ┌───────────────┐  │    │
│  │  │ AI Chat   │  │  Tool Registry│  │  Form Auto    │  │    │
│  │  │ Service   │  │  (工具注册中心)│  │  Builder      │  │    │
│  │  └─────┬─────┘  └──────┬───────┘  └───────┬───────┘  │    │
│  │        │               │                   │           │    │
│  │  ┌─────┴───────────────┴───────────────────┴───────┐  │    │
│  │  │          MAF 智能体编排引擎                        │  │    │
│  │  │  ┌───────┐  ┌────────┐  ┌──────┐  ┌──────────┐ │  │    │
│  │  │  │Router │  │Planner │  │Worker│  │Synthesize│ │  │    │
│  │  │  │Agent  │  │Agent   │  │Agents│  │Agent     │ │  │    │
│  │  │  └───────┘  └────────┘  └──────┘  └──────────┘ │  │    │
│  │  └──────────────────────────────────────────────────┘  │    │
│  │                                                        │    │
│  │  ┌──────────────────────────────────────────────────┐  │    │
│  │  │         LLM Provider (可插拔)                     │  │    │
│  │  │  ┌──────────┐  ┌────────────┐  ┌─────────────┐  │  │    │
│  │  │  │ OpenAI   │  │ Azure      │  │ 本地模型     │  │  │    │
│  │  │  │ Provider │  │ OpenAI     │  │ (Ollama等)  │  │  │    │
│  │  │  └──────────┘  └────────────┘  └─────────────┘  │  │    │
│  │  └──────────────────────────────────────────────────┘  │    │
│  └─────────────────────────────────────────────────────┘    │
│                                                               │
│  ┌─────────────────────────────────────────────────────┐    │
│  │              现有业务模块（不修改）                      │    │
│  │  VisualDev · Warning · StatisticalRule · TDengine    │    │
│  └─────────────────────────────────────────────────────┘    │
└──────────────────────────────────────────────────────────────┘
```

### 2.3 新增项目依赖关系

```
IotPlatform.AI (新增)
  ├──→ IotPlatform.Core          (框架基础设施)
  ├──→ VisualDev                 (表单创建服务)
  ├──→ IotPlatform.Thing.Warning  (报警查询服务)
  ├──→ IotPlatform.Thing.StatisticalRule (统计规则)
  ├──→ Extras.TDengine            (时序数据查询)
  └──→ Microsoft.SemanticKernel  (可选，AI编排SDK)
  └──→ System.Net.Http           (LLM HTTP调用)
```

---

## 3. 核心功能设计

### 3.1 AI 聊天面板与多轮对话

#### 3.1.1 会话管理

新增 `POSTGRESQL` 表 `AI_CONVERSATION` 和 `AI_MESSAGE`：

```sql
-- AI 会话表
CREATE TABLE AI_CONVERSATION (
    F_ID            BIGINT PRIMARY KEY,
    F_TENANT_ID     BIGINT,
    F_TITLE         VARCHAR(200),
    F_MODEL         VARCHAR(50),
    F_USER_ID       BIGINT,
    F_CREATED_TIME  TIMESTAMP DEFAULT NOW(),
    F_UPDATED_TIME  TIMESTAMP
);

-- AI 消息表
CREATE TABLE AI_MESSAGE (
    F_ID              BIGINT PRIMARY KEY,
    F_CONVERSATION_ID BIGINT,
    F_ROLE            VARCHAR(20),   -- user / assistant / system / tool
    F_CONTENT         TEXT,
    F_TOOL_CALLS      JSONB,         -- 工具调用记录
    F_METADATA        JSONB,         -- 元数据（token消耗等）
    F_CREATED_TIME    TIMESTAMP DEFAULT NOW()
);
```

#### 3.1.2 API 设计

**接口 1：创建会话**

```
POST /api/ai/conversations
Request:  { "title": "查询报警统计" }
Response: { "id": 123456, "title": "查询报警统计", "createdTime": "2026-06-29 10:00:00" }
```

**接口 2：发送消息（SSE 流式）**

```
POST /api/ai/conversations/{id}/chat
Content-Type: application/json
Accept: text/event-stream

Request: { "message": "查询下今天的报警总次数及累计报警次数排在前三的问题清单" }

Response (SSE 流):
event: thinking
data: {"content": "正在分析您的查询需求..."}

event: tool_call
data: {"tool": "query_alarm_stats", "arguments": {"date": "2026-06-29", "topN": 3}}

event: tool_result
data: {"tool": "query_alarm_stats", "result": {...}}

event: content
data: {"delta": "根据查询结果，今天报警总次数为 **128 次**..."}

event: done
data: {"conversationId": 123456, "messageId": 789012, "tokenUsage": 1234}
```

**接口 3：获取会话列表**

```
GET /api/ai/conversations?page=1&pageSize=20
Response: { "total": 10, "rows": [...] }
```

**接口 4：获取会话消息历史**

```
GET /api/ai/conversations/{id}/messages
Response: [ { "role": "user", "content": "...", "createdTime": "..." }, ... ]
```

#### 3.1.3 核心实现

创建 `AIChatService`：

```csharp
// 文件：IotPlatform.AI/Services/AIChatService.cs

[ApiDescriptionSettings("AI助手", Tag = "AI", Order = 200)]
[Route("api/ai/")]
public class AIChatService : IDynamicApiController, ITransient
{
    private readonly IAIConversationRepository _conversationRepo;
    private readonly IMAFOrchestrator _orchestrator;
    private readonly IToolRegistry _toolRegistry;

    public AIChatService(
        IAIConversationRepository conversationRepo,
        IMAFOrchestrator orchestrator,
        IToolRegistry toolRegistry)
    {
        _conversationRepo = conversationRepo;
        _orchestrator = orchestrator;
        _toolRegistry = toolRegistry;
    }

    // 创建新会话
    [HttpPost("conversations")]
    public async Task<dynamic> CreateConversation([FromBody] CreateConversationInput input)
    {
        var conversation = new AIConversationEntity
        {
            Id = YitIdHelper.NextId(),
            Title = input.Title ?? "新对话",
            Model = input.Model ?? "default",
            UserId = _userManager.UserId
        };
        await _conversationRepo.InsertAsync(conversation);
        return conversation.Adapt<ConversationOutput>();
    }

    // 发送消息并返回 SSE 流
    [HttpPost("conversations/{id}/chat")]
    public async Task Chat(long id, [FromBody] ChatInput input, CancellationToken cancellationToken)
    {
        var response = HttpContext.Response;
        response.ContentType = "text/event-stream";
        response.Headers["Cache-Control"] = "no-cache";
        response.Headers["Connection"] = "keep-alive";

        // 1. 加载历史消息（多轮对话上下文）
        var history = await _conversationRepo.GetMessagesAsync(id);

        // 2. 保存用户消息
        var userMsg = new AIMessageEntity { ... };
        await _conversationRepo.InsertMessageAsync(userMsg);

        // 3. 通过 MAF 编排器处理请求（流式）
        await foreach (var chunk in _orchestrator.ProcessStreamAsync(
            conversationId: id,
            userMessage: input.Message,
            history: history,
            tenantId: _userManager.TenantId,
            cancellationToken: cancellationToken))
        {
            // 写 SSE 事件到 response body
            await response.WriteAsync(chunk.ToSseString(), cancellationToken);
            await response.Body.FlushAsync(cancellationToken);
        }
    }
}
```

### 3.2 自然语言查询与智能 API 调用

#### 3.2.1 核心流程

用户输入自然语言 → AI 分析意图 → 匹配并调用已有 API/查询 → 汇总结果返回

```
用户: "查询下今天的报警总次数及累计报警次数排在前三的问题清单"
          │
          ▼
   ┌─────────────────┐
   │  Router Agent    │  分析意图 → 判定为 "query" 类型
   └────────┬────────┘
            │
            ▼
   ┌─────────────────┐
   │  Planner Agent   │  制定计划:
   │                  │  1. 调用 AlarmStatsTool 获取今日报警总次数
   │                  │  2. 调用 AlarmTopNTool 获取 Top3 问题
   └────────┬────────┘
            │
            ▼
   ┌─────────────────┐
   │  Worker Agent    │  并行执行工具调用:
   │  (Tool Executor) │  → query_alarm_stats(date="2026-06-29")
   │                  │  → query_alarm_top_n(date="2026-06-29", n=3)
   └────────┬────────┘
            │
            ▼
   ┌─────────────────┐
   │ Synthesize Agent │  汇总结果 → 生成自然语言回复
   └─────────────────┘
```

#### 3.2.2 工具注册中心（Tool Registry）

AI 不直接调用数据库，而是通过**预注册的工具（Tools）**来调用已有的业务服务。每个 Tool 包含名称、描述（供 LLM 理解）、参数 schema 和执行逻辑。

```csharp
// 文件：IotPlatform.AI/Tools/ITool.cs

/// <summary>
/// 工具接口 - 定义可供 AI 调用的工具
/// </summary>
public interface IAITool
{
    /// <summary>工具名称（LLM function name）</summary>
    string Name { get; }

    /// <summary>工具描述（供 LLM 理解何时调用）</summary>
    string Description { get; }

    /// <summary>参数 JSON Schema</summary>
    AIToolParameterSchema Parameters { get; }

    /// <summary>执行工具</summary>
    Task<object> ExecuteAsync(Dictionary<string, object> arguments, long tenantId);
}

public class AIToolParameterSchema
{
    public string Type { get; set; } = "object";
    public Dictionary<string, AIToolProperty> Properties { get; set; }
    public List<string> Required { get; set; }
}

public class AIToolProperty
{
    public string Type { get; set; }
    public string Description { get; set; }
    public string[] Enum { get; set; }
}
```

#### 3.2.3 内置工具清单

以下为第一期需要实现的工具：

| 工具名称 | 功能描述 | 依赖的现有服务 |
|----------|----------|----------------|
| `query_alarm_stats` | 按时间范围统计报警总数 | `ThingWarningHostedService` + TDengine |
| `query_alarm_top_n` | 获取报警次数 Top N 的问题 | `ThingWarningHostedService` + TDengine |
| `query_device_list` | 查询设备列表（按条件） | SqlSugar Repository |
| `query_device_data` | 查询设备实时/历史数据 | `TDengIneReadService` |
| `query_thing_model` | 查询物模型详情 | 物模型模块 |
| `query_oee_stats` | 查询 OEE 统计数据 | OEE 统计模块 |
| `query_statistical_rule` | 查询统计规则及结果 | 统计规则模块 |
| `create_visual_dev_form` | 自动创建表单/列表（见 3.3） | `VisualDevService` |
| `get_form_data` | 查询已创建的表单数据 | `RunService` / `VisualDevModelDataService` |

#### 3.2.4 工具实现示例（报警统计）

```csharp
// 文件：IotPlatform.AI/Tools/AlarmStatsTool.cs

public class AlarmStatsTool : IAITool
{
    public string Name => "query_alarm_stats";
    public string Description => "查询指定时间范围内的报警统计信息，返回报警总次数。";

    public AIToolParameterSchema Parameters => new()
    {
        Properties = new Dictionary<string, AIToolProperty>
        {
            ["startTime"] = new() { Type = "string", Description = "开始时间，格式 yyyy-MM-dd HH:mm:ss" },
            ["endTime"]   = new() { Type = "string", Description = "结束时间，格式 yyyy-MM-dd HH:mm:ss" },
            ["tenantId"]  = new() { Type = "number", Description = "租户ID（可选，从上下文获取）" }
        },
        Required = new List<string> { "startTime", "endTime" }
    };

    private readonly ISqlSugarRepository<AlarmConf> _alarmRepository;
    // 或者直接使用 TDengine 读写服务
    private readonly Extras.TDengine.TDengIne.ReadService _tdReadService;

    public async Task<object> ExecuteAsync(Dictionary<string, object> args, long tenantId)
    {
        string startTime = args["startTime"].ToString();
        string endTime = args["endTime"].ToString();

        // 方式1: 通过 SqlSugar 查询 PostgreSQL 中的报警汇总
        var stats = await _alarmRepository.AsSugarClient()
            .Queryable<AlarmRecordEntity>()
            .Where(x => x.TenantId == tenantId
                     && x.AlarmTime >= DateTime.Parse(startTime)
                     && x.AlarmTime <= DateTime.Parse(endTime))
            .GroupBy(x => x.AlarmType)
            .Select(x => new { AlarmType = x.AlarmType, Count = SqlFunc.AggregateCount(x.Id) })
            .ToListAsync();

        return new { totalAlarms = stats.Sum(x => x.Count), details = stats };
    }
}
```

### 3.3 AI 自动创建表单

这是本方案的核心难点。以下是详细的解决方案。

#### 3.3.1 问题分析

用户通过自然语言描述要创建的表单（例如："帮我创建一个设备巡检表，包含巡检日期、设备名称、巡检人、巡检结果（正常/异常）、异常描述、现场照片"），后端需要：

1. **解析需求**：从自然语言中提取表单字段信息
2. **生成表单 JSON**：将字段信息转换为 `VisualDevEntity.FormData` 的 JSON 结构
3. **生成列表 JSON**：自动生成对应的 `ColumnData`（列表视图）
4. **生成表结构 JSON**：自动生成 `Tables`（数据库表结构）
5. **调用 VisualDevService**：创建表单实体、创建数据库表、发布

#### 3.3.2 前端表单设计器代码如何使用

**结论：前端代码需要作为参考知识提供给 AI，但不是由后端直接运行 JS 代码。**

具体方案如下：

**方案一（推荐）：提取控件类型映射表 + 示例 JSON**

从前端表单设计器代码中提取以下关键信息，作为 AI 的 Prompt 上下文知识：

1. **`jnpfKey` 控件类型映射表**（附录 B）
2. **完整的 `FormData` JSON Schema / 示例**——从已有成功创建的表单中提取
3. **`ColumnData` JSON Schema / 示例**
4. **`Tables` JSON Schema / 示例**

这些信息以**知识库文件**的形式存放在 AI 模块中，在 AI 需要生成表单时作为 System Prompt 的一部分注入。

**方案二（辅助）：Few-Shot 示例库**

从现有已发布的表单中选取 5-10 个典型示例（不同复杂程度），提取其 `FormData` + `ColumnData` + `Tables` 三元组，作为 LLM 的 Few-Shot 学习样本。

#### 3.3.3 表单自动创建流程

```
用户: "帮我创建一个设备巡检表，包含巡检日期、设备名称、巡检人、
       巡检结果（正常/异常）、异常描述、现场照片"
         │
         ▼
┌──────────────────────┐
│   Router Agent        │  → 意图: create_form
└──────────┬───────────┘
           │
           ▼
┌──────────────────────┐
│   Planner Agent       │  计划:
│                       │  1. 解析字段列表
│                       │  2. 生成 FormData JSON
│                       │  3. 生成 ColumnData JSON
│                       │  4. 生成 Tables JSON
│                       │  5. 调用 VisualDevService 创建
│                       │  6. 发布表单+建表
└──────────┬───────────┘
           │
           ▼
┌──────────────────────┐
│   Form Builder Agent  │  (核心 Agent)
│                       │
│  输入: 自然语言描述     │
│  上下文:                │
│   - jnpfKey 映射表     │
│   - FormData JSON 示例│
│   - 当前租户信息        │
│                       │
│  输出: 结构化 JSON      │
│  {                     │
│    formData: {...},    │
│    columnData: {...},  │
│    tables: [...],      │
│    fullName: "设备巡检",│
│    enCode: "deviceInspection" │
│  }                     │
└──────────┬───────────┘
           │
           ▼
┌──────────────────────┐
│   Verifier Agent      │  校验生成的 JSON:
│                       │  1. FormData 模板合法性校验
│                       │  2. 字段名唯一性校验
│                       │  3. 表结构合理性校验
│                       │  (可调用 TemplateParsingBase.VerifyTemplate)
└──────────┬───────────┘
           │
           ▼
┌──────────────────────┐
│   Executor Agent      │  调用 VisualDevService:
│                       │  1. Create(form) → 创建实体
│                       │  2. FuncToMenu(release)→ 发布(建表+建菜单)
│                       │  3. 返回结果
└──────────────────────┘
```

#### 3.3.4 核心实现：FormBuilderService

```csharp
// 文件：IotPlatform.AI/Services/FormAutoBuilderService.cs

public class FormAutoBuilderService : ITransient
{
    private readonly ILLMProvider _llmProvider;
    private readonly VisualDevService _visualDevService;
    private readonly IFormKnowledgeBase _knowledgeBase;

    /// <summary>
    /// 从自然语言描述自动创建表单
    /// </summary>
    public async Task<FormCreationResult> CreateFormFromNaturalLanguageAsync(
        string description,
        long tenantId,
        long userId,
        long categoryId)
    {
        // 步骤1：加载知识库（jnpfKey 映射表 + 示例）
        var systemPrompt = await _knowledgeBase.BuildFormCreationPromptAsync();

        // 步骤2：调用 LLM 生成表单 JSON
        var generatedForm = await _llmProvider.GenerateStructuredOutputAsync<GeneratedFormDto>(
            systemPrompt: systemPrompt,
            userPrompt: $"请根据以下描述创建表单配置：\n{description}",
            responseSchema: GeneratedFormDto.JsonSchema // 强制 LLM 输出结构化 JSON
        );

        // 步骤3：校验生成的 JSON
        if (!await ValidateGeneratedForm(generatedForm))
        {
            throw new AIException("生成的表单配置校验失败");
        }

        // 步骤4：构造 VisualDevCrInput 并调用现有服务
        var input = new VisualDevCrInput
        {
            fullName = generatedForm.FullName,
            enCode   = generatedForm.EnCode,
            category = categoryId.ToString(),
            type     = 1,    // Web设计
            webType  = 2,    // 列表表单
            formData = generatedForm.FormData.ToJson(),
            columnData = generatedForm.ColumnData.ToJson(),
            tables   = generatedForm.Tables.ToJson(),
            dbLinkId = 0     // 使用默认数据库
        };

        // 步骤5：创建表单
        var entity = await _visualDevService.Create(input);

        // 步骤6：发布（建表+建菜单）
        await _visualDevService.FuncToMenu(entity.Id, new VisualDevToMenuInput
        {
            id = entity.Id.ToString(),
            pc = 1,
            pcSystemId = "default",
            pcModuleParentId = "-1",
            code = generatedForm.EnCode
        });

        return new FormCreationResult
        {
            FormId = entity.Id,
            FormName = generatedForm.FullName,
            EnCode = generatedForm.EnCode,
            GeneratedJson = generatedForm
        };
    }

    /// <summary>
    /// 校验生成的表单配置（使用现有的 TemplateParsingBase 校验）
    /// </summary>
    private async Task<bool> ValidateGeneratedForm(GeneratedFormDto generatedForm)
    {
        // 构造临时 VisualDevEntity 以利用现有模板解析校验
        var tempEntity = new VisualDevEntity
        {
            FormData = generatedForm.FormData.ToJson(),
            Tables = generatedForm.Tables.ToJson()
        };

        var templateInfo = new TemplateParsingBase(tempEntity);
        return templateInfo.VerifyTemplate();
    }
}
```

#### 3.3.5 知识库构建（FormKnowledgeBase）

从前端表单设计器代码中提取的知识应组织为以下形式：

```csharp
// 文件：IotPlatform.AI/Knowledge/FormKnowledgeBase.cs

public class FormKnowledgeBase : IFormKnowledgeBase
{
    /// <summary>
    /// 构建表单创建的 System Prompt
    /// </summary>
    public Task<string> BuildFormCreationPromptAsync()
    {
        var prompt = $@"
你是一个 IoT 平台表单设计专家。请根据用户的自然语言描述，生成表单设计的完整 JSON 配置。

## 控件类型映射 (jnpfKey)

{GetJnpfKeyMapping()}

## FormData JSON 结构说明

{GetFormDataSchema()}

## ColumnData JSON 结构说明

{GetColumnDataSchema()}

## Tables JSON 结构说明

{GetTablesSchema()}

## 生成规则

1. enCode 必须为小写驼峰格式的英文标识
2. 每个字段的 __vModel__ 使用英文camelCase命名
3. 需要文件上传时，使用 uploadImg（单图片）或 uploadFz（附件）
4. 下拉选择/单选用 select，多选用 checkbox
5. 日期用 date，时间用 time，数字用 numInput
6. 用户字段用 createUser/currOrganize 等系统字段
7. 主键策略默认使用雪花ID (primaryKeyPolicy: 1)
8. columnData 中的列默认包含所有表单字段
9. 如果是列表表单(webType=2)，必须包含 columnData

## Few-Shot 示例

{GetFewShotExamples()}
";
        return Task.FromResult(prompt);
    }

    private string GetJnpfKeyMapping()
    {
        // 从附录 B 提取，以 Markdown 表格形式提供
        return @"
| jnpfKey | 控件名称 | 数据类型 | 使用场景 |
|---------|---------|---------|---------|
| input | 单行文本 | varchar | 短文本输入 |
| textarea | 多行文本 | longtext | 长文本/描述 |
| numInput | 数字输入 | decimal | 数值字段 |
| switch | 开关 | int | 布尔值 (0/1) |
| radio | 单选框 | varchar | 单选 |
| checkbox | 多选框 | varchar | 多选(逗号分隔) |
| select | 下拉框 | varchar | 单选下拉 |
| date | 日期 | datetime | 日期选择 |
| time | 时间 | varchar | 时间选择 |
| uploadImg | 图片上传 | longtext | 单图片 |
| uploadFz | 附件上传 | longtext | 附件 |
| editor | 富文本 | longtext | 富文本编辑器 |
| ... | ... | ... | ... |
";
    }
}
```

> **注**：前端表单设计器源码应单独提取为一个参考文档（见附录 B），开发人员需从前端代码仓库中提取完整的 `jnpfKey` 映射关系及每种控件的 `__config__` 结构范例，放入 `IotPlatform.AI/Knowledge/` 目录下的配置文件中。

#### 3.3.6 表单自动创建的 SSE 反馈

表单创建过程较长（涉及 AI 生成 + 数据库建表），需要通过 SSE 向用户反馈进度：

```
event: progress
data: {"step": "analyzing", "message": "正在分析您的表单需求..."}

event: progress
data: {"step": "generating", "message": "正在生成表单配置..."}

event: progress
data: {"step": "validating", "message": "正在校验表单配置..."}

event: progress
data: {"step": "creating_db", "message": "正在创建数据库表..."}

event: progress
data: {"step": "publishing", "message": "正在发布表单..."}

event: result
data: {"step": "done", "formId": 123456, "formUrl": "/pages/form/deviceInspection"}
```

---

## 4. 多智能体架构设计（MAF）

### 4.1 MAF 框架概述

本方案采用自定义轻量级 MAF (Multi-Agent Framework) 实现多智能体编排，参考 LangChain/LangGraph 和 Semantic Kernel 的设计理念，但基于项目现有架构和依赖最小化原则，自建轻量级编排引擎。

**为什么自建而不是引入 Semantic Kernel？**
- 项目对依赖最小化有要求，SK 依赖链较重
- 现有 Furion 框架的依赖注入模式需要深度集成
- 需要精确控制每个 Agent 的上下文和工具权限
- 降低后续框架升级时的不兼容风险

> **备选方案**：如果团队更倾向于使用成熟的框架，可直接集成 `Microsoft.SemanticKernel`，它本身就提供了 Agent/Plugin/Tool 的抽象，比自建 MAF 在长期维护性上更优。两种方案在文档中均给出，供团队决策。

### 4.2 智能体角色定义

| Agent 角色 | 职责 | 模型要求 | 可用工具 |
|-----------|------|---------|---------|
| **Router Agent** | 分析用户意图，决定触发哪个场景（chat/query/create_form/unknown） | 快速/廉价模型 | 无工具 |
| **Planner Agent** | 根据意图制定执行计划，将复杂任务拆解为子任务和工具调用序列 | 强推理模型 | 工具注册中心（只读） |
| **Worker Agents** | 执行具体工具调用（每个 Worker 负责一个工具调用的执行和结果转换） | 标准模型 | 分配的具体工具 |
| **Form Builder Agent** | 专门处理表单生成（将自然语言转换为 FormData JSON） | 强推理模型 | 知识库上下文 |
| **Verifier Agent** | 校验 Agent 输出（表单 JSON 合法性、数据查询结果合理性） | 标准模型 | 业务校验规则 |
| **Synthesize Agent** | 汇总所有 Worker 结果，生成最终自然语言回复 | 标准模型/强推理模型 | 无（接收上游输出） |
| **Memory Agent** | 管理对话上下文、用户偏好记忆、跨会话知识 | 快速模型 | 对话存储 |

### 4.3 MAF 编排引擎设计

```csharp
// 文件：IotPlatform.AI/MAF/MAFOrchestrator.cs

public interface IMAFOrchestrator
{
    /// <summary>
    /// 流式处理用户请求，通过多 Agent 协作完成任务
    /// </summary>
    IAsyncEnumerable<AgentChunk> ProcessStreamAsync(
        long conversationId,
        string userMessage,
        List<AIMessageEntity> history,
        long tenantId,
        CancellationToken cancellationToken);
}

public class MAFOrchestrator : IMAFOrchestrator, ISingleton
{
    private readonly IRouterAgent _routerAgent;
    private readonly IPlannerAgent _plannerAgent;
    private readonly IEnumerable<IWorkerAgent> _workerAgents;
    private readonly ISynthesizeAgent _synthesizeAgent;
    private readonly IMemoryAgent _memoryAgent;
    private readonly IToolRegistry _toolRegistry;
    private readonly ILLMProvider _llmProvider;

    public async IAsyncEnumerable<AgentChunk> ProcessStreamAsync(
        long conversationId,
        string userMessage,
        List<AIMessageEntity> history,
        long tenantId,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // ===== Phase 1: Route（路由） =====
        yield return AgentChunk.Thinking("正在分析您的需求...");

        var routeResult = await _routerAgent.RouteAsync(userMessage, history, cancellationToken);

        yield return AgentChunk.Thinking($"识别意图: {routeResult.Intent}");

        // ===== Phase 2: Plan（规划） =====
        yield return AgentChunk.Thinking("正在制定执行计划...");

        var plan = await _plannerAgent.PlanAsync(
            routeResult.Intent,
            userMessage,
            history,
            _toolRegistry.GetAvailableTools(routeResult.Intent),
            cancellationToken);

        // ===== Phase 3: Execute（执行） =====
        var toolResults = new List<ToolExecutionResult>();

        // 并行执行独立的工具调用
        var parallelTasks = plan.Steps
            .Where(s => s.CanParallel)
            .Select(step => ExecuteStepAsync(step, tenantId, cancellationToken));

        // 串行执行有依赖的工具调用
        var sequentialSteps = plan.Steps.Where(s => !s.CanParallel);

        // 并行组先执行
        if (parallelTasks.Any())
        {
            var results = await Task.WhenAll(parallelTasks);
            toolResults.AddRange(results.Where(r => r != null));

            foreach (var result in results.Where(r => r != null))
            {
                yield return AgentChunk.ToolResult(result.ToolName, result.Output);
            }
        }

        // 串行执行依赖步骤
        foreach (var step in sequentialSteps)
        {
            var result = await ExecuteStepAsync(step, tenantId, cancellationToken);
            if (result != null)
            {
                toolResults.Add(result);
                yield return AgentChunk.ToolResult(result.ToolName, result.Output);
            }
        }

        // ===== Phase 4: Synthesize（合成） =====
        yield return AgentChunk.Thinking("正在汇总结果...");

        // 流式输出最终回复
        await foreach (var delta in _synthesizeAgent.SynthesizeStreamAsync(
            userMessage,
            toolResults,
            history,
            cancellationToken))
        {
            yield return AgentChunk.ContentDelta(delta);
        }

        // ===== Phase 5: Memory（记忆） =====
        yield return AgentChunk.Done(conversationId);

        // 异步保存对话记忆（不阻塞流式返回）
        _ = _memoryAgent.SaveMemoryAsync(conversationId, userMessage, toolResults, cancellationToken);
    }

    private async Task<ToolExecutionResult> ExecuteStepAsync(
        PlanStep step, long tenantId, CancellationToken ct)
    {
        yield return AgentChunk.ToolCall(step.ToolName, step.Arguments);

        var tool = _toolRegistry.GetTool(step.ToolName);
        if (tool == null)
        {
            return new ToolExecutionResult
            {
                ToolName = step.ToolName,
                Success = false,
                Error = $"工具 '{step.ToolName}' 未注册"
            };
        }

        try
        {
            var output = await tool.ExecuteAsync(step.Arguments, tenantId);
            return new ToolExecutionResult
            {
                ToolName = step.ToolName,
                Success = true,
                Output = output
            };
        }
        catch (Exception ex)
        {
            return new ToolExecutionResult
            {
                ToolName = step.ToolName,
                Success = false,
                Error = ex.Message
            };
        }
    }
}
```

### 4.4 Agent 间通信机制

Agent 之间通过 `AgentContext`（类似于消息总线）传递上下文：

```csharp
public class AgentContext
{
    public long ConversationId { get; set; }
    public long TenantId { get; set; }
    public long UserId { get; set; }

    // 当前路由结果
    public RouteResult RouteResult { get; set; }

    // 当前执行计划
    public AgentPlan Plan { get; set; }

    // 已执行的工具结果列表
    public List<ToolExecutionResult> ToolResults { get; set; } = new();

    // 中间状态存储（Agent 间共享数据）
    public Dictionary<string, object> State { get; set; } = new();

    // 多轮对话历史
    public List<AIMessageEntity> History { get; set; } = new();
}
```

### 4.5 MAF 分步实施路线

#### 第一阶段：基础版（单 Agent 模式）

**目标**：快速上线 AI 聊天功能

```
┌────────────────────┐
│    Simple Agent     │  直接接收用户输入 → 调用 LLM → 返回回复
│                    │
│  功能:              │
│  1. 普通对话        │
│  2. 基础工具调用     │
│  (无多 Agent 编排)  │
└────────────────────┘
```

- 实现 `AIChatService` + 基础 LLM Provider
- 实现 3-5 个核心 Tool（报警查询、设备查询等）
- 单次对话中 LLM 自行决定是否调用工具（Function Calling）
- 对话历史存储

#### 第二阶段：工具扩展版（带 Router + Worker）

**目标**：扩展工具数量，引入意图路由

```
   ┌───────────────────┐
   │   Router Agent     │  → 分类意图: chat / query / create_form
   └─────────┬─────────┘
             │
   ┌─────────┴─────────┐
   │   Worker Agent     │  → 执行工具调用
   │  (Tool Executor)   │
   └─────────┬─────────┘
             │
   ┌─────────┴─────────┐
   │   Synthesize Agent │  → 汇总回复
   └────────────────────┘
```

- 实现 Router Agent（基于 Prompt 分类）
- 扩展工具到 10+ 个
- 实现 Synthesize Agent（汇总工具结果 + 生成自然语言）
- 流式响应支持

#### 第三阶段：全功能版（完整 MAF 编排）

**目标**：引入 Planner + Verifier + Memory，实现复杂任务规划

```
   ┌───────────────────┐
   │   Memory Agent     │  ← 读取用户偏好/历史上下文
   └─────────┬─────────┘
             │
   ┌─────────┴─────────┐
   │   Router Agent     │
   └─────────┬─────────┘
             │
   ┌─────────┴─────────┐
   │   Planner Agent    │  → 复杂任务拆解 + 依赖分析
   └─────────┬─────────┘
             │
   ┌─────────┴─────────┐
   │   Worker Agents ×N │  → 并行/串行执行
   └─────────┬─────────┘
             │
   ┌─────────┴─────────┐
   │  Verifier Agent    │  → 校验工具输出
   └─────────┬─────────┘
             │
   ┌─────────┴─────────┐
   │ Synthesize Agent   │
   └────────────────────┘
```

- 实现 Planner Agent（将复杂自然语言需求拆解为多个工具调用步骤）
- 实现 Verifier Agent（校验表单 JSON、数据查询结果）
- 实现 Memory Agent（用户偏好、常用查询缓存）
- 工具间依赖编排（串行/并行）

#### 第四阶段：专用 Agent + 持续优化

**目标**：引入专用 Agent（Form Builder 等），持续优化

- Form Builder Agent 专用化（分离为独立 Agent，拥有专门的表单领域知识）
- 引入 Agent 间反馈循环（Verifier 发现问题的 → 自动重试修正）
- 用户反馈收集与模型微调
- 工具自动发现（通过反射扫描已有 API 自动生成工具注册）

---

## 5. 详细实施步骤

### 第一阶段：基础建设（1-2 周）

#### Step 1.1：创建 `IotPlatform.AI` 类库项目

```
📦 IotPlatform.AI/
├── IotPlatform.AI.csproj
├── AIStartup.cs                    # Furion AppStartup，注入 AI 服务
├── Configuration/
│   └── AIOptions.json              # AI 配置文件
├── Models/
│   ├── Entities/                   # AI 相关实体
│   │   ├── AIConversationEntity.cs
│   │   └── AIMessageEntity.cs
│   ├── Dto/                        # 请求/响应 DTO
│   │   ├── ChatInput.cs
│   │   ├── ConversationOutput.cs
│   │   └── GeneratedFormDto.cs
│   └── Enums/
│       └── AgentIntent.cs
├── Providers/                      # LLM 提供者
│   ├── ILLMProvider.cs
│   ├── OpenAIProvider.cs
│   └── LLMProviderFactory.cs
├── Repositories/                   # 数据访问
│   ├── IAIConversationRepository.cs
│   └── AIConversationRepository.cs
├── Services/                       # 核心服务
│   ├── AIChatService.cs            # AI 聊天服务（API 接口）
│   ├── FormAutoBuilderService.cs   # 表单自动创建
│   └── SSEHelper.cs                # SSE 辅助工具
├── Tools/                          # 工具注册
│   ├── ITool.cs
│   ├── IToolRegistry.cs
│   ├── ToolRegistry.cs
│   └── BuiltIn/                    # 内置工具
│       ├── AlarmStatsTool.cs
│       ├── AlarmTopNTool.cs
│       ├── DeviceQueryTool.cs
│       ├── FormCreateTool.cs
│       └── ...
├── MAF/                            # 多智能体编排（从第三阶段引入）
│   ├── IMAFOrchestrator.cs
│   ├── MAFOrchestrator.cs
│   ├── AgentContext.cs
│   ├── AgentChunk.cs
│   ├── Agents/
│   │   ├── IAgent.cs
│   │   ├── RouterAgent.cs
│   │   ├── PlannerAgent.cs
│   │   ├── WorkerAgent.cs
│   │   ├── SynthesizeAgent.cs
│   │   ├── VerifierAgent.cs
│   │   ├── FormBuilderAgent.cs
│   │   └── MemoryAgent.cs
│   └── Models/
│       ├── RouteResult.cs
│       ├── AgentPlan.cs
│       └── ToolExecutionResult.cs
└── Knowledge/                      # AI 知识库
    ├── IFormKnowledgeBase.cs
    ├── FormKnowledgeBase.cs
    ├── JnpfKeyMappings.json        # 控件类型映射（从前端代码提取）
    ├── FormDataSchema.json         # FormData JSON Schema
    └── FewShotExamples/            # Few-Shot 示例
        ├── example_1.json
        ├── example_2.json
        └── ...
```

#### Step 1.2：添加 NuGet 依赖

```xml
<!-- IotPlatform.AI.csproj -->
<ItemGroup>
    <!-- 核心框架依赖（已有） -->
    <ProjectReference Include="..\..\01-架构核心\IotPlatform.Core\IotPlatform.Core.csproj" />

    <!-- 业务模块依赖（按需引用接口/实体） -->
    <ProjectReference Include="..\..\02-应用模块\10-VisualDev\VisualDev\VisualDev.csproj" />
    <ProjectReference Include="..\..\02-应用模块\10-VisualDev\VisualDev.Entity\VisualDev.Entity.csproj" />
    <ProjectReference Include="..\..\02-应用模块\07-Thing\01-Warning\IotPlatform.Thing.Warning.Entity\IotPlatform.Thing.Warning.Entity.csproj" />
    <ProjectReference Include="..\..\01-架构核心\Extras.TDengine\Extras.TDengine.csproj" />

    <!-- LLM 调用 HTTP 客户端 -->
    <!-- 不引入 Semantic Kernel 等重依赖，直接用 HttpClient -->
</ItemGroup>
```

#### Step 1.3：创建配置

```json
// Configuration/AIOptions.json
{
  "AI": {
    "Enabled": true,
    "Provider": "OpenAI",          // OpenAI / AzureOpenAI / Ollama
    "OpenAI": {
      "ApiKey": "sk-xxx",
      "BaseUrl": "https://api.openai.com/v1",
      "DefaultModel": "gpt-4o",
      "FastModel": "gpt-4o-mini",   // 用于 Router 等简单任务
      "MaxTokens": 4096,
      "Temperature": 0.1            // 表单生成用低温度确保一致性
    },
    "AzureOpenAI": {
      "Endpoint": "https://xxx.openai.azure.com",
      "ApiKey": "xxx",
      "DeploymentName": "gpt-4o"
    },
    "Ollama": {
      "BaseUrl": "http://localhost:11434",
      "DefaultModel": "qwen2.5:14b"
    },
    "Conversation": {
      "MaxHistoryMessages": 20,
      "MaxContextTokens": 8000
    },
    "Streaming": {
      "Enabled": true,
      "ChunkDelayMs": 50
    }
  }
}
```

#### Step 1.4：注册 AI 服务到 DI

```csharp
// 文件：IotPlatform.AI/AIStartup.cs

public class AIStartup : AppStartup
{
    public void ConfigureServices(IServiceCollection services)
    {
        var aiOptions = App.GetOptions<AIOptions>();
        if (!aiOptions.Enabled)
        {
            return; // AI 功能关闭则不注册任何服务
        }

        // LLM Provider
        services.AddSingleton<ILLMProviderFactory, LLMProviderFactory>();
        services.AddSingleton<ILLMProvider>(sp =>
            sp.GetRequiredService<ILLMProviderFactory>().Create());

        // 工具注册中心
        services.AddSingleton<IToolRegistry, ToolRegistry>();

        // 知识库
        services.AddSingleton<IFormKnowledgeBase, FormKnowledgeBase>();

        // 仓储
        services.AddScoped<IAIConversationRepository, AIConversationRepository>();

        // 表单自动构建
        services.AddScoped<FormAutoBuilderService>();

        // MAF 编排器（根据阶段引入）
        // 第一阶段：简单编排
        services.AddSingleton<IMAFOrchestrator, SimpleOrchestrator>();
        // 第三阶段：完整编排
        // services.AddSingleton<IMAFOrchestrator, MAFOrchestrator>();

        // 注册所有 AI Tool
        services.AddSingleton<IAITool, AlarmStatsTool>();
        services.AddSingleton<IAITool, AlarmTopNTool>();
        services.AddSingleton<IAITool, DeviceQueryTool>();
        services.AddSingleton<IAITool, FormCreateTool>();
        // ... 更多工具

        // 注册 MAF Agents
        services.AddSingleton<IRouterAgent, RouterAgent>();
        services.AddSingleton<IPlannerAgent, PlannerAgent>();
        // ...
    }
}
```

#### Step 1.5：在主项目中注册 AI Startup

在 `IotPlatform.Web.Core/Startup.cs` 的 `ConfigureServices` 中最前面添加：

```csharp
// 文件：IotPlatform.Web.Core/Startup.cs

public void ConfigureServices(IServiceCollection services)
{
    // 添加 AI 模块（通过 AppStartup 自动发现）
    // 只需在 IotPlatform.Web.Core.csproj 中添加 ProjectReference 即可自动发现

    // ... 其他现有配置不变
}
```

在 `IotPlatform.Web.Core.csproj` 中添加：

```xml
<ItemGroup>
    <ProjectReference Include="..\..\02-应用模块\15-AI\IotPlatform.AI\IotPlatform.AI.csproj" />
</ItemGroup>
```

### 第二阶段：工具扩展与表单创建（2-3 周）

#### Step 2.1：从前端代码提取控件映射

> **重要操作**：需要从前端表单设计器项目中提取以下信息，由前端团队配合提供或后端自行爬取：

1. **完整 jnpfKey 映射表**：遍历前端设计器源码中所有可拖拽控件类型
2. **每个控件的默认 `__config__` 结构**：从前端组件定义中提取
3. **FormData 完整 JSON Schema**：从类型定义或已有表单导出
4. **5-10 个典型的已发布表单作为 Few-Shot 示例**

将这些信息整理为 JSON 配置文件，放入 `IotPlatform.AI/Knowledge/` 目录。

#### Step 2.2：实现表单自动创建

- 实现 `FormAutoBuilderService`
- 构建 `FormKnowledgeBase`（加载 jnpfKey 映射 + 示例）
- 编写 LLM Prompt 模板（使用 Few-Shot + 结构化输出）
- 集成 `VisualDevService.Create()` 和 `FuncToMenu()`

#### Step 2.3：实现 10+ 内置工具

按优先级实现工具：
1. `query_alarm_stats` - 报警统计
2. `query_alarm_top_n` - 报警 Top N
3. `query_device_list` - 设备列表
4. `query_device_realtime_data` - 设备实时数据
5. `query_device_history_data` - 设备历史数据
6. `create_visual_dev_form` - 创建表单
7. `query_form_list` - 查询表单列表
8. `query_thing_model` - 查询物模型
9. `query_oee_stats` - OEE 统计
10. `query_statistical_rule_result` - 统计规则结果

### 第三阶段：多智能体协同（3-4 周）

#### Step 3.1：实现完整 MAF 编排

- 实现 7 个 Agent（Router / Planner / Worker / Synthesize / Verifier / FormBuilder / Memory）
- 实现 Agent 间消息传递
- 实现工具并行执行与依赖编排

#### Step 3.2：引入 Agent 自我校验

- Verifier Agent 校验 Worker 输出
- 结果不正确时自动重试或修正
- 表单 JSON 自动校验（调用 `TemplateParsingBase.VerifyTemplate()`）

#### Step 3.3：记忆与偏好学习

- 用户常用查询缓存
- 表单创建偏好（默认字段、命名规范）
- 跨会话知识继承

### 第四阶段：优化与集成（1-2 周）

- 性能优化（LLM 响应缓存、工具结果缓存）
- 安全加固（Prompt Injection 防护、敏感数据脱敏）
- 监控与日志（Token 消耗统计、Agent 调用链追踪）
- 前端联调（与前端团队对接 SSE 格式和 API 协议）

---

## 6. 与现有框架解耦设计

### 6.1 模块隔离策略

```
IotPlatform.AI (独立类库)
      │
      ├── 依赖 IotPlatform.Core（框架基础）
      │    └── 仅使用: ISqlSugarRepository<T>, App.GetOptions<T>,
      │              YitIdHelper, Oops.Oh(), Log
      │
      ├── 依赖 VisualDev + VisualDev.Entity（表单创建）
      │    └── 仅调用: VisualDevService.Create(),
      │              VisualDevService.FuncToMenu(),
      │              VisualDevCrInput, VisualDevToMenuInput
      │              TemplateParsingBase (用于校验)
      │
      ├── 依赖 IotPlatform.Thing.Warning.Entity（报警实体）
      │    └── 仅使用: AlarmRecordEntity 等实体类定义
      │
      └── 依赖 Extras.TDengine（时序查询）
           └── 仅调用: ReadService, ExecuteService
```

### 6.2 通过接口隔离

```csharp
// 不直接依赖具体服务的实现，通过接口解耦

public interface IAlarmQueryService
{
    Task<AlarmStatsResult> GetAlarmStatsAsync(DateTime start, DateTime end, long tenantId);
    Task<List<AlarmTopItem>> GetAlarmTopNAsync(DateTime start, DateTime end, int n, long tenantId);
}

// 在现有的 Warning 模块中实现该接口
// 在 AI 模块中只依赖接口
public class AlarmQueryServiceAdapter : IAlarmQueryService, IScoped
{
    private readonly ISqlSugarRepository<AlarmRecordEntity> _repo;
    // 适配现有服务
}
```

### 6.3 配置开关

```csharp
// AI 功能可完全通过配置关闭，不注册任何服务
if (!App.GetOptions<AIOptions>().Enabled)
{
    return; // 跳过所有 AI 服务注册
}
```

### 6.4 不修改现有代码原则

1. **不改动任何现有模块的代码**（只新增，不修改）
2. **不向现有实体添加字段**（AI 数据存储在独立表中）
3. **不在现有 Startup 中添加 AI 相关逻辑**（通过独立的 AppStartup 注册）
4. **AI 服务异常不阻塞主流程**（所有 AI 调用有独立异常处理和降级策略）

---

## 7. 项目结构与文件清单

### 7.1 新增文件全景

```
📦 02-应用模块/
└── 📂 15-AI/                                    # 新增目录
    ├── IotPlatform.AI/
    │   ├── IotPlatform.AI.csproj
    │   ├── AIStartup.cs
    │   ├── GlobalUsings.cs
    │   │
    │   ├── Configuration/
    │   │   ├── AIOptions.cs
    │   │   └── AIOptions.json
    │   │
    │   ├── Models/
    │   │   ├── Entities/
    │   │   │   ├── AIConversationEntity.cs       # 会话实体
    │   │   │   └── AIMessageEntity.cs            # 消息实体
    │   │   ├── Dto/
    │   │   │   ├── ChatInput.cs                  # 聊天输入 DTO
    │   │   │   ├── ConversationOutput.cs         # 会话输出 DTO
    │   │   │   ├── GeneratedFormDto.cs           # AI 生成的表单 DTO
    │   │   │   └── SSEChunk.cs                   # SSE 消息块定义
    │   │   ├── Enums/
    │   │   │   ├── AgentIntent.cs                # 意图枚举
    │   │   │   └── AgentPhase.cs                 # Agent 阶段枚举
    │   │   └── Options/
    │   │       └── AIOptions.cs                  # 强类型配置类
    │   │
    │   ├── Providers/
    │   │   ├── ILLMProvider.cs                   # LLM 提供者接口
    │   │   ├── OpenAIProvider.cs                 # OpenAI 实现
    │   │   ├── AzureOpenAIProvider.cs            # Azure OpenAI 实现
    │   │   ├── OllamaProvider.cs                 # Ollama 本地模型
    │   │   └── LLMProviderFactory.cs             # 提供者工厂
    │   │
    │   ├── Repositories/
    │   │   ├── IAIConversationRepository.cs
    │   │   └── AIConversationRepository.cs
    │   │
    │   ├── Services/
    │   │   ├── AIChatService.cs                  # ★ 核心：AI 聊天 API
    │   │   ├── FormAutoBuilderService.cs         # ★ 核心：表单自动创建
    │   │   └── SSEHelper.cs                      # SSE 流式响应辅助类
    │   │
    │   ├── Tools/
    │   │   ├── ITool.cs                          # 工具接口
    │   │   ├── IToolRegistry.cs                  # 工具注册中心接口
    │   │   ├── ToolRegistry.cs                   # 工具注册中心实现
    │   │   └── BuiltIn/
    │   │       ├── AlarmStatsTool.cs             # 报警统计工具
    │   │       ├── AlarmTopNTool.cs              # 报警 Top N 工具
    │   │       ├── DeviceQueryTool.cs            # 设备查询工具
    │   │       ├── DeviceDataTool.cs             # 设备数据查询工具
    │   │       ├── FormCreateTool.cs             # 表单创建工具
    │   │       ├── FormQueryTool.cs              # 表单查询工具
    │   │       ├── ThingModelTool.cs             # 物模型查询工具
    │   │       └── OeeStatsTool.cs               # OEE 统计工具
    │   │
    │   ├── MAF/                                  # 多智能体编排（第三阶段）
    │   │   ├── IMAFOrchestrator.cs
    │   │   ├── MAFOrchestrator.cs
    │   │   ├── SimpleOrchestrator.cs             # 第一阶段简单编排器
    │   │   ├── AgentContext.cs
    │   │   ├── AgentChunk.cs
    │   │   ├── Agents/
    │   │   │   ├── IAgent.cs
    │   │   │   ├── RouterAgent.cs
    │   │   │   ├── PlannerAgent.cs
    │   │   │   ├── WorkerAgent.cs
    │   │   │   ├── SynthesizeAgent.cs
    │   │   │   ├── VerifierAgent.cs
    │   │   │   ├── FormBuilderAgent.cs
    │   │   │   └── MemoryAgent.cs
    │   │   └── Models/
    │   │       ├── RouteResult.cs
    │   │       ├── AgentPlan.cs
    │   │       ├── PlanStep.cs
    │   │       └── ToolExecutionResult.cs
    │   │
    │   └── Knowledge/
    │       ├── IFormKnowledgeBase.cs
    │       ├── FormKnowledgeBase.cs
    │       ├── Data/
    │       │   ├── JnpfKeyMappings.json          # ★ 从前端提取的控件映射
    │       │   ├── FormDataSchema.json           # FormData JSON Schema
    │       │   ├── ColumnDataSchema.json         # ColumnData JSON Schema
    │       │   └── TablesSchema.json             # Tables JSON Schema
    │       └── Examples/                         # Few-Shot 示例
    │           ├── example_simple_form.json      # 简单表单示例
    │           ├── example_list_form.json        # 列表表单示例
    │           └── example_complex_form.json     # 复杂表单示例
    │
    └── Migrations/                               # 数据库迁移脚本
        └── V1.0_AI_Tables.sql
```

### 7.2 需要修改的现有文件

| 文件 | 修改内容 | 说明 |
|------|---------|------|
| `IotPlatform.Web.Core.csproj` | 添加 `<ProjectReference>` | 引用 IotPlatform.AI |
| `IotPlatform.Web.Core/Startup.cs` | 无需修改 | Furion 自动发现 AppStartup |

> **总计新增文件数：约 55 个**
> **总计修改现有文件数：1 个**（仅添加项目引用）

---

## 8. 数据库设计

### 8.1 PostgreSQL 新表

```sql
-- =========================================================
-- AI 会话表
-- =========================================================
CREATE TABLE AI_CONVERSATION (
    F_ID            BIGINT NOT NULL PRIMARY KEY,
    F_TENANT_ID     BIGINT,
    F_TITLE         VARCHAR(200),
    F_MODEL         VARCHAR(50),
    F_USER_ID       BIGINT,
    F_USER_NAME     VARCHAR(100),
    F_MESSAGE_COUNT INT DEFAULT 0,
    F_CREATED_TIME  TIMESTAMP DEFAULT NOW(),
    F_UPDATED_TIME  TIMESTAMP
);

COMMENT ON TABLE AI_CONVERSATION IS 'AI 会话记录';
COMMENT ON COLUMN AI_CONVERSATION.F_ID IS '主键ID';
COMMENT ON COLUMN AI_CONVERSATION.F_TENANT_ID IS '租户ID';
COMMENT ON COLUMN AI_CONVERSATION.F_TITLE IS '会话标题';
COMMENT ON COLUMN AI_CONVERSATION.F_MODEL IS '使用的模型';

-- =========================================================
-- AI 消息表
-- =========================================================
CREATE TABLE AI_MESSAGE (
    F_ID              BIGINT NOT NULL PRIMARY KEY,
    F_CONVERSATION_ID BIGINT NOT NULL,
    F_TENANT_ID       BIGINT,
    F_ROLE            VARCHAR(20) NOT NULL,    -- system / user / assistant / tool
    F_CONTENT         TEXT,
    F_TOOL_CALLS      JSONB,                   -- 工具调用记录
    F_TOOL_RESULTS    JSONB,                   -- 工具执行结果
    F_TOKEN_USAGE     JSONB,                   -- {"prompt": 100, "completion": 200}
    F_METADATA        JSONB,
    F_CREATED_TIME    TIMESTAMP DEFAULT NOW()
);

COMMENT ON TABLE AI_MESSAGE IS 'AI 对话消息';
COMMENT ON COLUMN AI_MESSAGE.F_ROLE IS '消息角色: system/user/assistant/tool';
COMMENT ON COLUMN AI_MESSAGE.F_TOOL_CALLS IS 'AI 发起的工具调用详情';
COMMENT ON COLUMN AI_MESSAGE.F_TOOL_RESULTS IS '工具调用返回结果';

-- 索引
CREATE INDEX IDX_AI_MSG_CONV ON AI_MESSAGE(F_CONVERSATION_ID, F_CREATED_TIME);
CREATE INDEX IDX_AI_CONV_USER ON AI_CONVERSATION(F_USER_ID, F_CREATED_TIME DESC);
CREATE INDEX IDX_AI_CONV_TENANT ON AI_CONVERSATION(F_TENANT_ID);
```

### 8.2 TDengine

AI 模块**不在 TDengine 中新建表**。AI 对时序数据的查询通过现有的 `Extras.TDengine` 模块进行，复用已有的超级表和子表结构。

---

## 9. 部署与配置

### 9.1 LLM 提供者配置

推荐的模型选择（按场景分）：

| 场景 | 推荐模型 | 理由 |
|------|---------|------|
| Router Agent (意图分类) | gpt-4o-mini / qwen2.5:7b | 快速、便宜 |
| Planner Agent (任务规划) | gpt-4o / deepseek-v3 | 推理能力强 |
| Form Builder (表单生成) | gpt-4o / claude-sonnet-4-6 | 结构化输出精度高 |
| Worker (工具调用) | gpt-4o-mini | 明确的函数调用 |
| Synthesize (结果汇总) | gpt-4o / deepseek-v3 | 语言生成质量 |
| Memory (记忆管理) | gpt-4o-mini | 文本摘要 |

### 9.2 Docker Compose 配置（可选 Ollama）

```yaml
# 在现有 compose.yaml 中添加可选的 Ollama 服务
services:
  ollama:
    image: ollama/ollama:latest
    container_name: iot_ollama
    ports:
      - "11434:11434"
    volumes:
      - ollama_data:/root/.ollama
    deploy:
      resources:
        reservations:
          devices:
            - driver: nvidia
              count: 1
              capabilities: [gpu]    # 如果有 GPU
    profiles:
      - ai                             # 按需启动: docker compose --profile ai up

volumes:
  ollama_data:
```

### 9.3 环境变量

```bash
# .env 或 appsettings 中
AI__Enabled=true
AI__Provider=OpenAI
AI__OpenAI__ApiKey=sk-xxx
AI__OpenAI__BaseUrl=https://api.openai.com/v1
AI__OpenAI__DefaultModel=gpt-4o
AI__OpenAI__FastModel=gpt-4o-mini
```

---

## 10. 附录

### 附录 A：SSE 协议规范

#### Event 类型定义

| Event 类型 | 数据格式 | 说明 |
|-----------|---------|------|
| `thinking` | `{"content": "正在分析..."}` | AI 思考过程提示 |
| `tool_call` | `{"tool": "name", "arguments": {...}}` | AI 调用工具 |
| `tool_result` | `{"tool": "name", "result": {...}}` | 工具执行结果 |
| `content` | `{"delta": "文本增量..."}` | AI 回复内容流式增量 |
| `progress` | `{"step": "generating", "message": "..."}` | 表单创建进度 |
| `error` | `{"code": "ERR_XXX", "message": "..."}` | 错误信息 |
| `done` | `{"conversationId": 123, "messageId": 456, "tokenUsage": 1234}` | 完成标记 |

#### SSE 消息格式

```
event: {event_type}
data: {json_payload}

```

每次发送以 `\n\n` 结束。

### 附录 B：控件类型 (jnpfKey) 映射表

> **重要**：以下为示例映射表，完整的映射关系需要从前端表单设计器项目中提取。前端团队应提供该文档，或后端开发者从前端仓库中提取。

| jnpfKey | 控件名称 | 数据库类型 | dataLength | 使用场景 |
|---------|---------|-----------|------------|---------|
| `input` | 单行文本 | varchar | 500 | 短文本 |
| `textarea` | 多行文本 | longtext | - | 长文本/备注 |
| `numInput` | 数字输入 | decimal | 38(可变精度) | 数值 |
| `switch` | 开关 | int | 1 | 布尔/开关 |
| `radio` | 单选框 | varchar | 500 | 单选 |
| `checkbox` | 多选框 | varchar | 500 | 多选 |
| `select` | 下拉选择 | varchar | 500 | 单选下拉 |
| `date` | 日期 | datetime | - | 日期 |
| `time` | 时间 | varchar | 50 | 时间 |
| `uploadImg` | 图片上传 | longtext | - | 单图片 |
| `uploadFz` | 附件上传 | longtext | - | 文件附件 |
| `editor` | 富文本 | longtext | - | 富文本编辑 |
| `rate` | 评分 | decimal | 38(精度1) | 星级评分 |
| `slider` | 滑块 | decimal | 38(精度3) | 数值滑块 |
| `sign` | 手写签名 | longtext | - | 电子签名 |
| `createUser` | 创建用户 | varchar | 50 | 自动填充 |
| `createTime` | 创建时间 | datetime | - | 自动填充 |
| `modifyUser` | 修改用户 | varchar | 50 | 自动填充 |
| `modifyTime` | 修改时间 | datetime | - | 自动填充 |
| `currOrganize` | 当前组织 | varchar | 50 | 自动填充 |
| `billRule` | 单据规则 | varchar | 50 | 自动编号 |
| `popupSelect` | 弹窗选择 | varchar | 500 | 关联选择 |
| `popupAttr` | 弹窗属性 | varchar | 500 | 关联回填 |
| `relationForm` | 关联表单 | - | - | 不存储 |
| `relationFormAttr` | 关联表单属性 | (按需) | (按需) | 关联回填,isStorage=1时存储 |
| `calculate` | 计算公式 | decimal | (按需) | 计算字段,isStorage=1时存储 |
| `table` | 子表 | - | - | 子表容器 |
| `button` | 按钮 | - | - | 不存储 |
| `link` | 链接 | - | - | 不存储 |
| `alert` | 提示 | - | - | 不存储 |
| `jnpfText` | 文本展示 | - | - | 不存储 |
| `barcode` | 条形码 | - | - | 不存储 |
| `qrcode` | 二维码 | - | - | 不存储 |
| `iframe` | 内嵌页面 | - | - | 不存储 |

**前端代码提取指引：**

前端表单设计器通常有以下源码位置（以常见前端框架为例）：
- 控件注册清单：`src/components/FormDesigner/controls/` 目录
- 控件类型定义：`types/form.ts` 或 `enums/controlType.ts`
- 默认配置：每个控件目录下的 `defaultConfig.ts` 或 `config.ts`

开发者应找到包含如下结构的文件：
```typescript
// 类似这样的控件配置映射
const controlTypeMap = {
  input: { label: '单行文本', icon: '...', defaultConfig: {...} },
  numInput: { label: '数字输入', icon: '...', defaultConfig: {...} },
  // ...
}
```

将提取的信息整理为 JSON，输出到 `IotPlatform.AI/Knowledge/Data/JnpfKeyMappings.json`。

### 附录 C：AI 生成表单的 Prompt 模板

以下为 FormBuilderAgent 使用的 System Prompt 核心内容（节选）：

```
你是一个专业的企业级 IoT 平台表单设计器。你需要将用户的自然语言描述转换为
VisualDev 表单的完整 JSON 配置。

## 输出格式

你必须输出以下 JSON 结构：
{
  "fullName": "表单名称（中文）",
  "enCode": "英文标识（camelCase）",
  "type": 1,
  "webType": 2,
  "description": "表单描述",
  "primaryKeyPolicy": 1,
  "fields": [...],
  "columnList": [...],
  "tables": [...]
}

## 控件选择规则

1. 短文本 → input (jnpfKey)
2. 长文本/备注/描述 → textarea
3. 数字/金额/数量 → numInput
4. 日期选择 → date
5. 时间选择 → time
6. 单选/下拉 → select（选项用 options 配置）
7. 多选 → checkbox
8. 图片/照片 → uploadImg
9. 文件/附件 → uploadFz
10. 子表/明细表 → table（内含 children 字段列表）
11. 计算公式 → calculate

## 字段命名规则

- __vModel__: 使用 camelCase 英文，如 deviceName, inspectionDate
- label: 使用中文描述

## 表结构规则

- 主表名: mt_{entityId}（先用占位符 mt_{formId}）
- 子表名: ct_{randomId}
- 主键: f_id, varchar(50), 非自增
- 每个入库字段对应一个数据库列

请严格遵循以上规则生成配置。
```

### 附录 D：风险与应对

| 风险 | 影响 | 应对措施 |
|------|------|---------|
| LLM 生成表单 JSON 不合理 | 创建的数据库表不符合预期 | Verifier Agent 校验 + 给用户展示预览确认 |
| LLM 输出格式不稳定 | 结构化解析失败 | 使用 JSON Mode / Function Calling / 重试机制 |
| LLM API 不可用 | AI 功能瘫痪 | 本地 Ollama 兜底 + 降级提示 |
| SQL 注入 / Prompt 注入 | 数据安全风险 | 所有 AI 调用的数据库操作通过预注册 Tool 执行，不接受原始 SQL |
| Token 消耗过大 | 成本过高 | 对话历史截断、Router 使用便宜模型、缓存常见查询 |
| 多 Agent 编排复杂性 | 开发/调试困难 | 分四阶段渐进实施，每阶段有可验证的交付物 |

### 附录 E：关键术语

| 术语 | 全称 | 说明 |
|------|------|------|
| MAF | Multi-Agent Framework | 多智能体编排框架 |
| SSE | Server-Sent Events | 服务器推送事件（流式传输协议） |
| LLM | Large Language Model | 大语言模型 |
| Tool / Function Calling | - | LLM 调用外部工具/函数的能力 |
| jnpfKey | - | 表单设计器中控件类型的唯一标识符 |
| Few-Shot | - | 在 Prompt 中提供少量示例引导 LLM 输出的方法 |
| Structured Output | - | 强制 LLM 输出符合 JSON Schema 的结构化数据 |
| Router Agent | - | 负责意图分类的智能体 |
| Planner Agent | - | 负责任务分解的智能体 |
| Worker Agent | - | 负责执行具体工具调用的智能体 |
| Synthesize Agent | - | 负责汇总结果并生成自然语言回复的智能体 |
| Verifier Agent | - | 负责校验输出正确性的智能体 |
| Memory Agent | - | 负责管理对话上下文和用户偏好的智能体 |

---

> **文档结束**

> **下一步行动**：
> 1. 前端团队提取表单设计器 `jnpfKey` 完整映射及控件默认配置
> 2. 后端团队创建 `IotPlatform.AI` 项目骨架（按第一阶段步骤）
> 3. 确认 LLM 提供者选型（云端 vs 本地部署）
> 4. 准备 5-10 个 Few-Shot 表单示例
