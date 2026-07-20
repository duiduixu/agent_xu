# DbAdmin Phase Second Version

> 生成日期：2026-07-20  
> 适用场景：交接给新的 Claude / AI 会话，继续当前 DbAdmin 后端开发  
> 当前唯一实现依据：`03-应用服务/IotPlatform/DbAdmin_最终开发文档.md`

---

## 1. 当前任务背景

本阶段是在 `DbAdminPhaseReport1.md` 基础上继续推进的第二轮实装。

用户已明确要求：
- 后续实现必须以 `DbAdmin_最终开发文档.md` 为唯一依据
- 不回退到分析阶段
- 继续直接实现，不进入 plan mode
- 当前继续推进的主线包括：
  1. `TableDataAppService` 继续收口到方言层，并完善关键列选择策略
  2. `SqlConsoleAppService` 补查询 / 非查询分支与更严格的总数统计
  3. `MetadataProvider.GetIndexesAsync` 继续增强主键 / 唯一索引识别准确性
  4. `ImportExportAppService` 实现基础 Excel / CSV 导入导出

---

## 2. 当前阶段已完成的新增成果

## 2.1 MetadataProvider 已从占位版提升到基础可用版

文件：
- `D:\code\iotplatformv5\02-应用模块\06-DbAdmin\DbAdmin.Infrastructure\Metadata\MetadataProvider.cs`

已完成：
- `GetSchemasAsync`
  - SQL Server：查询 `sys.schemas`
  - PostgreSQL / openGauss：查询 `information_schema.schemata`
  - 其他库：保底返回当前 database
- `GetIndexesAsync`
  - MySQL：查询 `information_schema.statistics`
  - SQL Server：查询 `sys.indexes / sys.index_columns / sys.columns`
  - PostgreSQL / openGauss：当前仍通过 `pg_indexes.indexdef` 解析
  - 保底回退到主键信息
- `GetTableDdlAsync`
  - MySQL：`show create table`
  - SQL Server：`object_definition(object_id(...))`
  - PostgreSQL / openGauss：拼装式 DDL 预览
  - 仍保留 fallback DDL

当前判断：
- 已经不是“占位版”
- 但 PostgreSQL / openGauss 仍不是 `pg_dump` 级别完整还原

---

## 2.2 SchemaDesignAppService 与方言层已增强跨方言 DDL 细节

涉及文件：
- `DbAdmin.Interface/Interface/IDbDialect.cs`
- `DbAdmin.Infrastructure/Dialects/MySqlDialect.cs`
- `DbAdmin.Infrastructure/Dialects/SqlServerDialect.cs`
- `DbAdmin.Infrastructure/Dialects/PostgreSqlDialect.cs`
- `DbAdmin.Service/SchemaDesignAppService.cs`

已完成：
- 在 `IDbDialect` 增加：
  - `BuildUpdateTableCommentSql`
- `SchemaDesignAppService.UpdateMeta` 不再写死通用 SQL，而是统一走方言层
- 路由参数 `table / column / index` 会回填到 DTO，避免 body 与路由值不一致
- MySQL：
  - 表注释
  - 字段注释
  - 索引删除
- PostgreSQL / openGauss：
  - `comment on table`
  - `comment on column`
  - schema 级索引删除
- SQL Server：
  - 表 / 字段注释已由 `sp_addextendedproperty` 升级为：
    - 存在则 `sp_updateextendedproperty`
    - 不存在则 `sp_addextendedproperty`

当前判断：
- 结构设计服务已不再停留在通用 SQL 级别
- 跨方言 DDL 收口已进入可用状态

---

## 2.3 TableDataAppService 已继续收口到方言层

文件：
- `D:\code\iotplatformv5\02-应用模块\06-DbAdmin\DbAdmin.Service\TableDataAppService.cs`

已完成：
- 查询链路：
  - 表名 / 字段名走 `IDbDialect.WrapIdentifier`
  - 筛选条件字段走 `WrapIdentifier`
  - 排序字段走 `WrapIdentifier`
  - 分页查询改为：
    - 先本地 `count`
    - 再方言 `BuildPagedQuery`
- 新增链路：
  - `Add` 不再使用 `Insertable(rows).AS(table)` 作为主路径
  - 已改为：
    - 白名单校验
    - 方言 `BuildInsertSql`
    - 参数化 `@i0 / @i1 ...`
- 更新链路：
  - 不再依赖 `WhereColumns(primaryField)` 单字段模式
  - 改为：
    - 方言 `BuildUpdateSql`
    - 参数化 `@u* + @k*`
- 删除链路：
  - 不再手写 where 拼接
  - 改为：
    - 方言 `BuildDeleteSql`
    - 参数化 `@p*`
- 审计日志：
  - Query / Add / Update / Delete 都已接入 `IAuditLogService`

---

## 2.4 TableDataAppService 的关键列策略已做第一轮修正

已完成：
- 新增 `GetKeyColumnsAsync(...)`
- 不再把所有主键 / 唯一索引列 `SelectMany` 摊平
- 当前改为：
  - 优先选主键索引
  - 没有主键时再选唯一索引
  - 同类候选中优先列数更少者，再按索引名排序
  - 返回的是“一个索引”的列集合，不再是所有索引列并集

涉及辅助调整：
- `DbIndexInfoDto` 新增：
  - `ColumnCount`

当前判断：
- 已经比“自动把多个唯一索引列混在一起”更合理
- 但对于“无主键且存在多个唯一索引”的表，仍然属于自动猜测，还不够严格

---

## 2.5 SqlConsoleAppService 已补基础查询 / 非查询分支

文件：
- `D:\code\iotplatformv5\02-应用模块\06-DbAdmin\DbAdmin.Service\SqlConsoleAppService.cs`

已完成：
- SQL 预分析仍保留
- 当前增加了 SQL 类型识别：
  - `select / with / show / desc / explain` 视为查询语句
  - 其他视为非查询语句
- 查询语句执行：
  - 使用 `select count(1) from (<sql>) dbadmin_count` 做总数统计
  - 使用方言 `BuildPagedQuery` 执行分页查询
- 非查询语句执行：
  - 直接 `ExecuteCommandAsync(input.Sql)`
  - 回填 `AffectedRows`
  - `Rows` 返回空集合
- SQL 历史落库与 `DbOperationLog` 审计已兼容查询 / 非查询两类分支

当前判断：
- 已经从“只按查询分页执行”提升为“查询 / 非查询分支可区分”
- 但复杂 SQL 场景下的 count 包装可靠性仍需继续增强

---

## 2.6 ImportExportAppService 已从空壳推进到基础可用版

文件：
- `D:\code\iotplatformv5\02-应用模块\06-DbAdmin\DbAdmin.Service\ImportExportAppService.cs`
- `D:\code\iotplatformv5\02-应用模块\06-DbAdmin\DbAdmin.Entity\Dto\ImportExport\ImportTableRequest.cs`
- `D:\code\iotplatformv5\02-应用模块\06-DbAdmin\DbAdmin.Entity\Dto\ImportExport\ExportTableRequest.cs`
- `D:\code\iotplatformv5\02-应用模块\06-DbAdmin\DbAdmin.Service\GlobalUsings.cs`

已完成：
- 导入 DTO：
  - 增加 `IFormFile File`
  - 增加 `BatchSize`
- 导出 DTO：
  - 增加 `FileType`
  - 增加 `MaxRows`
- 导入：
  - 支持 CSV
  - 支持 Excel
  - 使用 `[FromForm]`
  - 分批 `Insertable(...).AS(table)` 写入
  - 会按元数据字段白名单过滤列
- 导出：
  - 支持 CSV
  - 支持 Excel (`xlsx`)
  - 有单次最大导出行数限制
  - CSV 按批分页查询并写入输出流
  - Excel 当前为分批查询后聚合到内存再输出

当前判断：
- 已经具备基础导入导出能力
- 其中：
  - CSV 导出更接近“流式输出”要求
  - Excel 导出目前仍不是严格低内存流式写出
- 按“本次只需基本功能即可”要求，已初步可用，但还不够交付级

---

## 3. 当前已完成的接口 / 方言新增点

## 3.1 IDbDialect 当前已新增的方法

当前除原有方法外，已新增：
- `BuildUpdateTableCommentSql(CreateTableRequest request)`
- `BuildDeleteSql(string tableName, IReadOnlyList<string> keyColumns)`
- `BuildUpdateSql(string tableName, IReadOnlyList<string> updateColumns, IReadOnlyList<string> keyColumns)`
- `BuildInsertSql(string tableName, IReadOnlyList<string> insertColumns)`

这几个方法在以下方言中均已实现：
- `MySqlDialect`
- `SqlServerDialect`
- `PostgreSqlDialect`
- `OpenGaussDialect` 继承 PostgreSQL 方言能力

---

## 4. 当前已知未完成点 / 需下一会话继续完善

## 4.1 SqlConsoleAppService

当前仍需继续补：
- 复杂 SQL 场景下更严格的 count 包装策略
  - 当前是 `select count(1) from (<sql>) dbadmin_count`
  - 对部分复杂 SQL / order by / 特殊数据库语法场景仍需继续验证
- SQL 类型识别目前是关键字启发式
  - 仍可能需要进一步细化
- 查询结果分页虽然已切到方言层，但还需要做更多跨方言验证

建议下一步：
- 重点验证：
  - `with ... select ...`
  - `show`
  - `desc`
  - `explain`
  - 普通 `update/delete/insert`
- 必要时将 count 包装收口到方言层扩展能力

---

## 4.2 MetadataProvider.GetIndexesAsync

当前最重要的待办之一：
- 要继续增强为“主键 / 唯一索引识别完整且列顺序绝对准确”

当前问题：
- MySQL / SQL Server 的准确性已经比第一版高很多
- PostgreSQL / openGauss 目前仍依赖 `pg_indexes.indexdef` 字符串拆列
  - 这不是最稳的方式
  - 对表达式索引 / 特殊索引定义 / 包裹格式差异不够稳

建议下一步：
- PostgreSQL / openGauss 改为直接查系统目录：
  - `pg_class`
  - `pg_index`
  - `pg_attribute`
  - `pg_namespace`
- 必须按真实 ordinal 返回列顺序
- 必须正确识别：
  - 主键索引
  - 唯一索引
  - 普通索引

---

## 4.3 TableDataAppService 关键列策略

当前仍需继续收紧：
- 对“表无主键但存在多个唯一索引”的场景，目前仍会自动选一个最优唯一索引
- 这虽然优于旧实现，但依然属于自动猜测

建议下一步：
- 若无主键且唯一索引数量 > 1：
  - 不再自动推断
  - 直接抛出明确错误
  - 要求前端或调用方指定键列，或限制不开放在线更新 / 删除
- 若只有一个唯一索引：
  - 才允许自动采用该索引列作为关键列

这会比当前实现更符合“业务正确性优先”的要求。

---

## 4.4 ImportExportAppService

当前仍需继续补：
- Excel 导出目前仍是聚合到内存后输出
  - 还不是真正的低内存流式导出
- 导入目前仍是基础版：
  - 仅做字段白名单过滤
  - 没有更细的数据类型转换 / 错误定位 / 行级异常说明
- 还未接入审计日志
- 还未接入 `ImportModeEnum` 的真实差异逻辑：
  - `RebuildTableAndImport`
  - `TruncateAndImport`
  - `CreateTableOnly`
  - `InsertDataOnly`
  目前实际上只接近 `InsertDataOnly`

建议下一步：
- 先把 `InsertDataOnly` 做稳
- 再视需要补：
  - `TruncateAndImport`
- 本次仍不要引入异步任务体系
- 优先保证：
  - CSV 导出稳定
  - Excel 导入稳定
  - 分批写入可用
  - 单次导出规模可控

---

## 5. 当前已确认可构建状态

本阶段结束时，以下项目构建通过：
- `DbAdmin.Infrastructure`
- `DbAdmin.Service`

构建命令示例：
```bash
dotnet build "D:/code/iotplatformv5/02-应用模块/06-DbAdmin/DbAdmin.Infrastructure/DbAdmin.Infrastructure.csproj"
dotnet build "D:/code/iotplatformv5/02-应用模块/06-DbAdmin/DbAdmin.Service/DbAdmin.Service.csproj"
```

注意：
- 仍会带出仓库已有警告：
  - `IotPlatform.Collection.Contracts` 的 `NU1603`
- 这不是本阶段 DbAdmin 新增代码引入的错误

---

## 6. 建议新会话接手后的优先顺序

### 第一优先级
1. 继续完善 `MetadataProvider.GetIndexesAsync`
   - 尤其 PostgreSQL / openGauss 改为系统目录级查询
   - 保证主键 / 唯一索引识别完整且列顺序绝对准确

2. 收紧 `TableDataAppService` 的关键列策略
   - 对“无主键但有多个唯一索引”的情况不再自动猜测
   - 必要时直接拒绝在线更新 / 删除

3. 继续增强 `SqlConsoleAppService`
   - 继续验证并增强复杂 SQL count 包装可靠性
   - 继续做跨方言分页行为验证

### 第二优先级
4. 继续完善 `ImportExportAppService`
   - 让 `InsertDataOnly` 更稳
   - 补 `TruncateAndImport` 的基础实现
   - 完善错误信息与字段映射容错
   - 视可行性继续降低 Excel 导出内存占用

### 第三优先级
5. 给导入导出接入统一审计日志
6. 继续增强 DDL / 元数据真实能力
7. 继续做跨方言验证与边界修复

---

## 7. 新会话接手时应直接阅读的文件

### 核心约束文件
- `D:\code\iotplatformv5\03-应用服务\IotPlatform\DbAdmin_最终开发文档.md`

### 第一阶段总结
- `D:\code\iotplatformv5\03-应用服务\IotPlatform\DbAdminPhaseReport1.md`

### 第二阶段总结
- `D:\code\iotplatformv5\03-应用服务\IotPlatform\DbAdminPhase_SecondVersion.md`

### 当前关键代码文件
- `D:\code\iotplatformv5\02-应用模块\06-DbAdmin\DbAdmin.Service\TableDataAppService.cs`
- `D:\code\iotplatformv5\02-应用模块\06-DbAdmin\DbAdmin.Service\SqlConsoleAppService.cs`
- `D:\code\iotplatformv5\02-应用模块\06-DbAdmin\DbAdmin.Service\SchemaDesignAppService.cs`
- `D:\code\iotplatformv5\02-应用模块\06-DbAdmin\DbAdmin.Service\ImportExportAppService.cs`
- `D:\code\iotplatformv5\02-应用模块\06-DbAdmin\DbAdmin.Infrastructure\Metadata\MetadataProvider.cs`
- `D:\code\iotplatformv5\02-应用模块\06-DbAdmin\DbAdmin.Infrastructure\Dialects\MySqlDialect.cs`
- `D:\code\iotplatformv5\02-应用模块\06-DbAdmin\DbAdmin.Infrastructure\Dialects\SqlServerDialect.cs`
- `D:\code\iotplatformv5\02-应用模块\06-DbAdmin\DbAdmin.Infrastructure\Dialects\PostgreSqlDialect.cs`
- `D:\code\iotplatformv5\02-应用模块\06-DbAdmin\DbAdmin.Interface\Interface\IDbDialect.cs`

---

## 8. 给新会话的直接指令

新会话接手后，请遵守以下规则：

1. **唯一实现依据**  
   只以 `DbAdmin_最终开发文档.md` 为实现依据，不重新发散需求。

2. **不要回退到分析阶段**  
   继续在当前代码基础上推进，不需要重新做规划。

3. **优先补业务正确性**  
   当前优先补：
   - `GetIndexesAsync` 的准确性
   - `TableDataAppService` 的关键列约束
   - `SqlConsoleAppService` 的复杂 SQL 分页 / count
   - `ImportExportAppService` 的基础可用性和稳定性

4. **不要推翻现有骨架**  
   当前四项目结构和现有方言 / Service 骨架已经落位，应继续增强，不要重建另一套分层。

5. **构建验证要持续执行**  
   每轮修改后至少构建：
   - `DbAdmin.Infrastructure`
   - `DbAdmin.Service`

---

## 9. 当前阶段一句话总结

当前已经在第一阶段骨架之上，进一步把 `TableData`、`SchemaDesign`、`SqlConsole`、`ImportExport` 四条主链路推进到了“基础可用且可继续增强”的状态；下一阶段重点是把索引元数据准确性、关键列约束、复杂 SQL 分页统计、以及导入导出稳定性，从“能运行”继续提升到“可交付”。
