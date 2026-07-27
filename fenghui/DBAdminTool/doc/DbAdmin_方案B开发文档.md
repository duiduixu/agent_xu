# DbAdmin 方案B开发文档：小范围边界型重构

> 文档版本：v1.0  
> 生成日期：2026-07-21  
> 适用范围：IotPlatform / DbAdmin 第一阶段边界治理开发  
> 文档用途：作为 AI / 开发人员直接实施 DbAdmin 小范围边界型重构的依据  
> 本文档仅覆盖方案 B，不包含全面 ORM / 驱动平台化方案内容

---

## 1. 开发目标

本次开发采用 **方案 B：小范围边界型重构**。

目标不是替换 SqlSugar，也不是建设通用数据库访问平台，而是：

1. 阻止 SqlSugar 继续穿透到 `DbAdmin.Service` 业务编排层
2. 收口数据库连接上下文暴露
3. 收口通用 SQL 执行入口
4. 保留现有 `IDbDialect` 方言体系
5. 保持现有 API 路由、DTO、功能语义基本不变

---

## 2. 本次开发范围

### 2.1 必须做

1. 定义第一阶段统一连接上下文抽象
2. 定义第一阶段统一执行器抽象
3. 让 `DbAdmin.Service` 不再直接依赖：
   - `ISqlSugarClient`
   - `.Ado`
   - `SugarParameter`
   - `UseTranAsync`
4. 优先改造以下服务：
   - `TableDataAppService`
   - `ImportExportAppService`
   - `SchemaDesignAppService`
   - `SqlConsoleAppService`

### 2.2 本次不做

1. 不替换 SqlSugar
2. 不重写 `IDbDialect` 整体体系
3. 不改造旧兼容模块
4. 不做元数据接口全面重签名
5. 不做通用 ORM / 驱动可替换平台
6. 不做 Dapper / ADO.NET 双实现

---

## 3. 当前问题摘要

当前 DbAdmin 存在以下边界问题：

### 3.1 Interface 层泄漏 SqlSugar

现状：

- `IDataSourceResolver` 返回 `ISqlSugarClient`
- `IMetadataProvider` 方法直接接收 `ISqlSugarClient`
- `IDbDialect` 也直接接收 `ISqlSugarClient`
- `DbConnectionContext` 直接持有 `ISqlSugarClient`

问题：

- 抽象层名义存在，但不是真正中立
- 未来想替换底层执行器时，接口合同本身就要改

### 3.2 Service 层直接操作底层执行 API

现状：

- `TableDataAppService` 直接用 `.Ado.GetInt`、`.Ado.GetDataTableAsync`、`.Ado.ExecuteCommandAsync`
- `SchemaDesignAppService` 直接用 `.Ado.ExecuteCommandAsync`
- `SqlConsoleAppService` 直接用 `.Ado.GetDataTableAsync`、`.Ado.ExecuteCommandAsync`
- `ImportExportAppService` 直接用 `.Ado.UseTranAsync`、`.Ado.ExecuteCommandAsync`、`Insertable(...)`

问题：

- Service 层不仅做业务编排，还承担执行器职责
- 事务、参数化、执行细节散落在多个服务中
- 难以统一日志、异常翻译、审计和超时控制

---

## 4. 设计原则

本次开发必须遵循以下原则：

1. **保留 SqlSugar 作为当前 Infrastructure 内部实现**
2. **不向 Service 层继续暴露 SqlSugar 细节**
3. **抽象只覆盖当前确实重复使用的执行能力**
4. **不把 `IDbCommandExecutor` 做成平台型胖接口**
5. **不改变现有对外 API 路由和 DTO**
6. **方言相关逻辑继续保留在 `IDbDialect` 体系中**
7. **重构顺序是先收口执行边界，再处理更深层接口问题**

---

## 5. 第一阶段目标架构

### 5.1 新增接口

#### 1. `IDbConnectionContextFactory`

用于根据 `sourceId + database` 构造 DbAdmin 自有连接上下文。

建议定义：

```csharp
public interface IDbConnectionContextFactory
{
    Task<DbConnectionContext> CreateAsync(long sourceId, string? database = null);
}
```

#### 2. `IDbCommandExecutor`

第一阶段采用 **瘦接口版本**：

```csharp
public interface IDbCommandExecutor
{
    Task<int> ExecuteNonQueryAsync(
        DbConnectionContext context,
        string sql,
        IReadOnlyList<DbParameterSpec>? parameters = null);

    Task<object?> ExecuteScalarAsync(
        DbConnectionContext context,
        string sql,
        IReadOnlyList<DbParameterSpec>? parameters = null);

    Task<DataTable> QueryDataTableAsync(
        DbConnectionContext context,
        string sql,
        IReadOnlyList<DbParameterSpec>? parameters = null);

    Task<T> ExecuteInTransactionAsync<T>(
        DbConnectionContext context,
        Func<Task<T>> action);
}
```

#### 3. `DbParameterSpec`

```csharp
public sealed class DbParameterSpec
{
    public string Name { get; set; } = string.Empty;

    public object? Value { get; set; }
}
```

### 5.2 调整 `DbConnectionContext`

第一阶段目标：`DbConnectionContext` 对外只表达 DbAdmin 自有连接语义，不再公开 `ISqlSugarClient`。

建议形态：

```csharp
public class DbConnectionContext
{
    public DbLink Source { get; set; } = default!;

    public DbEngineType EngineType { get; set; }

    public string? Database { get; set; }
}
```

注意：

- 如 Infrastructure 内部仍需持有 SqlSugar client，可采用内部扩展结构、内部映射表或私有实现对象承载
- 不允许继续把 `ISqlSugarClient` 暴露到 Interface / Service 合同中

---

## 6. 目录与代码落位

### 6.1 Interface 层

建议新增或调整：

```text
02-应用模块/06-DbAdmin/DbAdmin.Interface/Interface/
├── IDbConnectionContextFactory.cs
├── IDbCommandExecutor.cs
├── DbParameterSpec.cs
```

### 6.2 Infrastructure 层

建议新增：

```text
02-应用模块/06-DbAdmin/DbAdmin.Infrastructure/
├── Connections/
│   ├── DbConnectionContext.cs
│   ├── DbConnectionContextFactory.cs
│
├── Execution/
│   └── SqlSugarDbCommandExecutor.cs
```

### 6.3 Service 层

本轮重点改造：

```text
02-应用模块/06-DbAdmin/DbAdmin.Service/
├── TableDataAppService.cs
├── ImportExportAppService.cs
├── SchemaDesignAppService.cs
└── SqlConsoleAppService.cs
```

---

## 7. 具体改造要求

## 7.1 `IDataSourceResolver` 的处理策略

### 当前状态
- 已有 `IDataSourceResolver`
- 当前直接返回 `ISqlSugarClient`

### 第一阶段要求
- 不强制立刻删除 `IDataSourceResolver`
- 但新增的 Service 改造应优先依赖 `IDbConnectionContextFactory`
- 如果保留 `IDataSourceResolver`，它也不应继续作为 Service 获取 client 的推荐入口

### 建议
- 第一阶段可以保留 `IDataSourceResolver` 给旧代码过渡使用
- 新代码优先走新的上下文工厂

---

## 7.2 `IDbCommandExecutor` 的职责边界

### 只允许负责
1. 执行 SQL 非查询
2. 查询标量
3. 查询 `DataTable`
4. 事务包装

### 不允许负责
1. 分页业务语义
2. metadata 业务语义
3. DDL 语义封装
4. SqlConsole 特殊逻辑
5. 导入导出业务策略
6. 方言规则判断

### 解释
执行器只负责“怎么执行”，不负责“为什么执行”。

---

## 7.3 `TableDataAppService` 改造要求

### 当前问题
- 直接 `GetClientAsync(...)`
- 直接 `.Ado.GetInt(...)`
- 直接 `.Ado.GetDataTableAsync(...)`
- 直接 `.Ado.ExecuteCommandAsync(...)`
- 直接构造参数对象

### 改造目标
- 改为通过 `IDbConnectionContextFactory` 获取上下文
- 改为通过 `IDbCommandExecutor` 执行 count / query / insert / update / delete
- `BuildCountSql(...)`、`BuildQuerySql(...)`、`BuildOrderClause(...)` 等业务规则保持原处
- 参数对象改用 `DbParameterSpec`

### 注意
- 不要顺手重构排序逻辑抽象
- 不要在第一阶段碰 `IDbDialect` 分页规则体系

---

## 7.4 `SchemaDesignAppService` 改造要求

### 当前问题
- 直接拿 client
- 直接 `.Ado.ExecuteCommandAsync(sql)`

### 改造目标
- 通过 `IDbConnectionContextFactory` 获取上下文
- 继续由 `IDbDialect` 负责 DDL SQL 构造
- 执行环节统一走 `IDbCommandExecutor.ExecuteNonQueryAsync(...)`

### 注意
- DDL 生成逻辑不改
- 只替换执行边界

---

## 7.5 `SqlConsoleAppService` 改造要求

### 当前问题
- 查询和非查询都直接依赖 `.Ado`
- count / table query / command execute 都在服务层直连 SqlSugar

### 改造目标
- 查询模式下：
  - `GetRowCountByQueryAsync`
  - `GetTotalCountAsync`
  - `ExecutePagedQueryAsync`
  改为依赖执行器
- 非查询模式下：
  - `ExecuteCommandAsync` 改为 `ExecuteNonQueryAsync`
- SQL 预分析、分页 SQL 处理、`AnalyzeQueryBehavior(...)`、`BuildCountSql(...)` 保持原位

### 注意
- 第一阶段不要求把 `TryNormalizePagedQuerySource(...)` 一并改进去
- 第一阶段重点是执行器边界，不是分页策略重写

---

## 7.6 `ImportExportAppService` 改造要求

### 当前问题
- 直接 `.Ado.UseTranAsync`
- 直接 `.Ado.ExecuteCommandAsync`
- 可能存在 `Insertable(...)` 直连

### 改造目标
- 事务通过 `IDbCommandExecutor.ExecuteInTransactionAsync(...)` 收口
- 普通 SQL 执行通过 `ExecuteNonQueryAsync(...)`
- 能保留的导入业务策略保持不动
- 文件解析、行校验、identity 策略不改

### 注意
- 这是第一阶段最复杂的文件
- 不要为了让它“完全优雅”而扩大接口
- 如果个别批量写入逻辑必须暂时保留在 SqlSugar 实现内部，可以放到 Infrastructure，不要继续留在 Service

---

## 8. Infrastructure 实现要求

## 8.1 `SqlSugarDbCommandExecutor`

职责：

- 接收 `DbConnectionContext`
- 在内部解析出 SqlSugar client
- 将 `DbParameterSpec` 翻译为 `SugarParameter`
- 执行：
  - `ExecuteCommandAsync`
  - `GetScalarAsync`
  - `GetDataTableAsync`
  - `UseTranAsync`

要求：

1. 不向上层暴露 `ISqlSugarClient`
2. 参数翻译逻辑统一放在这里
3. 后续若接入 Dapper，替换点就在这里

## 8.2 `DbConnectionContextFactory`

职责：

- 根据 `sourceId` 查询数据源
- 根据 `database` 处理目标库
- 识别 `DbEngineType`
- 构造 DbAdmin 自有上下文

要求：

1. 对外返回 `DbConnectionContext`
2. 内部可以继续复用 `DataBaseManager.ChangeDataBase()`
3. Service 不得感知具体 SqlSugar client 生命周期

---

## 9. 不允许做的改动

本轮开发中，以下动作明确禁止：

1. 不得把 `IDbCommandExecutor` 扩成通用数据库平台接口
2. 不得新增 Dapper / ADO.NET 双实现
3. 不得在第一阶段改造 `IMetadataProvider` 全部签名
4. 不得重写 `IDbDialect` 全部方法
5. 不得改变现有 API 路由
6. 不得改变现有 DTO 对外结构
7. 不得顺手做“统一排序工具”“统一导入框架”“统一 SQL 控制台平台”等额外抽象

---

## 10. 验收标准

### 10.1 代码结构验收

完成后必须满足：

1. `DbAdmin.Service` 层不再直接引用 `ISqlSugarClient`
2. `DbAdmin.Service` 层不再直接出现：
   - `.Ado.`
   - `SugarParameter`
   - `UseTranAsync`
   - `Insertable`
3. `IDbCommandExecutor` 方法数控制在 4 个核心方法
4. `DbConnectionContext` 对外不再暴露 SqlSugar client

### 10.2 行为验收

必须验证：

1. 表数据分页查询仍正确
2. 表数据新增、更新、删除仍正确
3. 结构设计 DDL 执行仍正确
4. SQL 控制台查询 / 非查询仍正确
5. 导入导出仍正确
6. 审计日志与历史逻辑不受影响

### 10.3 静态扫描验收

建议通过扫描确认：

- `DbAdmin.Service` 中不再出现 `ISqlSugarClient`
- `DbAdmin.Service` 中不再出现 `.Ado.`
- `DbAdmin.Service` 中不再出现 `SugarParameter`

---

## 11. 推荐实施顺序

建议按以下顺序落地：

1. 新增 `DbParameterSpec`
2. 新增 `IDbCommandExecutor`
3. 新增 `IDbConnectionContextFactory`
4. 实现 `SqlSugarDbCommandExecutor`
5. 实现 `DbConnectionContextFactory`
6. 改造 `SchemaDesignAppService`
7. 改造 `SqlConsoleAppService`
8. 改造 `TableDataAppService`
9. 最后改造 `ImportExportAppService`
10. 做静态扫描与功能回归

说明：

- 先改 `SchemaDesignAppService`，因为路径最直、风险最低
- 再改 `SqlConsoleAppService` 和 `TableDataAppService`
- `ImportExportAppService` 最后处理，因为事务和批量导入最复杂

---

## 12. 最终结论

本次开发仅落实 **方案 B：小范围边界型重构**。

实施重点是：

1. **收口连接上下文暴露**
2. **收口通用数据库执行入口**
3. **把 SqlSugar 从 Service 层后撤到 Infrastructure 内部**

本次不追求：

- ORM 可替换平台化
- 全部底层实现中立化
- 一次性完成 DbAdmin 全架构翻新

这样做的目标不是“彻底抽象数据库”，而是先把当前最不合理的边界收回来，为后续继续演进留出空间，同时保持当前系统可控、可回归、可交付。
