# DbAdmin 最终开发文档

> 文档版本：v1.0  
> 生成日期：2026-07-20  
> 适用范围：IotPlatform 中关系数据库管理工具（DbAdmin）后端开发实施  
> 文档用途：作为 Claude / 其他 AI / 开发人员的直接实现依据

---

## 1. 文档目标

本文档用于定义 DbAdmin 模块的最终开发规范，统一以下内容：

- 模块目标与实施范围
- 分层架构与代码落位
- 数据实体与基础约束
- 各功能模块职责、接口、输入输出要求
- SQL 安全、日志、异常处理规范
- 与现有代码的复用边界
- 分阶段实施顺序

本文件是开发落地文档，不再保留分析型表述。实现时应以本文档为准。

---

## 2. 总体原则

### 2.1 实施策略

DbAdmin 采用“旧接口冻结，新模块承接新增需求”的策略：

- 旧接口：`DbLinkService`、`DbEntityManageService`、`ManageTableService` 仅做兼容和必要缺陷修复。
- 新需求：统一进入 `DbAdmin` 模块实现。
- 新前端：只调用 DbAdmin 新路由。
- 旧前端：保留原路由，后续逐步迁移。

### 2.2 复用原则

优先复用以下现有能力：

- `DbLink`：作为数据源实体基础，直接复用现有数据源功能。
- `ISqlSugarRepository<T>`：用于数据访问。
- `IDynamicApiController`、`ITransient`：用于 `DbAdmin.Service` 层接口暴露。
- `SqlSugar`：作为连接、参数化执行、事务、批量写入基础。
- `DataBaseManager.ChangeDataBase()`：可作为过渡期连接切换能力复用。
- `SysCacheService`：继续用于数据源配置缓存等场景。

### 2.3 禁止事项

实现中明确禁止以下做法：

- 不得新增数据源权限字段。
- 不得新增数据源权限控制表。
- 不得引入单独的 `WebApi` 项目或 Controller 层。
- 不得继续沿用 `Drop + Create` 作为正式结构变更方案。
- 不得在业务层通过字符串拼接生成查询条件 SQL。
- 不得将数据库密码、完整连接串、密钥等敏感信息直接写入日志。

---

## 3. 范围定义

### 3.1 本次必须实现

1. 数据源管理
2. 元数据浏览
3. 表数据分页查询
4. 单行新增、更新、删除
5. 表结构设计基础能力
6. 基础导入导出能力
7. SQL 控制台基础能力
8. SQL 安全分析
9. 审计日志与执行历史
10. Info / Error 日志与统一异常处理

### 3.2 本次明确不实现

1. 数据源权限控制
2. 数据源权限字段扩展
3. `db_source_permission` 表
4. 导入导出异步任务化
5. 后台消费者 / 任务中心 / 进度轮询 / 取消任务
6. `db_job` / `db_job_file` 表
7. 独立 `DbAdmin.WebApi` 层

### 3.3 后续可扩展但不在本次范围

1. 大数据量任务化导入导出
2. 更复杂的批量编辑能力
3. 更多导入导出格式扩展
4. SQL 执行计划高级展示
5. 更细粒度的审批、授权、权限体系

---

## 4. 模块分层与目录规范

### 4.1 模块结构

```text
02-应用模块/06-DbAdmin/
├── DbAdmin.Entity/
│   ├── Entity/
│   ├── Dto/
│   └── Enum/
│
├── DbAdmin.Interface/
│   └── Interface/
│
├── DbAdmin.Infrastructure/
│   ├── Dialects/
│   ├── Connections/
│   └── Security/
│
└── DbAdmin.Service/
    ├── DataSourceAppService.cs
    ├── MetadataAppService.cs
    ├── TableDataAppService.cs
    ├── SchemaDesignAppService.cs
    ├── ImportExportAppService.cs
    └── SqlConsoleAppService.cs
```

### 4.2 各层职责

#### Entity

只放以下内容：

- Entity
- DTO
- Enum

不得放业务编排逻辑。

#### Interface

只放跨模块使用的接口定义，包括但不限于：

- 数据源解析接口
- 方言接口
- 元数据查询接口
- 其他需要被 `Service`、`Infrastructure` 共同依赖的抽象接口

#### Infrastructure

负责以下基础设施能力：

- 数据库方言实现
- 元数据查询实现
- 数据源解析
- SQL 安全分析

#### Service

负责以下内容：

- 接口暴露
- 业务编排
- 参数校验
- 日志记录
- 异常处理
- 审计落库

### 4.3 接口暴露方式

所有接口统一在 `DbAdmin.Service` 层通过以下方式暴露：

- 实现 `IDynamicApiController`
- 生命周期使用 `ITransient`

不再新增 Controller。

---

## 5. 实体与基础建模规范

### 5.1 实体基类要求

所有 DbAdmin 新增业务实体必须继承 `EntityBase`，统一获得并维护以下运维字段：

- 创建人
- 创建时间
- 修改人
- 修改时间
- 其他 `EntityBase` 已提供字段

### 5.2 运维字段赋值要求

在新增和修改时，应用层必须显式设置相关字段值：

- 新增：设置创建人、创建时间、修改人、修改时间
- 修改：更新修改人、修改时间

不得依赖“希望底层自动带出”的隐式行为，必须在应用层逻辑中明确处理。

### 5.3 主键规范

DbAdmin 新建业务管理表主键统一使用雪花 ID。

适用对象包括但不限于：

- `db_operation_log`
- `db_sql_history`
- 后续新增的 DbAdmin 业务管理表

### 5.4 数据源实体规范

数据源直接复用现有 `DbLink`，本次不做以下改动：

- 不新增 `ReadonlyMode`
- 不新增 `AllowDDL`
- 不新增 `AllowCustomSql`
- 不新增 `AllowImportExport`
- 不新增权限维度设计字段

如需补充运维字段，应通过实体基类继承体系处理，而不是通过数据源权限扩展处理。

---

## 6. 路由与接口统一规范

### 6.1 路由风格

接口统一使用 `GET` 或 `POST`。

强制要求：

- 查询类接口使用 `GET` 或 `POST`
- 新增、修改、删除统一使用 `POST`
- 文档和实现中不使用 `PUT`
- 文档和实现中不使用 `DELETE`

### 6.2 路由前缀

统一使用以下前缀：

- `/api/db-sources/*`
- `/api/db-sources/{id}/tables/*`
- `/api/db-sources/{id}/sql/*`

---

## 7. 功能模块开发规范

## 7.1 数据源管理模块

### 职责

- 数据源列表查询
- 数据源创建
- 数据源修改
- 数据源删除
- 数据源连通性测试
- 动态客户端创建

### 开发要求

- 直接复用现有 `DbLink` 的业务语义
- 不扩展权限控制能力
- 允许继续复用现有仓储、缓存模式

### 接口

```text
GET  /api/db-sources
POST /api/db-sources
POST /api/db-sources/{id}/update
POST /api/db-sources/{id}/delete
POST /api/db-sources/{id}/test
```

---

## 7.2 元数据浏览模块

### 职责

用于驱动左侧数据库树和表结构展示，支持获取：

- 数据库列表
- Schema 列表
- 表列表
- 视图列表
- 字段列表
- 索引列表
- 建表 DDL

### 核心接口

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

### 接口

```text
GET /api/db-sources/{id}/databases
GET /api/db-sources/{id}/schemas?database=xxx
GET /api/db-sources/{id}/tables?database=xxx&schema=xxx
GET /api/db-sources/{id}/tables/{table}/columns?database=xxx&schema=xxx
GET /api/db-sources/{id}/tables/{table}/indexes?database=xxx&schema=xxx
GET /api/db-sources/{id}/tables/{table}/ddl?database=xxx&schema=xxx
```

---

## 7.3 表数据浏览与编辑模块

### 职责

- 表数据分页查询
- 结构化筛选
- 单行新增
- 单行更新
- 单行删除

### 强制约束

1. 所有查询条件必须结构化表达，不允许前端传原始拼接 SQL。
2. 所有值必须参数化传递。
3. 更新和删除必须带主键或唯一键。
4. 若表没有主键，则不开放在线编辑。
5. 表名、字段名必须做白名单校验，只允许元数据中存在的对象。

### 推荐请求模型

```csharp
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

public class QueryFilterItem
{
    public string Field { get; set; }
    public string Operator { get; set; }
    public object? Value { get; set; }
    public string Logic { get; set; } = "AND";
}

public class QuerySortItem
{
    public string Field { get; set; }
    public bool IsDesc { get; set; }
}

public class UpdateRowRequest
{
    public string Database { get; set; }
    public string? Schema { get; set; }
    public string Table { get; set; }
    public Dictionary<string, object?> KeyValues { get; set; }
    public Dictionary<string, object?> NewValues { get; set; }
}
```

### 接口

```text
POST /api/db-sources/{id}/tables/{table}/query
POST /api/db-sources/{id}/tables/{table}/rows/add
POST /api/db-sources/{id}/tables/{table}/rows/update
POST /api/db-sources/{id}/tables/{table}/rows/delete
```

---

## 7.4 结构设计模块

### 职责

- 创建表
- 修改表注释
- 新增字段
- 修改字段
- 删除字段
- 创建索引
- 删除索引

### 强制约束

1. 不允许继续使用 `Drop + Create` 作为更新表方案。
2. 必须生成差异化 DDL。
3. 修改表、修改字段、修改索引时应优先生成最小影响范围 SQL。
4. DDL 必须通过方言层统一收口。

### 推荐 DTO

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
    public IndexType Type { get; set; }
}
```

### 接口

```text
POST /api/db-sources/{id}/tables
POST /api/db-sources/{id}/tables/{table}/meta/update
POST /api/db-sources/{id}/tables/{table}/columns
POST /api/db-sources/{id}/tables/{table}/columns/{column}/update
POST /api/db-sources/{id}/tables/{table}/columns/{column}/delete
POST /api/db-sources/{id}/tables/{table}/indexes
POST /api/db-sources/{id}/tables/{table}/indexes/{index}/delete
```

---

## 7.5 导入导出模块

### 职责

提供基础导入导出能力，支持 CSV / Excel / SQL 相关场景。

### 本次边界

本次只做基础能力：

- 导入
- 导出
- 中小数据量场景可用
- 同步接口返回结果或文件流

本次不做：

- 异步任务化
- 任务状态查询
- 取消任务
- 后台调度
- 文件生命周期管理

### 导入模式

```text
RebuildTableAndImport = 1
TruncateAndImport     = 2
CreateTableOnly       = 3
InsertDataOnly        = 4
```

### 开发要求

- 导入时采用分批写入。
- 导出时采用流式输出。
- 避免一次性加载全部数据到内存。
- 如遇超大数据量场景，本次以限制单次操作规模为主，不额外引入任务体系。

### 接口

```text
POST /api/db-sources/{id}/tables/{table}/export
POST /api/db-sources/{id}/tables/{table}/import
```

---

## 7.6 SQL 控制台模块

### 职责

- SQL 预分析
- SQL 执行
- SQL 历史查询
- SQL 审计落库

### 查询返回规范

SQL 控制台查询结果必须分页返回，要求如下：

1. 不再使用“最多返回 1000 行”的固定限制。
2. 默认返回前 20 条数据。
3. 支持 `pageIndex`、`pageSize` 翻页。
4. 用户可持续翻页查看后续结果，交互行为参考阿里云 DMS。
5. 不允许一次性全量回包。
6. 分页 SQL 必须通过方言层生成。

### 安全护栏

执行流程如下：

```text
请求进入
  -> SQL 预分析
  -> 高危语句拦截
  -> 跨库检测
  -> 分页化执行
  -> 审计记录
```

### SQL 安全分析结果模型

```csharp
public class SqlSafetyResult
{
    public bool IsSafe { get; set; }
    public bool IsDangerous { get; set; }
    public List<string> BlockedKeywords { get; set; }
    public bool IsMultiStatement { get; set; }
    public bool IsCrossDatabase { get; set; }
    public string? ErrorMessage { get; set; }
}
```

### 高危语句处理要求

- 必须先去注释。
- 必须统一大小写。
- 必须压缩空白字符。
- 必须按关键短语或正则进行匹配。
- 不能只做简单 `Contains`。

### 接口

```text
POST /api/db-sources/{id}/sql/preview
POST /api/db-sources/{id}/sql/execute
GET  /api/db-sources/{id}/sql/history
```

---

## 8. 方言层开发规范

## 8.1 方言层职责边界

### 必须走方言层的能力

- 标识符包裹
- 分页 SQL 生成
- 元数据查询
- DDL SQL 生成
- SQL 安全分析
- 通用类型映射

### 可以直接使用 SqlSugar 的能力

- 连接创建
- 连接池复用
- 参数化 `INSERT/UPDATE/DELETE`
- 批量写入
- 事务控制
- 简单查询

### 推荐调用关系

```text
Application Service
    -> IDataSourceResolver
    -> IDbDialect
    -> ISqlSugarClient
```

## 8.2 统一接口

```csharp
public interface IDbDialect
{
    DbEngineType Engine { get; }
    string WrapIdentifier(string name);
    string BuildPagedQuery(string sql, int pageIndex, int pageSize, string? orderBy);
    Task<IReadOnlyList<string>> GetDatabasesAsync(ISqlSugarClient db);
    Task<IReadOnlyList<string>> GetSchemasAsync(ISqlSugarClient db, string database);
    Task<IReadOnlyList<DbTableInfoDto>> GetTablesAsync(ISqlSugarClient db, string database, string? schema);
    Task<IReadOnlyList<DbTableInfoDto>> GetViewsAsync(ISqlSugarClient db, string database, string? schema);
    Task<IReadOnlyList<DbColumnInfoDto>> GetColumnsAsync(ISqlSugarClient db, string database, string? schema, string table);
    Task<IReadOnlyList<DbIndexInfoDto>> GetIndexesAsync(ISqlSugarClient db, string database, string? schema, string table);
    Task<string> GetTableDDLAsync(ISqlSugarClient db, string database, string? schema, string table);
    string BuildCreateTableSql(CreateTableRequest request);
    string BuildAddColumnSql(AddColumnRequest request);
    string BuildAlterColumnSql(AlterColumnRequest request);
    string BuildDropColumnSql(DropColumnRequest request);
    string BuildCreateIndexSql(CreateIndexRequest request);
    string BuildDropIndexSql(DropIndexRequest request);
    SqlSafetyResult AnalyzeSqlSafety(string sql);
    string MapDataType(string genericType, int? length, int? precision, int? scale);
}
```

## 8.3 方言实现类

```text
IDbDialect
├── MySqlDialect
├── SqlServerDialect
├── PostgreSqlDialect
└── OpenGaussDialect : PostgreSqlDialect
```

### OpenGauss 要求

- 默认继承 PostgreSQL 方言能力。
- 在父类基础上补充 openGauss 差异。
- 不要单独复制一套完全重复的实现。

---

## 9. 安全规范

### 9.1 SQL 注入防护

- 表数据查询：只能接收结构化筛选 DTO。
- 表数据更新：只能接收主键字典和新值字典。
- 自定义 SQL：必须先经过预分析。
- 表名、字段名：必须白名单校验。
- 所有值：必须参数化传递。

### 9.2 SQL 控制台控制

本次不通过数据源权限字段限制 SQL 控制台能力，是否开放由业务授权和接入范围控制。

### 9.3 敏感信息保护

以下内容不得直接记录到日志：

- 数据库密码
- 完整连接串
- 密钥
- Token
- 其他敏感凭据

---

## 10. 日志与异常处理规范

## 10.1 Info 日志要求

所有关键业务流程必须记录必要的 `Info` 日志，至少包括：

- 请求入口
- 数据源标识
- 目标数据库 / Schema / 表
- 操作类型
- 入参摘要
- 执行阶段
- 结果概要
- 耗时

建议覆盖以下链路：

- 数据源管理
- 元数据查询
- 表数据查询与编辑
- 表结构变更
- 导入导出
- SQL 控制台执行

## 10.2 Error 日志要求

所有异常必须记录详细 `Error` 日志，至少包括：

- 异常消息
- 异常堆栈
- 操作类型
- 数据源标识
- 目标数据库 / Schema / 表
- 关键参数
- SQL 摘要或请求摘要
- 当前执行阶段

### 关键原则

- 日志必须足以直接定位线上问题。
- 客户端返回信息应收敛，日志保留完整排障细节。
- 敏感信息必须脱敏。

## 10.3 统一异常处理

应用层需统一处理异常，要求：

1. 记录完整错误日志。
2. 返回统一错误结构。
3. 对外只暴露必要错误描述。
4. 审计类异常与业务类异常都要落日志。

---

## 11. 管理表设计

本次需要落地的管理表如下：

| 表名 | 用途 |
|------|------|
| `db_operation_log` | 操作审计日志 |
| `db_sql_history` | SQL 执行历史 |

### 11.1 db_operation_log

建议字段：

- `Id`：雪花 ID 主键
- `OperatorId`
- `OperatorName`
- `SourceId`
- `SourceName`
- `DatabaseName`
- `SchemaName`
- `TableName`
- `OperationType`
- `SqlDigest`
- `AffectedRows`
- `ClientIp`
- `DurationMs`
- `IsSuccess`
- `ErrorMessage`
- `CreatedTime`

### 11.2 db_sql_history

用于保留 SQL 执行历史，建议至少包含：

- `Id`
- `SourceId`
- `Sql`
- `SqlDigest`
- `AffectedRows`
- `DurationMs`
- `CreatedBy`
- `CreatedTime`

### 11.3 本次不创建的表

本次明确不创建：

- `db_source_permission`
- `db_job`
- `db_job_file`

---

## 12. 性能规范

### 12.1 查询与分页

- 大表分页必须使用方言层分页语法。
- 避免在大偏移量场景下直接使用低效分页方式，必要时保留游标分页扩展空间。

### 12.2 元数据查询

- 优先直接查询数据库系统表。
- 尽量避免 N+1 查询。

### 12.3 导入导出

- 导出必须流式写出。
- 导入必须分批写入。
- 事务粒度要兼顾性能与回滚成本。

### 12.4 连接与缓存

- 复用 SqlSugar 连接池。
- 每个数据源独立 `SqlSugarScope`。
- 元数据允许短期缓存。

---

## 13. 与现有代码的衔接要求

### 13.1 必须修正的问题

1. `DbEntityManageService.GetData()` 中的字符串拼接筛选风险。
2. `DataBaseManager.Update()` 中 `Drop + Create` 的危险实现方式。
3. 旧模块职责混杂导致的扩展困难问题。

### 13.2 过渡期复用边界

可以复用：

- 连接切换
- 基础元数据能力
- 仓储与缓存基础设施

不应继续塞入 `DataBaseManager` 的能力：

- SQL 安全分析
- 导入导出编排
- 方言差异处理
- 新增 DbAdmin 业务职责

---

## 14. 实施阶段

## 14.1 第一阶段（P0）

目标：先打通核心链路。

1. 创建 `DbAdmin.Entity`
2. 创建 `DbAdmin.Interface`
3. 创建 `DbAdmin.Infrastructure`
4. 创建 `DbAdmin.Service`
5. 数据源管理
6. 元数据浏览
7. 表数据分页查询
8. 单行新增 / 更新 / 删除
9. 基础导入导出
10. Info / Error 日志与统一异常处理框架

## 14.2 第二阶段（P1）

目标：补齐管理增强能力。

1. 创建表 / 字段 / 索引管理
2. SQL 控制台执行与分页返回
3. SQL 执行历史查询
4. 审计日志落库
5. openGauss 完整兼容
6. SQL 执行计划展示

---

## 15. AI 实现指令

以下要求适用于 Claude 或其他 AI 在本仓库中进行代码实现时执行：

### 15.1 实现优先级

按以下顺序实施：

1. `DbAdmin.Entity` 建模
2. `DbAdmin.Interface` 抽象定义
3. `DbAdmin.Infrastructure` 方言与 Resolver
4. `DbAdmin.Service` 数据源管理
5. `DbAdmin.Service` 元数据浏览
6. `DbAdmin.Service` 表数据处理
7. 日志与异常处理骨架
8. `DbAdmin.Service` 导入导出
9. `DbAdmin.Service` 结构设计
10. `DbAdmin.Service` SQL 控制台
11. 审计与历史

### 15.2 代码风格要求

- 保持与现有仓库代码风格一致。
- 优先复用现有模式，不引入额外层级。
- 小步提交，按模块逐步落地。
- 所有查询与更新优先使用参数化能力。
- 所有新实体统一放在 `DbAdmin.Entity`，并继承 `EntityBase`。
- 所有抽象接口统一放在 `DbAdmin.Interface`。
- 所有接口与业务编排统一放在 `DbAdmin.Service`。
- 所有管理表主键统一采用雪花 ID。

### 15.3 验收重点

实现完成后，至少验证以下内容：

1. 新模块是否未引入权限字段扩展
2. 是否未创建异步任务体系相关代码和表
3. 是否未新增 WebApi/Controller 层
4. 表数据查询是否完全参数化
5. SQL 控制台是否为分页返回，默认前 20 条
6. 异常是否记录详细 Error 日志
7. 是否记录必要的 Info 日志
8. 新实体是否位于 `DbAdmin.Entity` 且继承 `EntityBase`
9. 抽象接口是否收敛到 `DbAdmin.Interface`
10. 接口与业务是否统一放在 `DbAdmin.Service`
11. 管理表主键是否统一使用雪花 ID
12. DDL 是否未继续采用 `Drop + Create`

---

## 16. 最终结论

DbAdmin 本次建设目标不是重写现有数据库管理能力，而是在复用现有数据源与执行基础设施的前提下，建立一个可持续扩展、边界清晰、接口统一、安全可控的数据库管理模块。

最终落地时必须严格遵守以下四个核心约束：

1. 数据源能力直接复用，不做权限扩展。
2. 接口与业务统一放在 `DbAdmin.Service`，不新增 Controller 层。
3. `DbAdmin.Entity`、`DbAdmin.Interface`、`DbAdmin.Infrastructure`、`DbAdmin.Service` 的职责边界必须清晰。
4. 导入导出先做基础同步能力，不做异步任务体系。
5. SQL、日志、异常、实体基类、雪花 ID 等规范必须一次定准。

以上内容即为最终开发依据。