# DbAdmin Metadata 方言化重构方案

> 生成日期：2026-07-21  
> 适用场景：交接给新的 Claude CLI 会话，继续 DbAdmin 元数据层的可扩展性重构  
> 目标：移除 `MetadataProvider` 中按数据库类型 `switch`/`if` 分支的元数据 SQL 判断，把数据库特定实现正式下沉到 `IDbDialect`
---

## 1. 背景与问题

当前 DbAdmin 元数据读取链路大致如下：

- `MetadataAppService`
- `IMetadataProvider`
- `MetadataProvider`
- `ISqlSugarClient`

其中 [MetadataProvider.cs](D:/code/iotplatformv5/02-应用模块/06-DbAdmin/DbAdmin.Infrastructure/Metadata/MetadataProvider.cs) 内部存在多处基于 `db.CurrentConnectionConfig.DbType` 的数据库类型分支，例如：

- `GetSchemasAsync`
- `GetIndexesAsync`
- `GetTableDdlAsync`
- 以及若干配套私有解析/拼装逻辑

这类实现短期可用，但有明显扩展问题：

1. 新增数据库类型时，需要回改 `MetadataProvider`
2. 一个类里同时承担“元数据编排”和“方言 SQL 知识”，职责混杂
3. 很容易继续膨胀成中央 `switch` 汇总点
4. 与项目中已有的 `IDbDialect` 方言体系不一致

更关键的是：

- [IDbDialect.cs](D:/code/iotplatformv5/02-应用模块/06-DbAdmin/DbAdmin.Interface/Interface/IDbDialect.cs) 已经定义了：
  - `GetDatabasesAsync`
  - `GetSchemasAsync`
  - `GetTablesAsync`
  - `GetViewsAsync`
  - `GetColumnsAsync`
  - `GetIndexesAsync`
  - `GetTableDdlAsync`
- 但当前这些元数据方法的真正实现并不在各方言类里，而是堆在 `MetadataProvider`

这说明设计方向已经有了，只是实现尚未完全落地。

---

## 2. 结论

**推荐方案：保留 `IMetadataProvider` 作为统一入口与协调层，把数据库特定的元数据实现正式下沉到 `IDbDialect`。**

即：

- `MetadataAppService` 仍然只依赖 `IMetadataProvider`
- `MetadataProvider` 不再维护数据库类型分支 SQL
- `MetadataProvider` 改为：
  - 调用当前数据源对应的 `IDbDialect`
  - 在必要时做 fallback、结果整理、兼容补全
- `SqlServerDialect` / `MySqlDialect` / `PostgreSqlDialect` / `OpenGaussDialect` 分别实现各自元数据 SQL

**不建议再新增一层 `IMetadataDialect`。**

原因：

- 当前 `IDbDialect` 已经承担数据库差异抽象职责
- 再拆新接口只会增加注册、注入、工厂复杂度
- 目前项目规模下没有明显收益

---

## 3. 推荐的职责边界

### 3.1 `IDbDialect` 负责什么

放入数据库特定实现：

- `GetSchemasAsync`
- `GetIndexesAsync`
- `GetTableDdlAsync`
- 若未来某些数据库 `GetColumnsAsync` 也需要特殊系统表读取，也可逐步下沉

本质上，**凡是依赖系统表、information_schema、pg_catalog、sys.* 的数据库特定 SQL，都应由方言类负责。**

### 3.2 `MetadataProvider` 负责什么

保留统一协调职责：

- 统一对外提供元数据入口
- 处理方言调用与 fallback
- 处理 `DbMaintenance` 能覆盖的通用逻辑
- 做 DTO 整理、默认值补齐、兼容性修正

本质上，`MetadataProvider` 更像一个 facade / orchestrator，而不是具体方言实现者。

---

## 4. 本次重构建议范围

建议本轮只先迁移最值得迁移的三类方法，不要一次性把所有元数据方法全搬空。

### 第一批迁移目标

1. `GetSchemasAsync`
2. `GetIndexesAsync`
3. `GetTableDdlAsync`

原因：

- 这三类最依赖数据库系统表
- 当前 `switch` 分支最明显
- 迁移收益最大
- 风险可控

### 暂时保留在 `MetadataProvider` 的方法

1. `GetDatabasesAsync`
2. `GetTablesAsync`
3. `GetViewsAsync`
4. `GetColumnsAsync`

原因：

- 当前主要依赖 `DbMaintenance`
- 通用性较强
- 没必要为了结构纯度强行下沉

后续如果某数据库在列元数据上需要更强可还原度，再考虑拆 `GetColumnsAsync`。

---

## 5. 调用链重构建议

### 当前调用链

- `MetadataAppService` -> `IMetadataProvider`
- `MetadataProvider` 内部自己按 `db.CurrentConnectionConfig.DbType` 判断 SQL

### 目标调用链

- `MetadataAppService` -> `IMetadataProvider`
- `MetadataProvider` -> `IDialectFactory` / `IDbDialect`
- `IDbDialect` 负责数据库特定元数据 SQL

### 推荐改法

给 `MetadataProvider` 注入 `IDialectFactory`，并增加一个内部方法，例如：

```csharp
private IDbDialect ResolveDialect(ISqlSugarClient db)
```

由 `db.CurrentConnectionConfig.DbType` 映射到 `DbEngineType`，然后调用 `IDialectFactory.GetDialect(...)`。

这样：

- `MetadataProvider` 不直接依赖具体方言类
- 仍保持和当前 `DialectFactory` 体系一致
- 新增数据库类型时，只新增方言实现即可

---

## 6. 文件级改造清单

### 6.1 需要重点改造的文件

1. [MetadataProvider.cs](D:/code/iotplatformv5/02-应用模块/06-DbAdmin/DbAdmin.Infrastructure/Metadata/MetadataProvider.cs)
2. [IDbDialect.cs](D:/code/iotplatformv5/02-应用模块/06-DbAdmin/DbAdmin.Interface/Interface/IDbDialect.cs)
3. [SqlServerDialect.cs](D:/code/iotplatformv5/02-应用模块/06-DbAdmin/DbAdmin.Infrastructure/Dialects/SqlServerDialect.cs)
4. [MySqlDialect.cs](D:/code/iotplatformv5/02-应用模块/06-DbAdmin/DbAdmin.Infrastructure/Dialects/MySqlDialect.cs)
5. [PostgreSqlDialect.cs](D:/code/iotplatformv5/02-应用模块/06-DbAdmin/DbAdmin.Infrastructure/Dialects/PostgreSqlDialect.cs)
6. [OpenGaussDialect.cs](D:/code/iotplatformv5/02-应用模块/06-DbAdmin/DbAdmin.Infrastructure/Dialects/OpenGaussDialect.cs)

### 6.2 需要参考的文件

1. [MetadataAppService.cs](D:/code/iotplatformv5/02-应用模块/06-DbAdmin/DbAdmin.Service/MetadataAppService.cs)
2. [DialectFactory.cs](D:/code/iotplatformv5/02-应用模块/06-DbAdmin/DbAdmin.Infrastructure/Dialects/DialectFactory.cs)
3. [DataSourceResolver.cs](D:/code/iotplatformv5/02-应用模块/06-DbAdmin/DbAdmin.Infrastructure/Connections/DataSourceResolver.cs)
4. [DbEngineType.cs](D:/code/iotplatformv5/02-应用模块/06-DbAdmin/DbAdmin.Entity/Enum/DbEngineType.cs)

---

## 7. 具体实施步骤

### 步骤 1：确认 `IDbDialect` 的元数据职责继续保留

当前接口已经包含：

- `GetSchemasAsync`
- `GetIndexesAsync`
- `GetTableDdlAsync`

本轮不新增新接口，直接使用现有接口。

如果部分方言当前还是空实现，本轮把空实现补齐。

### 步骤 2：给 `MetadataProvider` 注入 `IDialectFactory`

当前 `MetadataProvider` 无方言工厂依赖。

需要：

- 增加构造注入 `IDialectFactory`
- 增加 `ResolveDialect(ISqlSugarClient db)` 内部方法
- 参考 `DataSourceResolver.ToEngineType(...)` 做 `SqlSugar.DbType -> DbEngineType` 映射

注意：

- `DataSourceResolver` 现在是从 `DbTypeEnum` -> `DbEngineType`
- `MetadataProvider` 拿到的是 `SqlSugar.DbType`
- 这里需要单独补一个映射方法，不能直接复用 `DataSourceResolver` 私有方法

### 步骤 3：迁移 `GetSchemasAsync`

当前做法：

- `MetadataProvider.GetSchemasAsync` 内部 `switch (dbType)` 拼 SQL

目标做法：

- `MetadataProvider.GetSchemasAsync` 先 `ResolveDialect(db)`
- 调用 `dialect.GetSchemasAsync(db, database)`
- 若返回空，则保留当前 fallback：返回 `database`

方言实现建议：

- `SqlServerDialect.GetSchemasAsync`：迁移 `sys.schemas` 查询
- `PostgreSqlDialect.GetSchemasAsync`：迁移 `information_schema.schemata` 查询
- `OpenGaussDialect`：直接继承 `PostgreSqlDialect` 实现即可，除非 openGauss 后续要特化
- `MySqlDialect.GetSchemasAsync`：可先返回空数组，由 `MetadataProvider` fallback 为 `database`

### 步骤 4：迁移 `GetIndexesAsync`

当前做法：

- `MetadataProvider.GetIndexesAsync` 中按 dbType 选择 SQL
- 同一类中同时解析 MySQL / SQL Server / PostgreSQL 结果

目标做法：

- `MetadataProvider.GetIndexesAsync`：
  - `var dialect = ResolveDialect(db)`
  - `var result = await dialect.GetIndexesAsync(...)`
  - 如果结果为空，则 fallback 到 `BuildPrimaryKeyFallback(db, table)`

方言实现建议：

- `SqlServerDialect.GetIndexesAsync`：
  - 迁移 `sys.indexes/sys.index_columns/sys.columns/...` 查询
  - 可把 SQL Server 行解析逻辑一并迁入该类
- `MySqlDialect.GetIndexesAsync`：
  - 迁移 `information_schema.statistics` 查询
  - 把 `ParseMySqlIndexes` 的逻辑迁入方言内
- `PostgreSqlDialect.GetIndexesAsync`：
  - 迁移 `pg_class/pg_index/pg_attribute/...` 查询
  - 把 `ParsePostgreSqlIndexes` 的逻辑迁入方言内
- `OpenGaussDialect`：
  - 如果与 PostgreSQL 兼容，直接复用父类

重构后 `MetadataProvider` 中这些方法大概率可以删除：

- `ParseMySqlIndexes`
- `ParseSqlServerIndexes`
- `ParsePostgreSqlIndexes`

### 步骤 5：迁移 `GetTableDdlAsync`

这是本轮最重的部分。

当前做法：

- `MetadataProvider.GetTableDdlAsync` 里按 dbType 选择不同 SQL
- PostgreSQL / openGauss 的 DDL 拼装逻辑也堆在 `MetadataProvider`

目标做法：

- `MetadataProvider.GetTableDdlAsync`：
  - `var dialect = ResolveDialect(db)`
  - `var ddl = await dialect.GetTableDdlAsync(db, database, schema, table)`
  - 若空字符串，则 fallback 到 `BuildFallbackDdl(db, table)`

方言实现建议：

- `MySqlDialect.GetTableDdlAsync`
  - 迁移 `show create table`
- `SqlServerDialect.GetTableDdlAsync`
  - 迁移 `object_definition(object_id(...))`
- `PostgreSqlDialect.GetTableDdlAsync`
  - 迁移当前 PostgreSQL / openGauss 的完整 DDL 构造逻辑
  - 包括：
    - constraints 查询
    - sequence 查询
    - comments
    - indexes
    - `create table`
    - `alter sequence owned by`
    - `foreign key / check / unique`

注意：

- 当前 `BuildPostgreSqlDdl(...)` 以及 `AppendPostgreSqlSequences / AppendPostgreSqlConstraints / AppendPostgreSqlIndexes / AppendPostgreSqlComments` 这些方法，建议整体迁入 `PostgreSqlDialect`
- 不要只迁 SQL，把 PostgreSQL DDL 的拼装辅助方法也一起带走，否则 `MetadataProvider` 仍会残留大量方言知识

### 步骤 6：`MetadataProvider` 收缩成协调层

迁移完成后，`MetadataProvider` 应保留：

- `GetDatabasesAsync`
- `GetTablesAsync`
- `GetViewsAsync`
- `GetColumnsAsync`
- `BuildPrimaryKeyFallback`
- `BuildFallbackDdl`
- 通用辅助如 `ReadFirstColumn`
- `ResolveDialect`
- 必要的 schema normalization fallback

应尽量移除：

- 与某具体数据库强绑定的 SQL 字符串
- 与某具体数据库强绑定的解析函数
- 与 PostgreSQL DDL 强绑定的拼装函数

---

## 8. 推荐的类职责最终形态

### `MetadataAppService`

保持不变，继续只做 API 入口。

### `IMetadataProvider`

接口可以保持不变。

### `MetadataProvider`

变成：

- facade / coordinator
- fallback owner
- 不再是多数据库系统表 SQL 的中心

### `IDbDialect`

成为真正的数据库元数据差异承担者。

### 各方言类

- `SqlServerDialect`
- `MySqlDialect`
- `PostgreSqlDialect`
- `OpenGaussDialect`

都应具备可独立演进的元数据实现。

---

## 9. 风险点与注意事项

### 9.1 不要把所有方法一次性全迁完

建议只迁：

- `GetSchemasAsync`
- `GetIndexesAsync`
- `GetTableDdlAsync`

否则改动面过大，回归成本会明显上升。

### 9.2 `OpenGaussDialect` 目前继承 `PostgreSqlDialect`

这本身是合理的。

本轮要注意：

- 如果把 PostgreSQL DDL 逻辑迁入 `PostgreSqlDialect`
- `OpenGaussDialect` 就天然获得同样能力
- 若 openGauss 有细微差异，再在子类 override

### 9.3 `KingbaseEs` 当前没有独立方言实现

[DataSourceResolver.cs](D:/code/iotplatformv5/02-应用模块/06-DbAdmin/DbAdmin.Infrastructure/Connections/DataSourceResolver.cs) 会把 `DbTypeEnum.KingbaseEs` 映射到 `DbEngineType.KingbaseEs`，但当前仓库里没有看到对应独立方言类。

这意味着：

- 如果未来要支持金仓元数据，可能需要新增 `KingbaseDialect`
- 如果其语义和 PostgreSQL 接近，也可以考虑让它继承 `PostgreSqlDialect`

本轮至少要确认：

- `DialectFactory` 在遇到 `DbEngineType.KingbaseEs` 时是否已经有实现
- 如果没有，这次重构不要无意中把该路径弄坏

### 9.4 fallback 行为必须保留

尤其是：

- `GetSchemasAsync` 无结果时返回 `database`
- `GetIndexesAsync` 无结果时退回 `BuildPrimaryKeyFallback`
- `GetTableDdlAsync` 空结果时退回 `BuildFallbackDdl`

这三处 fallback 是线上兼容的关键，不能因为方言化而删掉。

---

## 10. 建议的开发顺序

按这个顺序做，风险最低：

1. 给 `MetadataProvider` 注入 `IDialectFactory`
2. 实现 `ResolveDialect(ISqlSugarClient db)`
3. 先迁 `GetSchemasAsync`
4. 再迁 `GetIndexesAsync`
5. 最后迁 `GetTableDdlAsync`
6. 清理 `MetadataProvider` 中已无用的 SQL/解析函数
7. `dotnet build` 验证

---

## 11. 验证要求

本轮至少验证：

1. `dotnet build D:/code/iotplatformv5/03-应用服务/IotPlatform/IotPlatform.csproj`
2. Metadata API 不改签名
3. 以下数据库路径不被破坏：
   - SQL Server
   - MySQL
   - PostgreSQL
   - openGauss
4. fallback 行为仍然存在

如果时间允许，建议手工检查以下输出：

- `schemas`
- `indexes`
- `ddl`

尤其要对比迁移前后 DTO 结构是否一致。

---

## 12. 对新 Claude CLI 会话的直接指令

新会话接手后请直接执行：

1. 不进入 Plan Mode
2. 不要求用户确认
3. 以本文件作为唯一重构依据开始实现
4. 优先重构 `MetadataProvider`，把元数据方言分支下沉到 `IDbDialect`
5. 第一批只迁移：
   - `GetSchemasAsync`
   - `GetIndexesAsync`
   - `GetTableDdlAsync`
6. 保留 `MetadataProvider` 的 fallback 行为
7. 完成后执行一次项目构建验证
8. 不要顺手大改 `MetadataAppService` API 结构

---

## 13. 一句话目标

**把 `MetadataProvider` 从“数据库类型判断中心”重构为“元数据协调层”，让 `IDbDialect` 成为真正承接元数据方言差异的实现入口。**
