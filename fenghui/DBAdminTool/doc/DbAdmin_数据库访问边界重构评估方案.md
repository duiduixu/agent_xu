# DbAdmin 数据库访问边界重构评估方案

> 文档版本：v1.0  
> 生成日期：2026-07-21  
> 适用范围：IotPlatform 中 DbAdmin 模块数据库访问边界治理与阶段性重构决策  
> 文档用途：用于评估 DbAdmin 是否需要从 SqlSugar 深绑定演进为更稳定的统一数据库访问边界，并给出第一阶段接口草案

---

## 1. 背景

当前 DbAdmin 模块已经完成主体功能落地，但在继续开发过程中暴露出一个架构问题：模块与 SqlSugar 耦合较深，接口层、基础设施层、服务层都不同程度直接依赖 `ISqlSugarClient`、`DbMaintenance`、`Ado`、`SugarParameter`、`UseTranAsync` 等 SqlSugar 细节。

这会带来几个现实问题：

1. 后续如果希望切换到 Dapper、原生 ADO.NET、国产数据库驱动，改动面会非常大。
2. Service 层直接承担数据库执行职责，不利于统一日志、超时、异常翻译、审计与测试替身。
3. 虽然当前已经存在 `IDataSourceResolver`、`IMetadataProvider`、`IDbDialect` 等抽象，但这些抽象本身仍然直接暴露 SqlSugar 类型，边界并不真正中立。

另一方面，现有开发文档 `DbAdmin_最终开发文档.md` 第 2.2 节明确要求优先复用：

- `ISqlSugarRepository<T>`
- `SqlSugar`
- `DataBaseManager.ChangeDataBase()`

这说明当前版本的原始目标并不是建设“ORM / 驱动可替换平台”，而是先基于现有底座把 DbAdmin 能力稳定交付。

因此，这个问题不能用“完全不动”或“全面重构”两个极端方案处理，需要做边界型判断。

---

## 2. 当前实现现状

### 2.1 既有开发约束

现有实施策略默认以 SqlSugar 为当前底座，而不是中立数据库访问层：

- `DbAdmin_最终开发文档.md` 第 2.2 节明确要求优先复用 `ISqlSugarRepository<T>`
- `DbAdmin_最终开发文档.md` 第 2.2 节明确要求优先复用 `SqlSugar`
- `DbAdmin_最终开发文档.md` 第 2.2 节明确要求优先复用 `DataBaseManager.ChangeDataBase()`

这意味着当前阶段不适合直接推翻 SqlSugar 底座，另起一套完全独立的数据库访问平台。

### 2.2 当前抽象与实际耦合情况

目前已经有一些抽象层，但它们并没有真正隔离掉 SqlSugar：

- `IDataSourceResolver` 当前直接返回 `ISqlSugarClient`
- `IMetadataProvider` 所有方法直接接收 `ISqlSugarClient`
- `IDbDialect` 除了 SQL 构造，还直接接收 `ISqlSugarClient`
- `DbConnectionContext` 直接持有 `ISqlSugarClient`

这说明当前不是“中立接口 + SqlSugar 实现”，而是“带接口壳的 SqlSugar 直连模型”。

### 2.3 高耦合点分布

#### 1. 连接管理耦合

关键位置：

- `DbAdmin.Interface/Interface/IDataSourceResolver.cs`
- `DbAdmin.Infrastructure/Connections/DataSourceResolver.cs`
- `DbAdmin.Infrastructure/Connections/DbConnectionContext.cs`

问题：

- `sourceId -> client` 的解析结果直接暴露为 `ISqlSugarClient`
- 上层服务默认拿到的是具体 ORM 客户端，而不是 DbAdmin 自己的连接上下文语义

#### 2. 统一执行耦合

以下 Service 层直接调用 SqlSugar 执行 API：

- `DbAdmin.Service/TableDataAppService.cs`
- `DbAdmin.Service/ImportExportAppService.cs`
- `DbAdmin.Service/SchemaDesignAppService.cs`
- `DbAdmin.Service/SqlConsoleAppService.cs`

典型直连包括：

- `client.Ado.GetDataTableAsync`
- `client.Ado.ExecuteCommandAsync`
- `client.Ado.GetInt`
- `client.Ado.UseTranAsync`
- `client.Insertable(...)`
- `SugarParameter`

问题：

- Service 层不仅做业务编排，还承担数据库访问职责
- 后续替换底层执行器时，改动会直接扩散到业务服务

#### 3. 元数据读取耦合

关键位置：

- `DbAdmin.Infrastructure/Metadata/MetadataProvider.cs`
- `DbAdmin.Infrastructure/Dialects/MySqlDialect.cs`
- `DbAdmin.Infrastructure/Dialects/SqlServerDialect.cs`
- `DbAdmin.Infrastructure/Dialects/PostgreSqlDialect.cs`

问题：

- `MetadataProvider` 同时依赖 `DbMaintenance` 和方言补充 SQL
- `IDbDialect` 既做规则定义，也参与元数据实际查询
- 元数据读取职责与方言规则职责混杂

#### 4. 事务与批量写入耦合

最明显在：

- `DbAdmin.Service/ImportExportAppService.cs`

问题：

- 事务控制直接绑定 `UseTranAsync`
- 批量插入和 identity 处理依赖 SqlSugar 语义
- 这类逻辑后续最容易成为解耦瓶颈

---

## 3. 是否有必要重构

### 3.1 备选项

#### 方案 A：不重构

优点：

- 当前投入最小
- 不影响既有开发节奏

缺点：

- SqlSugar 耦合面会继续扩散
- 后续任何驱动替换、执行策略调整、事务行为统一都更难
- Interface 层与 Service 层的边界继续恶化

结论：

- 不推荐

#### 方案 B：小范围边界型重构

目标：

- 不替换 SqlSugar 底座
- 只把 SqlSugar 从 Interface / Service 的业务边界后撤到 Infrastructure 内部
- 优先统一连接上下文和数据库执行边界

优点：

- 改动范围可控
- 与既有开发文档不冲突
- 能显著降低继续扩散的耦合
- 为未来局部替换驱动预留路径

缺点：

- 需要设计合适的执行器与上下文接口
- 第一阶段抽象粒度需要控制得比较稳

结论：

- 推荐

#### 方案 C：全面 ORM / 驱动可替换平台重构

目标：

- 统一抽象 Dapper / ADO.NET / SqlSugar / 国产驱动
- 将 DbAdmin 建成通用数据库访问平台

优点：

- 理论上长期最灵活

缺点：

- 明显超出当前 DbAdmin 的阶段目标
- 与现有开发文档策略冲突
- 改动面覆盖连接、执行、元数据、事务、批量导入、分页、DDL、方言
- 回归成本极高，收益短期不成比例

结论：

- 不推荐

### 3.2 结论

**建议采用方案 B：小范围边界型重构。**

### 3.3 结论理由

1. 当前确实存在重构必要性，但问题核心不是“用了 SqlSugar”，而是“SqlSugar 细节泄漏到了错误的层次”。
2. 现阶段 DbAdmin 的复杂度主要来自方言、元数据、DDL、分页和安全规则，而不是 ORM 映射本身。
3. 全面重构会把本轮工作从模块边界治理升级成平台建设，性价比明显不合理。
4. 小范围边界重构既能解决现有痛点，又不推翻当前实施底座。

---

## 4. 本次建议的目标边界

### 4.1 保留内容

以下内容建议保留：

1. 保留 `SqlSugar` 作为当前 Infrastructure 内部实现底座
2. 保留 `DataBaseManager.ChangeDataBase()` 作为当前过渡期连接切换能力
3. 保留 `IDbDialect` / `IDialectFactory` 的方言模式
4. 保留现有 API 路由与 DTO 结构
5. 保留 DbAdmin 现有目录分层与模块边界

### 4.2 建议新增抽象

#### 1. `IDbConnectionContextFactory` 或调整后的 `IDataSourceResolver`

职责：

- 根据 `sourceId + database` 解析连接上下文
- 提供引擎类型、数据库名、数据源信息
- 不再向上层暴露 `ISqlSugarClient`

建议结果模型：

- `DbConnectionContext`
  - `Source`
  - `EngineType`
  - `Database`
  - 不公开具体 ORM 客户端

#### 2. `IDbCommandExecutor`

职责：

- 统一收口通用数据库执行行为

建议能力：

- `ExecuteScalarAsync<T>`
- `QueryTableAsync`
- `ExecuteNonQueryAsync`
- `ExecuteInTransactionAsync`
- `BulkInsertAsync` 或等价批量插入能力

目标：

- Service 层不再直接使用 `.Ado`
- Service 层不再直接控制底层事务 API

#### 3. 自定义参数模型

建议增加 DbAdmin 自己的参数描述对象，而不是在上层直接使用 `SugarParameter`。

目标：

- 业务层只处理语义参数
- Infrastructure 负责翻译为 SqlSugar 参数对象

#### 4. 元数据读取接口收口

可保留 `IMetadataProvider` 名称，也可以演进为 `IDbMetadataReader`。

原则：

- 不再直接暴露 `ISqlSugarClient`
- 改为消费 DbAdmin 自有连接上下文或更高层语义参数

### 4.3 保留但收缩的现有接口

#### `IDbDialect`

建议保留，但收缩职责，仅保留：

- 标识符包装
- 分页 SQL 生成
- DDL / DML SQL 构造
- 类型映射
- SQL 安全分析补充

不建议继续保留：

- 直接依赖 `ISqlSugarClient` 的执行型方法
- 方言层自己承担通用查询执行职责

---

## 5. 推荐目标架构

### 5.1 分层职责

#### Service

职责：

- 业务编排
- 参数组织
- 日志、审计调用
- 对外接口暴露

限制：

- 不直接引用 `ISqlSugarClient`
- 不直接调用 `.Ado`
- 不直接创建 `SugarParameter`
- 不直接使用 `UseTranAsync` / `Insertable`

#### Interface

职责：

- 只暴露 DbAdmin 自己的抽象接口
- 不暴露 SqlSugar 类型

#### Infrastructure

职责：

- 继续使用 SqlSugar 实现数据库访问
- 实现执行器、连接上下文工厂、元数据读取器
- 封装 `DataBaseManager.ChangeDataBase()`、`DbMaintenance`、SqlSugar 参数和事务细节

#### Dialect

职责：

- 数据库方言规则定义
- SQL 文本生成与规则映射
- 不承担通用执行器角色

### 5.2 推荐调用链

```text
Service
  -> IDataSourceResolver / IDbConnectionContextFactory
  -> IDbCommandExecutor
  -> IDbMetadataReader
  -> IDbDialect

Infrastructure
  -> SqlSugarDbConnectionContextFactory
  -> SqlSugarDbCommandExecutor
  -> SqlSugarMetadataReader
  -> MySqlDialect / SqlServerDialect / PostgreSqlDialect
  -> DataBaseManager.ChangeDataBase()
```

### 5.3 与 SqlSugar 的关系定位

重构后的目标不是“移除 SqlSugar”，而是：

- SqlSugar 继续作为当前实现底座
- SqlSugar 不再成为 Service / Interface 的事实合同
- SqlSugar 仅作为 Infrastructure 内部实现细节存在

---

## 6. 分阶段实施路线

### 6.1 第一阶段：执行器与连接上下文收口

这是最小可行重构，也是最推荐优先做的一步。

#### 目标

- 让 Service 层停止直接依赖 SqlSugar 客户端和执行 API

#### 范围

重点覆盖：

- `DbAdmin.Service/TableDataAppService.cs`
- `DbAdmin.Service/ImportExportAppService.cs`
- `DbAdmin.Service/SchemaDesignAppService.cs`
- `DbAdmin.Service/SqlConsoleAppService.cs`

#### 动作

1. 引入统一连接上下文或上下文工厂
2. 引入 `IDbCommandExecutor`
3. 把 `SugarParameter` 的构造转移到 Infrastructure
4. Service 只调用 DbAdmin 自有接口，不直接拿 client

#### 收益

- 快速降低新增功能继续加深耦合的风险
- 改动集中，可回归范围明确
- 不改变既有底层技术栈

### 6.2 第二阶段：元数据接口去 SqlSugar 泄漏

#### 目标

- `IMetadataProvider` 不再直接接收 `ISqlSugarClient`

#### 动作

1. 让元数据接口改为接收 DbAdmin 自有连接上下文
2. `MetadataProvider` 内部通过执行器 + 方言协作
3. 将 `DbMaintenance` 依赖留在 Infrastructure 内部

#### 收益

- Interface 层进一步中立化
- 元数据能力可以单独演进

### 6.3 第三阶段：方言职责收缩

#### 目标

- 让 `IDbDialect` 回归规则定义层

#### 动作

1. 逐步移除 `IDbDialect` 中直接依赖 `ISqlSugarClient` 的方法
2. 将元数据执行职责归并到元数据读取器
3. 保留 SQL 规则、分页、DDL/DML 构造、类型映射、安全分析能力

#### 收益

- 方言层职责清晰
- 元数据执行与方言规则分离
- 未来新增数据库引擎时结构更稳定

---

## 7. 不建议做的事情

1. 不建议现在建设“全面 ORM / 多驱动 / 多执行器可插拔平台”
2. 不建议把 `IDbDialect` 继续膨胀成大一统数据库访问接口
3. 不建议一次性同时重写连接解析、执行器、元数据、方言和所有 Service
4. 不建议为了抽象纯度强行立刻移除 `SqlSugar`、`DbMaintenance`、`DataBaseManager.ChangeDataBase()`
5. 不建议回头先清洗旧兼容模块，当前应优先治理 DbAdmin 新模块边界

---

## 8. 收益与风险分析

### 8.1 收益

1. 控制 SqlSugar 耦合继续向 Service 层扩散
2. 提升 Service 层可测试性和可维护性
3. 统一数据库执行、异常翻译、审计、超时和日志边界
4. 为未来局部替换 Dapper / ADO.NET / 国产驱动预留可落地路径
5. 不推翻当前开发文档和技术底座，改造成本相对可控

### 8.2 风险

1. 执行器设计过薄，容易沦为机械透传层
2. 执行器设计过厚，容易把业务语义塞进基础设施
3. `ImportExportAppService` 的事务与批量写入最容易暴露抽象不足
4. `SqlConsoleAppService` 的自由 SQL 执行场景边界最复杂
5. 如果没有静态扫描和回归约束，容易出现“表面抽象、实则换个地方继续直连 SqlSugar”的伪重构

---

## 9. 验证与落地策略

### 9.1 架构验收标准

第一阶段完成后，至少满足：

1. `DbAdmin.Service` 层不再直接引用 `ISqlSugarClient`
2. `DbAdmin.Service` 层不再直接调用：
   - `.Ado`
   - `SugarParameter`
   - `UseTranAsync`
   - `Insertable`
3. `DbAdmin.Interface` 层不再直接暴露 `SqlSugar` 类型
4. 现有 API 路由与 DTO 基本保持不变

### 9.2 功能回归范围

后续实施时重点回归以下场景：

1. 表数据查询与分页
2. 导入导出与事务回滚
3. 结构设计 DDL 执行
4. SQL 控制台查询与非查询执行
5. metadata 查询链路

### 9.3 静态扫描建议

可以用作阶段性验收：

- `DbAdmin.Service` 目录中不再出现 `ISqlSugarClient`
- `DbAdmin.Service` 目录中不再出现 `.Ado.`
- `DbAdmin.Service` 目录中不再出现 `SugarParameter`
- `DbAdmin.Interface` 层中不再出现 `SqlSugar` 类型泄漏
- `IDbDialect` 中仅保留规则型职责

### 9.4 落地方式

建议采用以下顺序：

1. 先新增新接口，不立刻删除旧接口
2. 在 Infrastructure 中补齐 SqlSugar 版本实现
3. 按 Service 粒度逐个迁移调用
4. 迁移完成后再收缩旧接口
5. 最后再做 `IMetadataProvider` / `IDbDialect` 的职责整理

---

## 10. 第一阶段接口草案

以下草案只覆盖第一阶段最小可行边界，不追求一次性抽象完整平台。

### 10.1 连接上下文

```csharp
namespace DbAdmin.Interface.Interface;

public interface IDbConnectionContextFactory
{
    Task<DbConnectionContext> CreateAsync(long sourceId, string? database = null);
}

public class DbConnectionContext
{
    public DbLink Source { get; set; } = default!;

    public DbEngineType EngineType { get; set; }

    public string? Database { get; set; }
}
```

设计意图：

- 对上层暴露 DbAdmin 自己的连接语义
- 不再公开 `ISqlSugarClient`
- 具体 SqlSugar client 仅在 Infrastructure 内部使用

### 10.2 统一执行器

```csharp
namespace DbAdmin.Interface.Interface;

public interface IDbCommandExecutor
{
    Task<int> ExecuteNonQueryAsync(DbConnectionContext context, string sql, IReadOnlyList<DbParameterSpec>? parameters = null);

    Task<object?> ExecuteScalarAsync(DbConnectionContext context, string sql, IReadOnlyList<DbParameterSpec>? parameters = null);

    Task<DataTable> QueryTableAsync(DbConnectionContext context, string sql, IReadOnlyList<DbParameterSpec>? parameters = null);

    Task<int> QueryIntAsync(DbConnectionContext context, string sql, IReadOnlyList<DbParameterSpec>? parameters = null);

    Task<T> ExecuteInTransactionAsync<T>(DbConnectionContext context, Func<Task<T>> action);
}
```

设计意图：

- 统一 `ExecuteCommandAsync`、`GetDataTableAsync`、`GetInt`、`UseTranAsync` 入口
- 让 Service 层只描述“要执行什么”，不关心 SqlSugar 的 `.Ado` 细节
- 第一阶段先覆盖最常用执行动作，不提前抽象所有数据库能力

### 10.3 参数描述对象

```csharp
namespace DbAdmin.Interface.Interface;

public sealed class DbParameterSpec
{
    public string Name { get; set; } = string.Empty;

    public object? Value { get; set; }
}
```

设计意图：

- 替代 Service 层直接创建 `SugarParameter`
- Infrastructure 内部再翻译为 SqlSugar 参数对象

### 10.4 元数据读取接口过渡草案

第一阶段不强制立刻替换 `IMetadataProvider`，但建议为第二阶段预留如下形态：

```csharp
namespace DbAdmin.Interface.Interface;

public interface IDbMetadataReader
{
    Task<IReadOnlyList<string>> GetDatabasesAsync(DbConnectionContext context);

    Task<IReadOnlyList<string>> GetSchemasAsync(DbConnectionContext context, string database);

    Task<IReadOnlyList<DbTableInfoDto>> GetTablesAsync(DbConnectionContext context, string database, string? schema);

    Task<IReadOnlyList<DbColumnInfoDto>> GetColumnsAsync(DbConnectionContext context, string database, string? schema, string table);

    Task<IReadOnlyList<DbIndexInfoDto>> GetIndexesAsync(DbConnectionContext context, string database, string? schema, string table);

    Task<string> GetTableDdlAsync(DbConnectionContext context, string database, string? schema, string table);
}
```

设计意图：

- 为第二阶段“元数据接口去 SqlSugar 泄漏”预留方向
- 仍允许底层先继续复用 `MetadataProvider + SqlSugar` 的实现

### 10.5 第一阶段改造目标文件

建议优先改造以下文件：

- `DbAdmin.Service/TableDataAppService.cs`
- `DbAdmin.Service/ImportExportAppService.cs`
- `DbAdmin.Service/SchemaDesignAppService.cs`
- `DbAdmin.Service/SqlConsoleAppService.cs`
- `DbAdmin.Infrastructure/Connections/DataSourceResolver.cs`
- `DbAdmin.Infrastructure/Connections/DbConnectionContext.cs`

改造目标：

- Service 层不再直接拿 `ISqlSugarClient`
- Service 层不再直接操作 `.Ado`
- 执行细节下沉到 Infrastructure

---

## 11. 最终建议

**最终建议：采纳“小范围边界型重构”方案。**

### 推荐本轮只做两件事

1. 收口连接上下文暴露
2. 收口数据库统一执行边界

### 暂不做

1. 全面 ORM 可替换平台化
2. 全量方言重写
3. 旧模块回溯清洗
4. 一次性把所有 SqlSugar 依赖全部移除

### 结论总结

DbAdmin 当前确实和 SqlSugar 绑定较深，但问题重点不是“用了 SqlSugar”，而是“SqlSugar 越过了应该停留的边界”。因此，有必要重构，但应是**边界后撤式的小范围重构**，而不是一次性建设通用数据库访问平台。

这样做既能控制架构债继续扩大，也不会推翻当前版本已经明确的技术底座和交付节奏。
