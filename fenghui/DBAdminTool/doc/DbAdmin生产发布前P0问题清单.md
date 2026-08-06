# DbAdmin 生产发布前 P0 问题清单

审查对象：`D:\code\iotplatformv5\02-应用模块\16-DbAdmin`

审查结论：当前版本不建议发布生产。以下问题均可能造成错误数据库操作、不可逆数据损坏或生产审计失效，必须在前端联调完成前修复并通过回归。按要求，未将接口权限控制列入本清单。

## P0-1 SQL 安全策略未接入执行链路

- **证据**：`DbAdmin.Infrastructure/Security/SqlSafetyAnalyzer.cs` 实现了 `ISqlSafetyAnalyzer.Analyze`，但模块内只有 `SqlConsoleStatementParser.Analyze` 被 `SqlConsoleAppService` 调用（`DbAdmin.Service/SqlConsoleAppService.cs:95,151,266`）；`ISqlSafetyAnalyzer` 没有任何注入或调用。`PreviewAsync` 仅做单语句解析后直接返回 `IsSafe=true`（约 `:90-105`），`ExecuteAsync` 和 `ExportAsync` 也未调用安全分析器。
- **触发方式**：提交 `DROP/ALTER/TRUNCATE/GRANT/EXEC/CALL/COPY/SET` 等非只读 SQL，预检仍可能返回安全，执行接口直接交给数据库；单语句限制也不能阻止存储过程、匿名块或数据库特有管理语句。
- **影响**：应用层“生产执行策略”形同虚设，误操作可直接删除库表、修改权限或执行任意管理命令；前端若依据预检结果放行，风险更高。
- **解决方案**：
  1. 将 `ISqlSafetyAnalyzer` 注入 `SqlConsoleAppService`，`PreviewAsync` 返回真实分析结果。
  2. `ExecuteAsync`、SQL 导出前再次在服务端分析，禁止仅信任前端预检；拒绝多语句、跨库引用、危险关键字、过程/匿名块及无法可靠分类的语句。
  3. 不要用正则替代方言解析；对 MySQL/PostgreSQL/SQL Server 分别维护白名单/黑名单测试，默认拒绝未知语法。
  4. 增加集成测试：每种数据库对 `DROP DATABASE`、`TRUNCATE`、`EXEC/CALL`、注释绕过、字符串内关键字、`WITH ... UPDATE` 均验证“预检拒绝且执行不落库”。

## P0-2 目标数据库路由不可信，可能把操作发到错误数据库

- **证据一（自定义连接串）**：`DbConnectionContextFactory.CloneSource` 只修改 `DataBaseName`（`DbAdmin.Infrastructure/Connections/DbConnectionContextFactory.cs:63-87`），但 `DataBaseManager.ToConnectionString` 在 `ConnectString` 非空时直接使用自定义连接串，不把请求的数据库名合并进去（`Common.Core/Manager/DataBase/DataBaseManager.cs:1520-1532`）。因此配置了自定义连接串的数据源，所有请求体中的 `Target.Database` 都可能被忽略。
- **证据二（连接键）**：`DataBaseManager.ChangeDataBase` 以 `link.Id` 作为 SqlSugar `ConfigId`（`.../DataBaseManager.cs:282-310`）。同一 `sourceId` 已建立连接后，再请求另一个数据库不会新建/切换包含新数据库名的连接配置。
- **触发方式**：同一数据源连续访问 `db_a`、`db_b`，或数据源使用带 `Database/Initial Catalog` 的自定义连接串；查询、导入、DDL、删除库均可能在首个数据库或固定数据库上执行。
- **影响**：读到错误库的数据，写入/删除错误库；这是不可接受的生产数据完整性风险。
- **解决方案**：
  1. 连接缓存键必须包含 `sourceId + database + schema（如影响连接）`，或每次按完整目标创建独立连接配置；禁止用仅 `sourceId` 的 ConfigId 复用不同数据库连接。
  2. 自定义连接串使用结构化连接字符串 builder，明确覆盖/校验 `Database`（MySQL/PostgreSQL）或 `Initial Catalog`（SQL Server）；若连接串不允许切库，则服务端拒绝与其不一致的 `Target.Database`。
  3. 创建上下文后执行 `SELECT current_database()/DB_NAME()/DATABASE()`（按方言）与目标库比对，不一致立即终止。
  4. 增加双库隔离集成测试：同一 `sourceId` 交替请求两个库，验证元数据、查询、增删改、导入和 DDL 的实际连接库均一致。

## P0-3 数据库删除存在检查与删除之间的竞态

- **证据**：`DataBaseManageService.DeleteDatabaseAsync` 先在目标库上下文查询表（`DbAdmin.Service/DataBaseManageService.cs:112-120`），随后重新创建上下文并执行 `DROP DATABASE`（`:121-125`），两步之间无锁、版本号或原子保护。
- **触发方式**：检查返回“无表”后，业务服务或另一请求在窗口内创建表；删除请求仍会继续执行 `DROP DATABASE`。
- **影响**：可能删除刚被重新使用的生产数据库，造成不可逆数据丢失。仅检查当前是否为空不能构成并发安全保障。
- **解决方案**：
  1. 第一版生产环境建议下线“删除数据库”接口；若必须保留，改为显式二次确认（数据库名+随机确认串）并默认禁用。
  2. 服务端对 `sourceId+database` 获取分布式锁，并将“确认为空”和删除置于同一数据库厂商支持的原子/锁定流程；锁失败直接拒绝。
  3. 对系统库、租户默认库和配置中的业务库建立不可删除清单，不能只依赖 `_userManager.TenantDbName`。
  4. 增加并发测试：删除检查与建表同时发生时，接口必须拒绝且数据库保持不变。

## P0-4 关键操作审计失败被吞掉，无法保证生产可追溯

- **证据**：`AuditLogService.WriteAsync` 和 `SqlHistoryService.WriteAsync` 捕获所有异常后只记录日志并正常返回（`DbAdmin.Infrastructure/Persistence/AuditLogService.cs:29-43`、`SqlHistoryService.cs:31-45`）。各写操作在数据库命令成功后调用审计，审计失败不会改变接口结果。
- **触发方式**：审计表不可用、数据库连接池耗尽、字段长度/迁移不匹配或短时网络故障时，用户仍收到“操作成功”，但没有审计记录。
- **影响**：生产数据被修改/删除而无操作者、目标和结果记录；发生事故时无法定位，且会掩盖数据库迁移或连接故障。
- **解决方案**：
  1. 对 DDL、删除、导入、SQL 执行等高风险操作采用“审计先写入（pending）→ 执行 → 更新结果”的可靠流程；审计写入失败时阻止执行（fail closed）。
  2. 使用独立审计库/可靠消息 Outbox，具备重试、唯一操作 ID、幂等和告警；禁止空 catch 后视为成功。
  3. 审计字段长度、脱敏规则和迁移脚本纳入发布检查；验证失败/成功两类记录都能查询。

## 发布前验收门槛

1. 上述四项均有代码修复、单元测试和真实 MySQL/SQL Server/PostgreSQL（含自定义连接串）集成测试证据。
2. 使用两个数据库、两个并发请求、取消请求和连接失败注入进行回归；确认没有跨库写入、部分误删或“成功但无审计”。
3. 执行 `dotnet build IotPlatform.csproj --no-restore` 通过；本次审查中该命令已通过（存在项目既有 NuGet/Nullable 警告，不属于本清单 P0）。

