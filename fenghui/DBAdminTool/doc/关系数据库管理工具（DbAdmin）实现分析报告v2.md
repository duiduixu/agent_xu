# 关系数据库管理工具（DbAdmin）实现分析报告

> 基于 `db-admin-backend-design.md` 需求文档，结合项目现有代码资产分析  
> 分析日期：2026-07-16

---

## 一、现有代码资产盘点

### 1.1 已实现的核心模块

| 模块 | 文件路径 | 功能状态 |
|------|----------|----------|
| 数据库连接管理 | `Systems.Core/System/DbLink/DbLinkService.cs` | 基础 CRUD 完备，含缓存层 |
| 数据库建模 | `IotPlatform.DataWeaving/Servers/DbEntityManageService.cs` | 表/字段管理，DDL 操作 |
| 数据查询 | `DbLinkService.cs` 中的 `/table/` 路由 | 分页查询、预览、动态 SQL |
| 数据库管理器 | `Common.Core/Manager/DataBase/DataBaseManager.cs` | 核心执行层，封装 SqlSugar |

### 1.2 现有实体模型

| 实体 | 数据库表 | 用途 |
|------|----------|------|
| `DbLink` | `business_dbLink` | 数据库连接配置（Host/Port/User/Password/DbType 等） |
| `DbEntityManage` | `business_dbEntityManage` | 实体（表）管理元数据（ConfigId/Table/Description/Tags） |

### 1.3 现有 API 路由汇总

**连接管理（/dbLink）：**
```
GET    /dbLink/page              — 分页列表
GET    /dbLink/select            — 下拉框
GET    /dbLink/detail            — 详情
POST   /dbLink                   — 新增
PUT    /dbLink                   — 修改
DELETE /dbLink                   — 删除
POST   /dbLink/dynamic-query     — 动态 SQL 执行
```

**表建模（/dbEntity/table）：**
```
GET    /dbEntity/table/list                          — 实体管理列表
GET    /dbEntity/table/{linkId}/{tableName}          — 表详情
GET    /dbEntity/table/{linkId}/{tableName}/Preview  — 预览数据
GET    /dbEntity/table/{linkId}/{tableName}/Fields   — 字段列表
POST   /dbEntity/table/{linkId}/add                  — 创建表
POST   /dbEntity/table/{linkId}/update               — 更新表
POST   /dbEntity/table/{linkId}/{tableName}/delete   — 删除表
GET    /dbEntity/table/sqlTemplateGenerator           — SQL 模板生成
```

**数据查询（/table）：**
```
GET    /table/{dbLinkId}/{tableName}/preview  — 分页预览
GET    /table/{dataBase}/executeCommand       — 执行 SQL
POST   /table/{linkId}/addFields              — 添加字段
```

### 1.4 现有架构问题

| 问题 | 严重程度 | 说明 |
|------|----------|------|
| 路由体系混乱 | 中 | `/dbEntity/table`、`/table`、`/dbLink` 三套路由，缺乏统一前缀 |
| 职责边界不清 | 中 | `DbLinkService` 既管连接又管数据查询；`DbEntityManageService` 混入表数据操作 |
| SQL 注入风险 | **高** | `GetData` 方法直接拼接 `like '%{input.SearchValue}%'`，未参数化 |
| 无方言抽象层 | **高** | `DataBaseManager` 通过 `if (DbType != DbType.MySql)` 硬编码差异 |
| 无审计日志 | 中 | 所有 DDL/DML 操作无记录，出问题无法追溯 |
| 无权限控制 | 中 | 数据源没有 `Readonly`/`DDL`/`CustomSql` 等权限标记 |
| 无 SQL 安全护栏 | **高** | 动态 SQL 执行无预分析、无高危语句拦截 |
| 表更新逻辑缺陷 | **高** | `Update` 方法直接 `Drop + Create`，表中数据全部丢失 |

---

## 二、目标架构设计

### 2.1 模块分层

```
02-应用模块/05-DbAdmin/
├── DbAdmin.Domain/          # 领域层：实体、DTO、枚举、接口定义
│   ├── Entities/            #   业务实体（DbSource, DbJob, DbOperationLog 等）
│   ├── DTOs/                #   数据传输对象
│   ├── Enums/               #   枚举定义
│   └── Interfaces/          #   核心接口（IDbDialect, IMetadataProvider 等）
│
├── DbAdmin.Infrastructure/  # 基础设施层：方言实现、元数据查询、安全分析
│   ├── Dialects/            #   方言适配器（MySql/SqlServer/PostgreSql/OpenGauss）
│   ├── Connections/         #   连接解析器（DataSourceResolver）
│   ├── Security/            #   SQL 安全分析器
│   └── Audit/               #   审计日志服务
│
├── DbAdmin.Application/     # 应用层：6 个 AppService 编排业务
│   ├── DataSourceAppService.cs
│   ├── MetadataAppService.cs
│   ├── TableDataAppService.cs
│   ├── SchemaDesignAppService.cs
│   ├── ImportExportAppService.cs
│   └── SqlConsoleAppService.cs
│
└── DbAdmin.WebApi/          # 接口层：Controller + 统一路由
    ├── DataSourceController.cs
    ├── MetadataController.cs
    ├── TableDataController.cs
    ├── SchemaDesignController.cs
    ├── ImportExportController.cs
    └── SqlConsoleController.cs
```

### 2.2 分层职责

| 层 | 职责 | 依赖方向 |
|----|------|----------|
| **Domain** | 定义实体、DTO、枚举、核心接口 | 无业务依赖 |
| **Infrastructure** | 实现方言适配、元数据查询、SQL 安全分析、审计持久化 | Domain |
| **Application** | 业务编排，调用 Infrastructure 完成用例 | Domain + Infrastructure |
| **WebApi** | 接收 HTTP 请求，参数校验，调用 Application | Application |

### 2.3 与现有代码的共存策略

```
┌─────────────────────────────────────────────────┐
│                   HTTP Request                   │
└──────────┬──────────────────────┬───────────────┘
           │                      │
    旧路由（冻结）            新路由（新增）
    /dbLink/*                /api/db-sources/*
    /dbEntity/table/*        /api/db-sources/{id}/tables/*
    /table/*                 /api/db-sources/{id}/sql/*
           │                      │
    仅修复缺陷/兼容          DbAdmin.WebApi
    不再承接新需求           DbAdmin.Application
           │                      │
           └──────────┬───────────┘
                      │
         共享基础能力（有限复用，逐步收口）
      DataSourceResolver / Dialect / SqlSugar
```

**建议结论：不是完全重写旧接口，也不是继续在旧接口上演进，而是“旧接口冻结 + 新模块承接新增需求”。**

原因如下：

1. 现有接口职责已经明显混杂：`DbLinkService` 同时承担连接管理、表预览、动态 SQL；`DbEntityManageService` 同时承担表建模、预览、导入导出。
2. 旧实现存在明确技术债，继续叠加需求会放大风险。例如：
   - `DbEntityManageService.GetData()` 直接拼接 `like '%{input.SearchValue}%'`，有注入风险。
   - `DataBaseManager.Update()` 采用 `DropTable + CreateTable`，不适合作为正式 DDL 演进能力。
   - 导入导出当前仍是同步思路，没有任务中心、进度、失败恢复、文件生命周期管理。
3. 这批 DbAdmin 需求包含统一路由、异步任务、审计、安全护栏、多数据库方言，和旧接口的设计目标已经不是一个层级。

**具体落地策略：**

- **保留并冻结旧接口**：`DbLinkService`、`DbEntityManageService`、`ManageTableService` 仅做缺陷修复和兼容，不再新增 DbAdmin 功能。
- **复用现有实体与连接能力**：
  - 复用 `DbLink` 作为数据源实体基底，但建议扩展权限/超时字段。
  - 复用 `ISqlSugarRepository<T>`、`IDynamicApiController`、缓存模式。
  - `DataBaseManager.ChangeDataBase()` 可作为过渡期连接切换能力复用，但不应继续承担新模块全部职责。
- **新增面向 DbAdmin 的收口层**：优先增加 `IDataSourceResolver`，由它统一负责 `DbLink -> ISqlSugarClient/DbType/方言` 的解析，避免上层业务直接依赖 `DataBaseManager` 大而全接口。
- **按能力渐进替换**：
  - 连接测试、元数据读取可先复用 `DataBaseManager` 的稳定能力。
  - 表结构修改、SQL 安全分析、导入导出任务必须在新模块独立实现。
  - 后续新前端只调用 `/api/db-*` 新路由；旧前端保留原路由直到完成迁移。

**是否完全重写接口？**

不建议一刀切重写旧服务并替换全部调用方。当前项目已有在用路由，直接替换风险高、回归面大，而且旧代码中仍有一部分能力可作为过渡复用。

**是否在现有接口下改造？**

也不建议。因为旧接口的路由结构、职责边界、参数模型都不适合继续承载 DbAdmin 的目标能力，强行改造会导致“表面复用，实际持续腐化”。

因此，推荐策略是：**新建 DbAdmin 路由与应用层，旧接口只保底，不承接新需求。**

---

## 三、六大功能模块详细设计

### 3.1 数据源管理模块（DataSource）

**职责**：数据库连接配置的 CRUD、连通性测试、动态客户端创建、权限控制

**需扩展的 DbLink 字段**：

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `ReadonlyMode` | bool | false | 只读模式，禁止 INSERT/UPDATE/DELETE |
| `AllowDDL` | bool | false | 允许 DDL 操作（CREATE/ALTER/DROP） |
| `AllowCustomSql` | bool | false | 允许自定义 SQL 执行 |
| `AllowImportExport` | bool | false | 允许数据导入导出 |
| `ConnectTimeoutSeconds` | int | 15 | 连接超时时间（秒） |
| `CommandTimeoutSeconds` | int | 30 | 命令执行超时时间（秒） |

**API**：
```
GET    /api/db-sources              # 数据源列表（支持分页、搜索）
POST   /api/db-sources              # 创建数据源
PUT    /api/db-sources/{id}         # 修改数据源
DELETE /api/db-sources/{id}         # 删除数据源
POST   /api/db-sources/{id}/test    # 连通性测试
```

---

### 3.2 元数据浏览模块（Metadata）

**职责**：获取数据库/模式/表/视图/字段/索引/主键/约束等元数据，驱动前端左侧导航树和表结构页面

**核心接口**：
```csharp
public interface IMetadataProvider
{
    Task<IReadOnlyList<string>> GetDatabasesAsync(ISqlSugarClient db);
    Task<IReadOnlyList<string>> GetSchemasAsync(ISqlSugarClient db, string database);
    Task<IReadOnlyList<DbTableInfoDto>> GetTablesAsync(ISqlSugarClient db, string database, string? schema);
    Task<IReadOnlyList<DbTableInfoDto>> GetViewsAsync(ISqlSugarClient db, string database, string? schema);
    Task<IReadOnlyList<DbColumnInfoDto>> GetColumnsAsync(ISqlSugarClient db, string database, string? schema, string table);
    Task<IReadOnlyList<DbIndexInfoDto>> GetIndexesAsync(ISqlSugarClient db, string database, string? schema, string table);
    Task<string> GetTableDDLAsync(ISqlSugarClient db, string database, string? schema, string table);
}
```

**API**：
```
GET /api/db-sources/{id}/databases                          # 数据库列表
GET /api/db-sources/{id}/schemas?database=xxx               # Schema 列表
GET /api/db-sources/{id}/tables?database=xxx&schema=xxx     # 表/视图列表
GET /api/db-sources/{id}/tables/{table}/columns?database=xxx&schema=xxx    # 字段列表
GET /api/db-sources/{id}/tables/{table}/indexes?database=xxx&schema=xxx    # 索引列表
GET /api/db-sources/{id}/tables/{table}/ddl?database=xxx&schema=xxx        # 建表 DDL
```

---

### 3.3 数据浏览与编辑模块（TableData）

**职责**：表数据分页查询、结构化筛选、新增、编辑、删除

**关键设计：结构化筛选，杜绝 SQL 拼接**

```csharp
// 查询请求
public class TableQueryRequest
{
    public string Database { get; set; }
    public string? Schema { get; set; }
    public string Table { get; set; }
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public List<QueryFilterItem> Filters { get; set; } = new();
    public List<QuerySortItem> Sorts { get; set; } = new();
    public List<string>? SelectedColumns { get; set; }
}

// 筛选条件
public class QueryFilterItem
{
    public string Field { get; set; }          // 字段名
    public string Operator { get; set; }       // eq / neq / gt / lt / gte / lte / like / in / between / isnull
    public object? Value { get; set; }         // 筛选值
    public string Logic { get; set; } = "AND"; // AND / OR
}

// 排序条件
public class QuerySortItem
{
    public string Field { get; set; }
    public bool IsDesc { get; set; }
}

// 更新请求（必须带主键或唯一键）
public class UpdateRowRequest
{
    public string Database { get; set; }
    public string? Schema { get; set; }
    public string Table { get; set; }
    public Dictionary<string, object?> KeyValues { get; set; }  // 主键值
    public Dictionary<string, object?> NewValues { get; set; }  // 新值
}
```

**安全约束**：
- 更新和删除**必须带主键或唯一键**
- 如果表没有主键，**不开放**在线编辑
- 所有值**参数化**传递，后端生成参数化 SQL

**API**：
```
POST   /api/db-sources/{id}/tables/{table}/query      # 分页查询
POST   /api/db-sources/{id}/tables/{table}/rows        # 新增行
PUT    /api/db-sources/{id}/tables/{table}/rows        # 更新行（按主键）
DELETE /api/db-sources/{id}/tables/{table}/rows        # 删除行（按主键）
```

---

### 3.4 结构设计模块（SchemaDesign）

**职责**：创建表、修改表注释、增删改字段、增删索引

**核心 DTO**：
```csharp
public class CreateTableRequest
{
    public string Database { get; set; }
    public string? Schema { get; set; }
    public string TableName { get; set; }
    public string? Comment { get; set; }
    public List<ColumnDefinition> Columns { get; set; }
    public List<IndexDefinition> Indexes { get; set; }
}

public class ColumnDefinition
{
    public string Name { get; set; }
    public string DataType { get; set; }
    public int? Length { get; set; }
    public int? Precision { get; set; }
    public int? Scale { get; set; }
    public bool IsNullable { get; set; }
    public bool IsPrimaryKey { get; set; }
    public bool IsAutoIncrement { get; set; }
    public string? DefaultValue { get; set; }
    public string? Comment { get; set; }
}

public class IndexDefinition
{
    public string Name { get; set; }
    public List<string> Columns { get; set; }
    public bool IsUnique { get; set; }
    public IndexType Type { get; set; }  // BTREE / HASH / GIN / GIST
}
```

**API**：
```
POST   /api/db-sources/{id}/tables                          # 创建表
PUT    /api/db-sources/{id}/tables/{table}/meta             # 修改表注释
POST   /api/db-sources/{id}/tables/{table}/columns          # 新增字段
PUT    /api/db-sources/{id}/tables/{table}/columns/{column} # 修改字段
DELETE /api/db-sources/{id}/tables/{table}/columns/{column} # 删除字段
POST   /api/db-sources/{id}/tables/{table}/indexes          # 创建索引
DELETE /api/db-sources/{id}/tables/{table}/indexes/{index}  # 删除索引
```

---

### 3.5 导入导出模块（ImportExport）

**职责**：CSV/Excel/SQL 格式的数据导入导出，异步任务化

**关键设计决策**：
- 所有导入导出**异步任务化**，不阻塞 HTTP 请求
- 大数据量分批处理（1000~5000 行/批）
- 导出：流式写文件，**不一次性加载到内存**
- 前端轮询任务状态

#### 3.5.1 异步任务执行模型

结合当前项目现状，**建议使用“数据库任务表 + 本进程后台消费者（`BackgroundService`）”的轻量模型作为第一阶段实现，不优先引入 Furion 调度器承接导入导出任务。**

原因：

1. 导入导出属于**长时执行、需要实时进度、需要取消/失败恢复**的业务任务，不是简单的 cron 类定时任务。
2. 当前仓库未形成成熟的通用调度中心接入范式，直接把大文件导入导出挂到调度器上，只是“换一个线程池入口”，并不能自动解决并发、隔离、状态机、文件清理问题。
3. 第一阶段更需要的是**可控并发和任务状态持久化**，而不是定时调度能力。

建议设计：

- API 创建任务时只做：参数校验、文件落盘、写入 `db_job`/`db_job_file`，状态置为 `Pending`。
- `BackgroundService` 按固定轮询间隔拉取待执行任务，抢占后置为 `Running`。
- 单个任务执行时持续更新 `Progress`、`Message`、`FinishedTime`。
- 若未来有跨实例部署，再把“抢占 Pending 任务”的逻辑升级为数据库乐观锁/分布式锁即可，整体模型不用推翻。

#### 3.5.2 是否限制并行任务数

**需要，而且必须限制。**

建议采用两级并发控制：

1. **全局并发上限**：默认 `2`。
   - 导入导出都属于高 IO + 高 CPU + 高内存波动操作。
   - 对当前单体服务来说，默认并发 2 已经足够保守，后续再通过配置放开到 3~4。
2. **数据源级串行**：同一 `SourceId + Database + Table` 同时只允许 1 个运行中任务。
   - 避免同一张表并发导入、导出与结构修改互相踩踏。

建议增加配置项：

```json
{
  "DbAdmin": {
    "Job": {
      "MaxConcurrentJobs": 2,
      "MaxConcurrentImports": 1,
      "MaxConcurrentExports": 1,
      "PollingIntervalSeconds": 3,
      "HeartbeatSeconds": 10
    }
  }
}
```

补充建议：

- 导入任务优先级高于导出任务时，可采用两个 `SemaphoreSlim` 分别控流。
- 不建议在 P0 阶段做“无限排队 + 无上限并发”，那样最容易把连接池、磁盘 IO 和内存一起打满。
- 如果后续确实要统一接入 Furion 调度体系，也建议让调度器只负责**唤醒任务消费者**或做**失败重试/夜间清理**，不要直接把导入导出执行体塞进调度器。

#### 3.5.3 导入成功后的文件清理

导入任务涉及两类文件，处理策略应区分：

1. **用户上传的源文件**：如 CSV/Excel/JSON。
2. **处理中间文件**：如分片缓存、临时解析文件。

建议策略：

- 源文件先落盘到 `App_Data/DbAdmin/imports/{yyyyMMdd}/`。
- 任务成功后**不立即删除**，保留 `1~3 天` 用于追溯和人工排障。
- 超过保留期后由清理任务删除，并把 `db_job_file` 中状态更新为 `Deleted` 或记录删除时间。
- 中间临时文件在任务 `finally` 中立即删除。
- 任务失败时源文件应保留更久，建议 `7 天`，便于复盘。

原因很实际：

- 导入成功不代表业务一定认可结果，用户可能需要追查“这批数据到底是按哪个文件导进去的”。
- 立刻删源文件虽然省空间，但会让审计、复现、问题定位都变差。

建议文档补充一个后台清理规则：

- `ImportSourceFileRetentionDays = 3`
- `ImportFailedFileRetentionDays = 7`
- `TempFileRetentionHours = 6`

#### 3.5.4 导出完成后的服务器文件处理

导出文件也不建议永久保留，更不建议下载后立刻删除。

建议策略：

- 导出文件统一写入 `App_Data/DbAdmin/exports/{yyyyMMdd}/`。
- `db_job.FilePath` 保存服务端物理路径，另存 `DownloadToken` 或受控下载接口映射，避免前端直接拼物理路径。
- 任务完成后保留 `24~72 小时`，允许用户重复下载。
- 下载接口返回文件流，不暴露真实磁盘路径。
- 过期文件由后台清理任务删除，同时把任务标记为 `FileExpired` 或补充 `FileDeletedTime`。

原因：

- 很多导出任务完成后用户不会第一时间下载；若立即删除，会导致“任务成功但文件已没了”。
- 但永久保留又会让磁盘无限增长，尤其是大表导出。

建议默认值：

- `ExportFileRetentionDays = 2`
- `ExpiredDownloadBehavior = NotFoundWithFriendlyMessage`

#### 3.5.5 导入模式

| 模式 | 枚举值 | 说明 |
|------|--------|------|
| RebuildTableAndImport | 1 | 删除旧表 -> 建表 -> 导入数据 |
| TruncateAndImport | 2 | 清空表数据 -> 导入数据 |
| CreateTableOnly | 3 | 仅建表，不导入数据 |
| InsertDataOnly | 4 | 仅插入数据（表必须已存在） |

其中 `RebuildTableAndImport`、`TruncateAndImport` 都属于高风险操作，必须受 `AllowImportExport` 与 `AllowDDL/ReadonlyMode` 双重约束。

#### 3.5.6 任务表（db_job）结构

```
Id, JobType(Import/Export/SqlExecute), SourceId, DatabaseName, SchemaName,
TableName, Status(Pending/Running/Completed/Failed/Cancelled),
Progress(0-100), Message, FilePath, CreatedBy, CreatedTime, FinishedTime
```

建议补充字段：

- `LockedBy`：被哪个实例/消费者抢占
- `LockedTime`：抢占时间
- `RetryCount`：重试次数
- `FileRetentionUntil`：文件保留截止时间
- `CleanupStatus`：None/Waiting/Deleted/Expired

**API**：
```
POST /api/db-sources/{id}/tables/{table}/export    # 创建导出任务
POST /api/db-sources/{id}/tables/{table}/import    # 上传文件 + 创建导入任务
GET  /api/db-jobs/{jobId}                          # 查询任务状态和进度
GET  /api/db-jobs/{jobId}/download                 # 下载导出文件
POST /api/db-jobs/{jobId}/cancel                   # 取消任务（可选）
```

---

### 3.6 SQL 控制台模块（SqlConsole）

**职责**：自定义 SQL 执行、预分析、多语句拆分、执行历史与审计

**多层安全护栏**：

```
请求进入
  │
  ├─ 第1层：权限校验
  │   └─ AllowCustomSql == false? → 拒绝
  │
  ├─ 第2层：SQL 预分析
  │   ├─ 检测高危关键词（DROP DATABASE, TRUNCATE, ALTER USER 等）
  │   ├─ 检测是否多语句
  │   └─ 检测是否跨库操作
  │
  ├─ 第3层：执行限制
  │   ├─ 最大返回行数：1000
  │   ├─ 命令超时：30s
  │   └─ 大结果集仅允许导出，不允许直接回包
  │
  └─ 第4层：审计记录
      └─ 记录所有执行细节到 db_operation_log
```

**SQL 安全分析结果**：
```csharp
public class SqlSafetyResult
{
    public bool IsSafe { get; set; }
    public bool IsDangerous { get; set; }
    public List<string> BlockedKeywords { get; set; }  // 命中的高危关键词
    public bool IsMultiStatement { get; set; }          // 是否包含多条语句
    public bool IsCrossDatabase { get; set; }           // 是否跨库操作
    public string? ErrorMessage { get; set; }
}
```

**高危关键词黑名单**：

> 说明：黑名单的目标不是“拦住所有危险 SQL”，而是先阻断明显的高危管理语句、权限语句、跨库破坏语句。执行前仍需配合权限校验、多语句检测、返回行数限制、审计日志。

**通用高危关键词**
```
DROP DATABASE
DROP SCHEMA
TRUNCATE DATABASE
TRUNCATE TABLE
ALTER DATABASE
CREATE DATABASE
GRANT
REVOKE
DENY
SHUTDOWN
KILL
RECONFIGURE
RESTORE
BACKUP DATABASE
```

**MySQL 黑名单补充**
```
DROP USER
RENAME USER
CREATE USER
ALTER USER
SET PASSWORD
GRANT OPTION
FLUSH PRIVILEGES
INSTALL PLUGIN
UNINSTALL PLUGIN
LOAD DATA INFILE
LOAD DATA LOCAL INFILE
SELECT ... INTO OUTFILE
SELECT ... INTO DUMPFILE
CREATE FUNCTION
DROP FUNCTION
CREATE PROCEDURE
DROP PROCEDURE
CREATE TRIGGER
DROP TRIGGER
CHANGE MASTER TO
RESET MASTER
PURGE BINARY LOGS
```

**PostgreSQL 黑名单补充**
```
DROP ROLE
CREATE ROLE
ALTER ROLE
DROP OWNED BY
REASSIGN OWNED
CREATE EXTENSION
ALTER SYSTEM
COPY ... FROM PROGRAM
COPY ... TO PROGRAM
COPY ... TO STDOUT
COPY ... FROM STDIN
DO $$
SECURITY DEFINER
SET SESSION AUTHORIZATION
SET ROLE
CREATE SERVER
ALTER SERVER
CREATE FOREIGN DATA WRAPPER
CREATE EVENT TRIGGER
```

**SQL Server 黑名单补充**
```
ALTER LOGIN
CREATE LOGIN
DROP LOGIN
ALTER USER
CREATE USER
DROP USER
ALTER ROLE
CREATE ROLE
DROP ROLE
EXEC xp_cmdshell
EXEC master..xp_cmdshell
sp_configure
OPENROWSET
OPENDATASOURCE
BULK INSERT
SELECT ... INTO OUTFILE
ALTER SERVER ROLE
CREATE CREDENTIAL
ALTER CREDENTIAL
DROP CREDENTIAL
CREATE LINKED SERVER
ALTER ANY LINKED SERVER
DROP SERVER
```

**openGauss / OpenGaussDialect 黑名单补充**

openGauss 与 PostgreSQL 高度相似，建议**默认继承 PostgreSQL 黑名单**，并额外补充：

```
CREATE NODE
ALTER NODE
DROP NODE
CREATE DIRECTORY
DROP DIRECTORY
gs_encrypt
COPY ... FROM PROGRAM
COPY ... TO PROGRAM
```

实现建议：

- `OpenGaussDialect : PostgreSqlDialect`，先复用 PostgreSQL 的安全分析逻辑和大部分元数据查询。
- `OpenGaussDialect.AnalyzeSqlSafety()` 在父类结果上追加 openGauss 特有关键字，不要另起一套完全独立实现。
- 黑名单匹配不要只做简单 `Contains`，至少要先：
  - 去注释
  - 统一大小写
  - 压缩空白字符
  - 再按关键短语或正则匹配

这样可以先挡住绝大多数管理类高危操作，也避免因为大小写/换行导致绕过。

**API**：
```
POST /api/db-sources/{id}/sql/preview    # SQL 预分析（不执行，返回安全分析结果）
POST /api/db-sources/{id}/sql/execute    # 执行 SQL（经过安全护栏）
GET  /api/db-sources/{id}/sql/history    # 查询执行历史
```

---

## 四、数据库方言层设计（核心架构）

### 4.1 设计原则

- **SqlSugar 是执行器与基础能力提供者**，不是完整的数据库管理抽象模型
- **方言层**负责隔离数据库差异，但不是所有数据库操作都必须强制绕过 SqlSugar
- 所有**跨数据库差异明显**的能力，应通过方言层收口
- 不在 Service 层散落 `if/else` 判断数据库类型

### 4.1.1 是否所有数据库操作都要经过 IDbDialect 层？

**不需要，也不建议。**

更可行的边界划分是：

**必须经过 `IDbDialect` 的场景**

- 标识符包裹（表名、列名、schema 名）
- 分页 SQL 生成
- 元数据查询（库、schema、表、视图、字段、索引、DDL）
- DDL 语句生成（建表、改列、删列、建索引、删索引）
- SQL 安全分析
- 通用数据类型映射

这些能力天然存在数据库差异，是 `IDbDialect` 的核心职责。

**可以直接使用 SqlSugar 或 Resolver 的场景**

- 连接创建、连接池复用、命令超时设置
- 参数化执行 `INSERT/UPDATE/DELETE`
- 批量写入、事务控制
- 简单 `SELECT` / `COUNT` / `Any` 查询
- 文件导入后的批量入库执行

原因是：这些能力 SqlSugar 本身已经提供较成熟封装，若全部人为再包一层 `IDbDialect.ExecuteInsert/ExecuteUpdate/...`，只会把方言层做成“大而全转发器”，价值不高，维护成本反而更大。

**推荐分工方式**

```csharp
Application Service
    -> IDataSourceResolver   // 取连接、取数据源、取方言
    -> IDbDialect            // 做差异化 SQL/元数据/DDL/安全分析
    -> ISqlSugarClient       // 做参数化执行、事务、批量写入
```

也就是说：

- **方言层负责“怎么拼、怎么查、怎么识别差异”**
- **SqlSugar 负责“怎么连、怎么执行、怎么批量操作”**

这个边界比“所有操作必须过方言层”更稳，也更符合当前项目已有 `DataBaseManager`/SqlSugar 的现实基础。

### 4.2 统一接口

```csharp
public interface IDbDialect
{
    DbEngineType Engine { get; }

    // ── 标识符处理 ──
    string WrapIdentifier(string name);   // MySQL: `name`  SQLServer: [name]  PG: "name"

    // ── 分页构建 ──
    string BuildPagedQuery(string sql, int pageIndex, int pageSize, string? orderBy);

    // ── 元数据查询 ──
    Task<IReadOnlyList<string>> GetDatabasesAsync(ISqlSugarClient db);
    Task<IReadOnlyList<string>> GetSchemasAsync(ISqlSugarClient db, string database);
    Task<IReadOnlyList<DbTableInfoDto>> GetTablesAsync(ISqlSugarClient db, string database, string? schema);
    Task<IReadOnlyList<DbTableInfoDto>> GetViewsAsync(ISqlSugarClient db, string database, string? schema);
    Task<IReadOnlyList<DbColumnInfoDto>> GetColumnsAsync(ISqlSugarClient db, string database, string? schema, string table);
    Task<IReadOnlyList<DbIndexInfoDto>> GetIndexesAsync(ISqlSugarClient db, string database, string? schema, string table);
    Task<string> GetTableDDLAsync(ISqlSugarClient db, string database, string? schema, string table);

    // ── DDL 生成 ──
    string BuildCreateTableSql(CreateTableRequest request);
    string BuildAddColumnSql(AddColumnRequest request);
    string BuildAlterColumnSql(AlterColumnRequest request);
    string BuildDropColumnSql(DropColumnRequest request);
    string BuildCreateIndexSql(CreateIndexRequest request);
    string BuildDropIndexSql(DropIndexRequest request);

    // ── SQL 安全分析 ──
    SqlSafetyResult AnalyzeSqlSafety(string sql);

    // ── 类型映射 ──
    string MapDataType(string genericType, int? length, int? precision, int? scale);
}
```

### 4.3 方言实现类层次

```
IDbDialect
├── MySqlDialect          # MySQL 5.7+ / 8.0
├── SqlServerDialect      # SQL Server 2016+
├── PostgreSqlDialect     # PostgreSQL 12+
└── OpenGaussDialect : PostgreSqlDialect  # 继承 PG，覆盖差异
```

> 说明：当前仓库 `DbTypeEnum` 只有 `MySql / SqlServer / PostgreSql / KingbaseEs`，尚未定义 openGauss。若本次需求必须覆盖 openGauss，建议新增 `DbTypeEnum.OpenGauss`；若短期先复用 PostgreSQL 连接能力，也至少应在 DbAdmin 模块内单独保留 `OpenGaussDialect` 类型，避免后续和 PostgreSQL 分支继续耦合。

### 4.4 关键方言差异对照

| 差异点 | MySQL | SQL Server | PostgreSQL | OpenGauss |
|--------|-------|-----------|------------|-----------|
| 标识符 | `` `name` `` | `[name]` | `"name"` | `"name"` |
| 分页 | `LIMIT N OFFSET M` | `OFFSET M ROWS FETCH NEXT N ROWS ONLY` | `LIMIT N OFFSET M` | `LIMIT N OFFSET M` |
| 自增列 | `AUTO_INCREMENT` | `IDENTITY(1,1)` | `SERIAL` / `GENERATED BY DEFAULT AS IDENTITY` | `SERIAL` |
| 注释-表 | `COMMENT='xxx'` | `sp_addextendedproperty` | `COMMENT ON TABLE` | `COMMENT ON TABLE` |
| 注释-列 | `COMMENT 'xxx'` | `sp_addextendedproperty` | `COMMENT ON COLUMN` | `COMMENT ON COLUMN` |
| 修改字段 | `MODIFY COLUMN` | `ALTER COLUMN` | `ALTER COLUMN TYPE` | `ALTER COLUMN TYPE` |
| 元数据源 | `information_schema` | `sys.tables/columns` | `information_schema` | `pg_catalog` |
| 布尔类型 | `TINYINT(1)` | `BIT` | `BOOLEAN` | `BOOLEAN` |
| 字符串类型 | `VARCHAR(N)` | `NVARCHAR(N)` | `VARCHAR(N)` | `VARCHAR(N)` |

### 4.5 方言注册与解析

```csharp
// 方言工厂
public interface IDialectFactory
{
    IDbDialect GetDialect(DbEngineType engineType);
}

// 连接解析器
public interface IDataSourceResolver
{
    Task<ISqlSugarClient> GetClientAsync(long sourceId);
    Task<IDbDialect> GetDialectAsync(long sourceId);
    Task<DbSource> GetSourceAsync(long sourceId);
}
```

---

## 五、安全机制设计

### 5.1 数据加密

| 层级 | 措施 |
|------|------|
| 存储 | 数据库密码使用 AES-256-CBC 加密存储，密钥由环境变量/配置中心注入 |
| 传输 | 全站 HTTPS，API 参数通过请求体传输 |
| 内存 | 连接字符串中的密码解密后仅在内存中保持，不写入日志 |

### 5.2 权限控制矩阵

| 操作 | ReadonlyMode | AllowDDL | AllowCustomSql | AllowImportExport |
|------|:---:|:---:|:---:|:---:|
| 查询元数据 | ✓ | - | - | - |
| SELECT 查询 | ✓ | - | - | - |
| INSERT/UPDATE/DELETE | ✗ | - | - | - |
| CREATE TABLE | ✗ | ✓ | - | - |
| ALTER TABLE | ✗ | ✓ | - | - |
| DROP TABLE | ✗ | ✓ | - | - |
| 自定义 SQL | ✗ | - | ✓ | - |
| 数据导出 | - | - | - | ✓ |
| 数据导入 | ✗ | - | - | ✓ |

### 5.3 SQL 注入防护

| 场景 | 防护措施 |
|------|----------|
| 表数据查询 | 只接受结构化 `QueryFilterItem`，后端生成参数化 SQL |
| 表数据更新 | 只接受 `KeyValues` + `NewValues` 字典，参数化绑定 |
| 自定义 SQL | 预分析高危关键词 + 权限校验 + 参数化 |
| 表名/字段名 | 白名单校验（仅允许元数据中存在的表/字段） |

### 5.4 审计日志（db_operation_log）

| 字段 | 类型 | 说明 |
|------|------|------|
| Id | long | 主键 |
| OperatorId | long | 操作人 ID |
| OperatorName | string | 操作人名称 |
| SourceId | long | 数据源 ID |
| SourceName | string | 数据源名称 |
| DatabaseName | string | 目标数据库 |
| SchemaName | string? | 目标模式 |
| TableName | string? | 目标表 |
| OperationType | int | 操作类型枚举：Query/Insert/Update/Delete/DDL/Import/Export/SqlExecute |
| SqlDigest | string | SQL 摘要（截取前 500 字符） |
| AffectedRows | int? | 影响行数 |
| ClientIp | string | 客户端 IP |
| DurationMs | long | 耗时（毫秒） |
| IsSuccess | bool | 是否成功 |
| ErrorMessage | string? | 错误信息 |
| CreatedTime | DateTime | 操作时间 |

---

## 六、管理表设计

需在业务库中创建以下表：

| 表名 | 用途 | 关键字段 |
|------|------|----------|
| `db_source_permission` | 数据源-用户权限关联 | SourceId, UserId, CanRead, CanWrite, CanDDL, CanCustomSql, CanImportExport |
| `db_operation_log` | 操作审计日志 | 见 5.4 节 |
| `db_sql_history` | SQL 执行历史 | SourceId, Sql, SqlDigest, AffectedRows, DurationMs, CreatedBy, CreatedTime |
| `db_job` | 导入导出任务 | JobType, SourceId, Status, Progress, FilePath, CreatedBy, CreatedTime, FinishedTime |
| `db_job_file` | 任务关联文件 | JobId, FileName, FileSize, FilePath, UploadTime |

---

## 七、性能优化策略

| 场景 | 策略 |
|------|------|
| 大表分页查询 | 使用方言层分页语法，避免 `OFFSET` 过大导致性能下降（游标分页备选） |
| 元数据查询 | 使用各数据库系统表直接查询，单次返回所有信息，避免 N+1 |
| 大数据量导出 | 流式写入文件，分批 `SELECT`（每次 5000 行），不一次性加载到内存 |
| 大数据量导入 | 分批 `INSERT`（1000~5000 行/批），使用事务包裹，异步任务执行 |
| 连接池 | 复用 SqlSugar 连接池，每个数据源独立 `SqlSugarScope` |
| 缓存 | 数据源配置缓存（已有 `SysCacheService`），表结构元数据短期缓存（5 分钟） |

### 7.1 结合现有代码的额外落地建议

结合当前仓库代码，以下几点建议应明确写入实施方案：

1. **先收口查询拼接风险，再扩功能**
   - 现有 `DbEntityManageService.GetData()` 已存在 `LIKE` 字符串拼接。
   - 新模块中所有筛选都必须转成结构化 DTO + 参数化执行，旧接口只做必要修补，不继续扩展。

2. **禁止沿用 `Drop + Create` 作为正式更新表方案**
   - 当前 `DataBaseManager.Update()` 的实现不适合作为 DbAdmin 的结构变更基础。
   - 新模块中的 DDL 必须改为“差异化 alter 语句生成”，至少保证不因修改注释/列定义就整体删表。

3. **`DataBaseManager` 只做过渡复用，不继续扩张职责**
   - 可复用连接切换、基础元数据能力。
   - 不建议继续把 SQL 安全分析、导入导出任务编排、方言差异处理继续塞进 `DataBaseManager`。

4. **导入导出要显式限制磁盘占用**
   - 除保留天数外，建议增加按目录总大小或按任务数的兜底清理策略。
   - 避免导出文件长期堆积导致服务盘被打满。

---

## 八、实施路线图

### 第一阶段：核心功能（P0）

聚焦功能实现，打通数据源连接 → 元数据浏览 → 数据查询 → 数据编辑 → 导出的完整链路。

| 序号 | 任务 | 依赖 |
|------|------|------|
| 1 | 创建 DbAdmin.Domain 项目（实体、DTO、枚举、接口） | - |
| 2 | 创建 DbAdmin.Infrastructure（方言层 + 元数据 Provider） | 1 |
| 3 | 数据源管理（CRUD + 连通性测试，暂不涉及权限字段） | 1, 2 |
| 4 | 元数据浏览（数据库/模式/表/视图/字段/索引查询，驱动左侧树） | 1, 2 |
| 5 | 表数据分页查询（结构化筛选 + 参数化） | 1, 2 |
| 6 | 单行新增/编辑/删除（主键校验 + 参数化） | 5 |
| 7 | CSV 导出（流式写入） | 1, 2 |
| 8 | 创建 DbAdmin.WebApi（Controller + 统一路由） | 3-7 |

### 第二阶段：管理增强（P1）

扩展 DDL 能力，支持表结构在线设计，增加导入和 SQL 控制台。

| 序号 | 任务 |
|------|------|
| 9 | 创建表 + 字段管理 + 索引管理（DDL 方言层） |
| 10 | CSV 导入（异步任务 + 分批处理） |
| 11 | 自定义 SQL 执行（基础版，先跑通功能） |
| 12 | Excel 导入导出 |
| 13 | openGauss 完整兼容 |
| 14 | SQL 执行计划展示 |

### 第三阶段：安全与权限（P2）

为已成型的功能补齐安全机制，不影响前期核心功能开发节奏。

| 序号 | 任务 |
|------|------|
| 15 | SQL 安全分析器（高危语句拦截 + 多语句检测 + 跨库检测） |
| 16 | 审计日志（db_operation_log 记录所有 DDL/DML 操作） |
| 17 | 数据源权限控制（ReadonlyMode / AllowDDL / AllowCustomSql / AllowImportExport） |
| 18 | SQL 执行历史查询 |
| 19 | 多语句 SQL 执行（含完整安全护栏） |
| 20 | 旧路由逐步迁移 |

---

## 九、风险评估与应对

| 风险 | 等级 | 影响 | 应对措施 |
|------|:---:|------|----------|
| 方言差异导致 DDL 错误 | **高** | 多数据库建表/改表失败 | 每个方言独立实现 + 单元测试覆盖 + 先在测试环境验证 |
| 大数据量导入导出 OOM | 中 | 服务崩溃 | 异步任务 + 流式处理 + 分批写入 + 并发上限 |
| openGauss 兼容性不足 | 中 | openGauss 用户不可用 | 继承 PG 方言，逐步覆盖差异，与 openGauss 文档对照 |
| 与现有功能冲突 | 低 | 旧功能异常 | 独立路由前缀，新旧并行运行 |
| 导入导出文件未清理 | 中 | 磁盘持续增长，最终影响服务可用性 | 增加保留期、后台清理任务、目录容量兜底策略 |
| 同表并发导入/导出 | 中 | 数据不一致、表锁竞争、用户感知混乱 | 全局并发控制 + 同数据源同表串行执行 |

---

## 十、总结

当前项目已具备连接管理、基础 DDL、数据查询三大能力，但存在以下核心短板：

1. **缺乏方言抽象层** — 数据库差异散落在 `if/else` 中，不可维护
2. **缺乏任务化机制** — 导入导出同步执行，大数据量场景不可用
3. **缺乏安全机制** — SQL 注入风险、无高危语句拦截、无审计（P2 阶段补齐）
4. **缺乏权限控制** — 数据源无细粒度操作权限（P2 阶段补齐）

建议按本报告方案，新建 `DbAdmin` 独立模块：

- **以 SqlSugar 做连接和执行基础**
- **以方言层做多数据库差异隔离**
- **以结构化 DTO 做筛选和参数化**
- **P0/P1 先聚焦功能实现，打通核心链路**
- **P2 再补齐安全、权限、审计等机制**

新旧模块并行运行，逐步迁移，降低风险。