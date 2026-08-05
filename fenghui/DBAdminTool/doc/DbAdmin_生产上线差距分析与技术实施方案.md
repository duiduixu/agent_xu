# DbAdmin 生产上线差距分析与技术实施方案

> 评审日期：2026-07-27  
> 评审范围：`02-应用模块/16-DbAdmin` 全部 C# 源码、项目文件、`DbAdmin_最终开发文档.md`，以及宿主项目引用和 MVC Application Part 产物。  
> 结论：模块骨架和主要业务接口已实现，项目可独立编译；但当前不具备直接生产上线条件。必须先完成 P0 安全、宿主集成、数据作用域、审计可靠性和资源控制改造，并通过本文第 7 节的上线门禁。

## 1. 评审结论与范围对照

最终开发文档要求的四层结构、动态 API、数据源、元数据、表数据 CRUD、基础导入导出、DDL、SQL 控制台、审计/历史实体均已存在。未发现新增数据源权限字段、权限表、异步任务体系或独立 Controller，符合既定边界。

但下列文档约束尚未形成可生产依赖的闭环：

| 能力 | 当前状态 | 结论 |
|---|---|---|
| 宿主装配与路由发布 | `IotPlatform` 未引用 `DbAdmin.Service`，编译产物的 Application Part 也没有该程序集 | P0 缺失，API 很可能不被发现 |
| SQL 高危拦截、跨库与多语句控制 | 安全分析只做少量 `Contains`；执行端只检查 `IsSafe` | P0 不满足 |
| Schema/表对象作用域 | 多数 SQL 仅包裹 `table`，请求中的 `Schema` 未进入对象名；元数据也主要走无 schema 的 SqlSugar API | P0 不满足多 schema 数据库隔离与正确性 |
| 表数据在线编辑约束 | 无主键表仍可按调用方任意字段更新/删除；空值无法写入 | P0 不满足开发文档强制约束 |
| DDL 安全与可审计性 | DDL 缺少元数据白名单、输入校验、审计、事务/锁风险控制 | P0 缺失 |
| 导入导出资源边界 | 导出和 Excel/CSV 导入仍在内存中聚集完整数据；上传类型/大小/行数限制不足 | P0 缺失 |
| 管理表、保留策略与审计可靠性 | 实体存在，但未见迁移/建表交付；审计写入失败会反向使业务失败 | P0/P1 缺失 |
| 数据源凭据保护 | 密码和完整连接串以明文实体/缓存对象流转，缺少加密、脱敏和缓存失效 | P0 缺失 |
| 多引擎承诺 | MySQL/SQL Server/PostgreSQL 有方言；OpenGauss 仅继承；`KingbaseEs` 无方言实例且映射会抛错 | P0 缺失 |
| 自动化质量保证 | 模块没有单元、集成、契约或安全回归测试项目 | P0 缺失 |

## 2. P0：上线前必须完成的功能与改造清单

### P0-01 宿主项目装配、解决方案正确性与启动自检

**现状证据**

- `DbAdmin.Service` 仅引用其内部项目，未被 `03-应用服务/IotPlatform/IotPlatform.csproj` 引用。
- `IotPlatform.MvcApplicationPartsAssemblyInfo.cs` 中没有 `DbAdmin.Service`，当前动态 API 不会作为宿主 Application Part 发布。
- `IotPlatform.sln` 的四个 DbAdmin 项目均使用相同 GUID，名称均为 `IotPlatform.PolicyMessage`，解决方案配置不可靠。

**实施方案**

1. 向宿主 `IotPlatform.csproj` 添加对 `DbAdmin.Service.csproj` 的 `ProjectReference`；确认该引用能够传递 `Entity`、`Interface`、`Infrastructure`。
2. 为四个 DbAdmin 项目重新生成唯一 Project GUID 和正确显示名称，更新 `.sln` 的 `ProjectConfigurationPlatforms`；或用 `dotnet sln add` 重建这些条目。
3. 使用宿主发布配置执行 `dotnet publish`，断言生成的 `IotPlatform.MvcApplicationPartsAssemblyInfo.cs` 包含 `DbAdmin.Service`，并在启动后从 `IApiDescriptionGroupCollectionProvider` 断言所有 23 个设计路由至少被发现一次。
4. 增加启动健康检查 `dbadmin-registration`：验证关键接口的 DI 解析和管理库连接可用；不得在健康检查中输出连接串或密码。

**验收标准**：发布产物含 `DbAdmin.Service.dll`；Swagger/API Explorer 中可见 `/api/db-sources` 与表、SQL 路由；启动 DI 验证无 `IDbDialect`、`IDataSourceResolver` 等未注册异常。

### P0-02 SQL 控制台改为解析驱动的安全策略

**现状证据**

- [`SqlSafetyAnalyzer.cs`](DbAdmin.Infrastructure/Security/SqlSafetyAnalyzer.cs) 只匹配 9 个文本片段，`IsMultiStatement`、`IsCrossDatabase` 仅作为结果返回，没有被 `Execute` 拦截。
- [`SqlConsoleAppService.cs`](DbAdmin.Service/SqlConsoleAppService.cs) 只以 `!safety.IsSafe` 拦截；因此 `DELETE FROM t`、`UPDATE t SET ...`、`ALTER TABLE`、`CREATE USER`、`SELECT ...; DELETE ...` 等可执行或可能绕过策略。
- 注释移除使用正则，不能正确处理字符串字面量、嵌套注释、方言特性；方言自身 `AnalyzeSqlSafety` 与实际注入的分析器也未建立调用关系。

**实施方案**

1. 新增 `SqlPolicyOptions`：按环境和角色定义允许的语句类别（默认 `SELECT`/`EXPLAIN`）、是否允许 DML/DDL、最大执行秒数、最大扫描/返回行数、是否允许系统库及跨库。生产默认禁止 DDL、账户/权限语句、事务控制、过程调用、文件/外部命令和多语句。
2. 用支持目标方言的 SQL AST 解析器替代字符串匹配；若无法统一解析，按引擎实现 `ISqlStatementParser`，并遵循“解析失败即拒绝”。解析结果应包含语句数、语句类型、引用对象、库/schema、是否含注释与动态执行。
3. `Preview` 和 `Execute` 统一调用 `ISqlExecutionPolicy.Authorize(...)`。将多语句、跨库/跨 schema、非允许类别、危险对象、解析失败显式置为 `IsSafe=false` 和有意义的 `ErrorMessage`；服务端必须再校验，不信任预览结果。
4. 仅对单条、只读、可分页 AST 生成 count/paging SQL。含 `LIMIT/OFFSET`、`SHOW`、`EXPLAIN` 等不应通过“跳过分页后直接全量回包”绕过行数限制；应改为安全上限或采用游标协议。
5. 连接层设置数据库原生超时和只读会话：MySQL `MAX_EXECUTION_TIME`/连接命令超时，PostgreSQL/openGauss `statement_timeout` 与 `default_transaction_read_only`，SQL Server `CommandTimeout`/Resource Governor（如可用）。使用低权限、按引擎分离的 DbAdmin 数据库账号作为最终防线。

**验收标准**：对注释、字符串、CTE、分号、混合大小写、跨库、DML/DDL/权限语句的安全用例均有自动化回归；无白名单授权时只允许单条只读 SQL；任意查询不能突破服务端行数与超时限制。

### P0-03 正确落实 Database/Schema/Table 作用域与对象白名单

**现状证据**

- [`TableDataAppService.cs`](DbAdmin.Service/TableDataAppService.cs)、[`ImportExportAppService.cs`](DbAdmin.Service/ImportExportAppService.cs)、[`SchemaDesignAppService.cs`](DbAdmin.Service/SchemaDesignAppService.cs) 生成 SQL 时只使用路由 `table`，请求 `Schema` 未写入对象名。
- [`MetadataProvider.cs`](DbAdmin.Infrastructure/Metadata/MetadataProvider.cs) 的表、字段查询使用 `GetTableInfoList`、`GetColumnInfosByTableName`，schema 参数实际未参与；相同表名时可能命中错误对象。
- 路由表名与 body 中 `Table`/`TableName`、`SourceId` 均有重复字段，但未校验一致性。

**实施方案**

1. 定义不可变 `DbObjectName(Database, Schema, Name)` 与 `QualifiedObjectName`。每个方言实现 `QuoteObjectName`，按引擎处理 `database.schema.table`、默认 schema 和大小写规则；禁止把未校验字符串直接传给 `WrapIdentifier`。
2. 将 `IDbDialect` 的 DDL/DML 构建接口改为接收 `DbObjectName`，元数据接口强制带 database/schema。为 MySQL、SQL Server、PostgreSQL/openGauss、Kingbase 分别实现系统目录查询，避免无 schema 的 SqlSugar 模糊 API。
3. 引入 `IMetadataGuard`：在每次读写和 DDL 前以 `(sourceId,database,schema,object)` 查询并缓存短期元数据，确认表、字段、索引存在且对象类型正确；缓存键必须包含引擎、数据库和 schema，DDL 成功后精准失效。
4. 服务入口校验 path 参数优先：body 中若带 `SourceId`、`Table`、`TableName`，必须为空或与路由一致，否则返回 400；校验 database/schema 可访问且不是系统库/schema。

**验收标准**：同一数据库两个 schema 下同名表的读、写、导入、导出、DDL 均只影响指定 schema；不存在对象、视图写入、body/path 不一致均被拒绝。

### P0-04 表数据读写的完整性、并发与输入边界

**现状证据**

- [`TableDataAppService.cs`](DbAdmin.Service/TableDataAppService.cs) 在无主键/唯一索引时以调用方 `KeyValues.Keys` 继续更新和删除，违反“无主键不开放在线编辑”；`Update` 未要求 `NewValues` 非空。
- 新增、更新时 `null` 值被过滤，无法将字段更新为 `NULL`；没有依据列类型做转换、长度、精度、不可写（identity/generated）校验。
- `PageIndex`、`PageSize`、筛选数、`IN` 元素数、列数没有上限；筛选 `Logic` 未限制为 `AND/OR`，可构造非法 SQL；`Total` 使用 `int` 可能溢出。
- 更新/删除没有乐观并发令牌，可能覆盖他人修改；更新/删除仅记录影响行数，不将 0 行视作并发冲突。

**实施方案**

1. 将键解析收敛为 `ResolveEditableKey`：只接受主键；仅在明确选择且完整提供唯一索引全部列时才允许唯一键编辑。没有主键/唯一索引时返回 `409`/业务错误并禁用 UI 编辑。
2. DTO 使用显式 patch 模型（例如 `Dictionary<string, JsonElement>`），区分“字段未提交”和“提交 null”；通过 `DbColumnInfoDto` 转为目标 CLR/DbType，校验 nullable、长度、precision/scale、日期范围、枚举/二进制格式，并拒绝 identity、计算列、生成列与主键变更。
3. 增加 `RowVersion`/原始值哈希作为可选并发条件。客户端提交读取时返回的并发令牌；`UPDATE ... WHERE key AND version/hash` 影响行数为 0 时返回 `409`，不记录为成功。
4. `TableQueryOptions` 设置 `PageSize` 默认 20、最大 200，`PageIndex>=1`，筛选最多 20，排序最多 5，`IN` 最多 1,000，查询列最多 100；`Logic` 只允许 `AND`/`OR`，operator 使用枚举。计数及页数改为 `long`。
5. 对 `LIKE` 转义 `%`、`_` 与 escape 字符，规定大小写/空值语义；多租户业务表若有租户字段，必须在独立 `ITableAccessScope` 中强制追加当前租户谓词，不能信任前端筛选。

**验收标准**：无键表无法编辑；显式 null 可写入；非法类型/空键/空更新/超限请求返回 4xx；并发更新不会静默覆盖；所有值仍通过参数传递。

### P0-05 DDL 防误操作、校验、审计与变更协议

**现状证据**

- [`SchemaDesignAppService.cs`](DbAdmin.Service/SchemaDesignAppService.cs) 直接执行方言 SQL，未验证表/列/索引是否存在、名称是否合法或路由/body 一致，也未写 `DbOperationLog`。
- DDL 默认值已改为结构化定义，并由方言按字段类型生成安全的常量或受控时间函数 SQL；注释和对象名仍需保持现有标识符校验与字符串转义规范。
- 创建表未显式执行 `CreateTableRequest.Indexes`；多语句 DDL 在部分方言需要原子性、锁等待和失败补偿设计。

**实施方案**

1. 增加 `ISchemaChangeValidator` 和 `ISchemaChangePlanner`。先读取元数据生成 precondition（对象存在性、列/索引冲突、依赖关系、数据量、锁风险），再生成最小 SQL plan；执行前二次验证 schema 版本/元数据指纹。
2. 对所有标识符采用严格正则和元数据白名单；数据类型使用每个方言的允许类型映射，长度/精度设上限；默认值只接受受控常量与白名单函数，注释通过参数或统一转义函数处理。
3. 所有 DDL 先返回或持久化 `ChangePlan`（SQL 摘要、风险级别、影响对象、不可逆标记）；高风险操作（删列/删索引/改非空/大表索引）要求二次确认 token 和操作理由。该 token 必须绑定用户、源、对象、SQL 摘要和短 TTL，不能由前端自行伪造。
4. DDL 执行增加数据库侧 `lock_timeout`/命令超时、单源串行锁、失败日志和 DDL 审计。不同引擎不支持事务 DDL 时，明确记录部分成功状态并提供元数据刷新。
5. 补齐创建表中的索引、主键、注释步骤，按引擎以事务或可恢复计划执行；成功后失效相关元数据缓存。

**验收标准**：所有 DDL 有审计记录、操作人、耗时、计划摘要和结果；非法类型/名称/默认表达式被拒绝；高风险操作没有确认 token 不能执行；同表并发 DDL 被串行化。

### P0-06 导入导出的流式处理、文件安全与一致性

**现状证据**

- [`ImportExportAppService.cs`](DbAdmin.Service/ImportExportAppService.cs) 虽分页读取目标表，但 CSV/Excel 导出写入 `MemoryStream`；Excel/CSV 导入均先读成 `List<Dictionary<...>>`，大文件将造成内存压力。
- `.xls` 被送入 OpenXML 读取器，支持声明与实际格式不一致；未知扩展名默认按 CSV 处理。
- 没有明确的上传长度、压缩比、工作表、列数、行数、单元格长度、编码和 MIME 校验；CSV 解析器不能处理带引号的跨行字段。
- `TruncateAndImport` 直接执行 `truncate ... restart identity cascade`（PostgreSQL/openGauss），可能意外清空关联表；导出审计记录的影响行数为请求上限 `maxRows`，不是真实导出行数。

**实施方案**

1. 引入 `DbAdminFileOptions`：最大文件字节、最大行数、最大列数、最大单元格字节、最大错误明细、允许扩展名/MIME、导出上限和下载速率。Web 服务器层同步设置 multipart 上限，拒绝未知后缀和内容签名不匹配文件。
2. 将 `ReadCsvRowsAsync`/`ReadExcelRowsAsync` 改成 `IAsyncEnumerable<ImportRow>`；逐行校验、按 batch 写入，保留有限问题明细和计数，禁止完整文件进入内存。使用成熟 RFC 4180 CSV 解析器，并对 XLSX 做 zip bomb/工作表限制；若不支持二进制 XLS，明确拒绝它。
3. CSV 使用 `FileCallbackResult`/`Response.BodyWriter` 直接输出；XLSX 使用支持真实流式写出的库或临时受控文件。返回前设置 `Content-Disposition`、`X-Content-Type-Options: nosniff`，文件名做净化。导出用 keyset pagination（主键/唯一索引）替代大 OFFSET。
4. 默认禁用 `TruncateAndImport`，或要求独立危险确认并禁止 `CASCADE`；导入使用 staging table + 校验 + 原子交换（方言可用时）或清晰的事务边界。明确 `AllOrNothing` 与 `SkipInvalid` 的回滚、部分成功和审计语义。
5. 对 CSV/Excel 文本单元格以 `= + - @` 开头的内容进行导出转义，防止 spreadsheet formula injection。导入/导出都记录真实处理/写入/跳过行数。

**验收标准**：达到限制的文件稳定返回 4xx；10 万行以上测试不随行数线性占用进程内存；恶意 CSV、公式注入、zip bomb、错误扩展名和跨行字段都有测试；审计中的导出行数真实准确。

### P0-07 数据源凭据、缓存和网络访问控制

**现状证据**

- [`DataSourceAppService.cs`](DbAdmin.Service/DataSourceAppService.cs) 直接保存 `Password` 和 `ConnectString`；[`DataSourceResolver.cs`](DbAdmin.Infrastructure/Connections/DataSourceResolver.cs) 与工厂将完整 `DbLink` 缓存 5 分钟。
- 更新和删除未使 `dbadmin:source:{id}` 缓存失效，删除后仍可能在 TTL 内连接旧数据源；没有数据源测试的超时、诊断和安全网络边界。
- `DbTypeEnum` 的 `KingbaseEs` 映射到 `DbEngineType.KingbaseEs`，但没有对应 dialect；未知类型回退 MySQL，存在错误连接/错误 SQL 风险。

**实施方案**

1. 使用既有密钥管理能力或 KMS/Key Vault 实现 envelope encryption：数据库仅存 `PasswordCiphertext`/`ConnectionStringCiphertext` 和 key version，应用边界短暂解密；API 响应 DTO 永不返回密文/明文，日志使用敏感字段掩码器。
2. 数据源创建、更新后先做带超时的连接测试再提交；禁止内网保留地址、环回、链接本地、多播和未许可端口，或以网络策略/代理白名单落实 Egress 控制。DNS 解析结果也需校验，防止 DNS rebinding/SSRF。
3. 缓存存储最小化的连接配置或加密载荷，设置短 TTL；更新、删除、密钥轮换后立即显式 `Remove`。将连接池按 `sourceId + configVersion + database` 隔离，并限制每源最大池/并发数。
4. 将 `DbTypeEnum -> DbEngineType -> SqlSugar.DbType -> IDbDialect` 的映射改为显式完整映射。暂不真正支持的引擎在创建/更新时拒绝；补齐 Kingbase 方言后才允许启用。OpenGauss 需增加实际连接、元数据、DDL、分页兼容测试后才能声明支持。

**验收标准**：任何 GET/List、日志、异常、审计均不含密码和连接串；更新/删除数据源后下一请求不能使用旧缓存；不受控地址不能被测试连接或业务连接访问；每个可选引擎都有可运行的完整映射。

### P0-08 管理表迁移、审计可靠性与敏感数据策略

**现状证据**

- `DbOperationLog`、`DbSqlHistory` 实体已定义，但本次代码范围未见数据库迁移/初始化 SQL、索引和保留策略交付。
- 每个业务 try/catch 都 `await WriteAuditLogAsync`；审计库不可用会覆盖原业务结果，catch 内的审计失败还会掩盖原始异常。
- SQL history 保存原始 SQL，可能含个人数据、口令、token 或连接串；`SqlDigest` 只是截断而非脱敏或哈希。`ClientIp` 字段未赋值。

**实施方案**

1. 为每个系统管理库引擎提供版本化迁移（不要只依赖实体扫描）：创建两表、雪花主键确认、必要索引 `source_id + created_time`、`operator_id + created_time`、`is_success + created_time`，并纳入部署流水线的幂等迁移步骤。
2. 增加 `IAuditWriter.TryWriteAsync`：审计失败必须记录本地 Error/指标并保留原业务成功或原始异常；对监管场景改为 outbox，再由可靠消费者落库。审计事件内携带 `TraceId`、Client IP（经受信代理链解析）、请求 ID、来源名、操作理由和风险级别。
3. 原始 SQL 分级保存：默认仅保存规范化摘要、SHA-256 指纹和受控参数摘要；确有排障需要时以字段级加密存储原文，设置严格访问权限、查询审计和短期保留。异常消息同样经过脱敏器再入库。
4. 建立保留/归档/清理策略（例如在线 90 天、归档 1 年，按合规调整），采用按月分区或分批删除，并监控表容量、写入失败率和延迟。

**验收标准**：空库部署可创建管理表并重复执行无异常；审计故障不改变主业务语义；安全扫描和人工抽查不发现凭据；审计查询有索引计划且保留任务可验证。

### P0-09 统一异常、日志、限流和可观测性

**现状证据**

- 元数据、数据源和 DDL 服务没有一致的入口/成功/失败日志；`DbAdminExceptionHelper` 未被使用，未形成统一错误码与脱敏边界。
- 查询、SQL、导入导出均缺少 `CancellationToken`、用户/数据源维度并发限制和速率限制；日志未统一带 TraceId。

**实施方案**

1. 为 DbAdmin 定义错误码（参数、对象不存在、策略拒绝、并发冲突、目标数据库超时、目标数据库连接失败、审计降级），在全局异常处理器映射为统一安全响应；完整细节只写结构化 Error 日志。
2. 新增 `DbAdminOperationContext`，在中间件/过滤器中注入 trace/request/user/client IP/source/database/schema/table；所有服务使用同一日志模板，参数只记录摘要与数量，不记录值。
3. 所有异步接口向下传递 `CancellationToken`，命令执行器设置 `CommandTimeout`，HTTP 断开时中止可取消操作。引入分布式限流：用户、源、IP 三个维度分别限制 SQL、DDL、导入、导出；每源使用 semaphore 防止连接池耗尽。
4. 暴露指标和 tracing：请求数/失败数/拒绝数、连接耗时、执行耗时、返回行数、导入行数、审计写入失败、池等待、缓存命中。告警覆盖高错误率、慢查询、连续安全拒绝、审计失败和内存压力。

**验收标准**：每条操作可按 TraceId 串联；取消请求可终止目标命令或在超时后清理；压力测试中单用户无法耗尽连接池；告警指标可在监控系统查看。

## 3. P1：生产增强项

| 编号 | 功能缺口 | 技术实现与验收 |
|---|---|---|
| P1-01 | SQL 执行计划 | 增加只读 `EXPLAIN`/`EXPLAIN ANALYZE` 策略；`ANALYZE` 设更严格权限和超时。输出统一树形 DTO、成本/行数/索引建议，原始计划脱敏。 |
| P1-02 | 游标分页 | 表浏览和导出引入基于主键/唯一索引的 cursor（含排序列和值、HMAC 签名），避免深页 OFFSET；保留 offset 协议兼容小页。 |
| P1-03 | 元数据缓存 | 使用带 `sourceId/database/schema/object/schemaVersion` 的缓存键，表级 TTL、单飞加载和 DDL 后精准失效；提供手工刷新接口并审计。 |
| P1-04 | 数据库能力矩阵 | 建立“功能 x 引擎 x 版本”矩阵，运行时 capability negotiation。未认证能力在 UI/API 返回 `NotSupported`，不能静默降级到 MySQL 行为。 |
| P1-05 | 数据脱敏与列级展示 | 通过配置或数据库注释标记 PII/secret 列，查询/导出默认掩码；仅受控角色可申请临时明文查看，申请和访问都审计。 |
| P1-06 | 操作审批 | 文档明确当前不做细粒度权限；但生产建议先复用现有角色/菜单权限限制 DbAdmin 入口，并为 SQL DML/DDL 预留审批接口。不得改造数据源权限字段或新建数据源权限表。 |
| P1-07 | 数据源健康管理 | 后台轻量健康探测、失败熔断、连接版本/能力采集、证书到期告警；探测使用最小权限账号且不输出敏感配置。 |
| P1-08 | 备份/回滚辅助 | DDL plan 生成受影响对象 DDL 与样本统计快照，提供脚本下载和回滚建议；明确其不是自动事务回滚承诺。 |

## 4. 关键技术设计

### 4.1 建议的调用链

```text
Dynamic API
  -> DbAdminOperationFilter (认证、角色、限流、TraceId、请求上限)
  -> AppService (path/body 一致性、业务编排)
  -> ObjectScope + MetadataGuard (database/schema/table/column 白名单)
  -> SqlPolicy / SchemaPlanner (AST 或受控 DDL plan)
  -> Dialect + CommandExecutor (参数化、timeout、取消、事务)
  -> AuditWriter (try-write/outbox) + Metrics/Tracing
```

目标数据库账号、网络出口规则和数据库原生读写/DDL权限必须与上述应用层控制共同启用。应用层校验不能替代数据库权限。

### 4.2 配置建议

```json
{
  "DbAdmin": {
    "Sql": {
      "DefaultPageSize": 20,
      "MaxPageSize": 200,
      "MaxResultRows": 10000,
      "CommandTimeoutSeconds": 30,
      "AllowDml": false,
      "AllowDdl": false,
      "AllowCrossDatabase": false
    },
    "Files": {
      "MaxUploadBytes": 52428800,
      "MaxImportRows": 100000,
      "MaxExportRows": 50000,
      "AllowedExtensions": [".csv", ".xlsx"]
    },
    "Audit": {
      "RetentionDays": 90,
      "StoreRawSql": false
    }
  }
}
```

生产配置必须由受控配置中心或密钥系统托管；示例中的策略开关不可由请求参数覆盖。

### 4.3 测试工程与测试矩阵

新增 `DbAdmin.Tests`（单元）和 `DbAdmin.IntegrationTests`（Testcontainers 或独立隔离实例），至少覆盖：

1. SQL tokenizer/AST：注释、字符串、CTE、子查询、方言语法、多语句、跨库和拒绝策略。
2. 每种启用引擎：连接、schema 同名表、元数据、分页、CRUD、DDL、导入导出、标识符转义、identity 行为。
3. 安全：SQL 注入、SSRF/内网地址、凭据脱敏、CSV 公式注入、zip bomb、权限拒绝、审计失败降级。
4. 一致性：并发更新、DDL 串行、导入失败回滚、缓存更新/删除失效、审计与历史保留。
5. 性能：百万级表深页、最大导入/导出、并发 SQL、取消/超时、内存上限和连接池耗尽。
6. API 契约：路由、HTTP 方法、参数来源、统一错误结构、OpenAPI 快照和宿主 API 发现。

CI 应执行格式/静态分析、单元测试、安全测试和至少 MySQL/PostgreSQL/SQL Server 的集成 smoke test；发布候选额外执行实际生产版本的兼容性回归。

## 5. 分阶段实施计划

| 阶段 | 工作项 | 完成条件 |
|---|---|---|
| 0. 接入修复 | P0-01、项目引用、DI/路由启动测试 | 真实宿主中 API 可发现，解决方案条目正确 |
| 1. 安全基线 | P0-02、P0-03、P0-07、P0-09 的策略、对象作用域、凭据和限流 | 未授权 SQL/网络/对象访问均在服务端拒绝 |
| 2. 数据正确性 | P0-04、P0-05、管理表迁移 | CRUD/DDL 有完整前置校验、并发和审计 |
| 3. 文件可靠性 | P0-06、P0-08 | 大文件资源受控，导入导出与审计语义准确 |
| 4. 多引擎验证 | 补齐 capability matrix 和集成测试 | 每个对外宣称支持的引擎全链路通过 |
| 5. 灰度上线 | 只读浏览 -> 受控导出 -> 受控 DML -> 经审批 DDL | 每阶段均有指标、告警、回滚与复盘记录 |

建议初始生产灰度只开放数据源列表、元数据和只读 SQL/表浏览；DML、导入、导出、DDL 依据上述门禁逐项开放。此策略符合“不新增数据源权限字段/权限表”的限制，可由现有应用角色、菜单和网关能力控制入口。

## 6. 与最终开发文档的差异说明

1. `RebuildTableAndImport`、`CreateTableOnly` 在文档的导入模式枚举中存在，但当前实现明确拒绝。这不是错误，只要 API 文档同步标注为未支持；若要开放，必须经过 SchemaPlanner 和 staging/回滚设计，不能直接复用旧的 Drop + Create。
2. 文档要求 SQL 分页返回。当前普通 `SELECT` 会分页，但 `SHOW`/`DESC`/`EXPLAIN` 和含现有分页子句会直接全量读取，因此不满足“禁止一次性全量回包”的生产要求。
3. 文档要求 openGauss 完整兼容作为 P1；当前仅为继承类，数据源枚举/映射及真实兼容测试均未闭环，生产不得标记为可用。
4. 文档要求 Info/Error 与统一异常框架；当前部分链路有日志和 catch，但没有统一脱敏、错误码、取消、超时、TraceId 和审计降级策略，属于“部分实现”。

## 7. 上线门禁清单

在全部勾选前不得将 DbAdmin 暴露到生产用户：

- [ ] 宿主发布产物加载 `DbAdmin.Service`，所有设计 API 通过真实 HTTP smoke test。
- [ ] 解决方案项目 GUID/名称已修正，CI 可构建宿主发布配置。
- [ ] SQL AST/策略、数据库低权限账号、命令超时和多语句/跨库阻断均已上线。
- [ ] Database/schema/table 作用域在读、写、导入、导出、DDL 一致生效。
- [ ] 无键表在线编辑禁用；null、类型、并发、分页和请求上限测试通过。
- [ ] DDL 有计划、确认、元数据 precondition、锁/超时、审计和缓存失效。
- [ ] 上传/下载流式化，文件安全、公式注入和资源限制测试通过。
- [ ] 数据源密码/连接串加密、全链路脱敏、缓存失效和网络 Egress 控制已验证。
- [ ] 管理表迁移、索引、审计失败降级、原始 SQL 保留策略和清理作业已验证。
- [ ] 启用引擎的集成测试矩阵通过，未支持引擎在创建时被拒绝。
- [ ] 指标、追踪、告警、限流、回滚预案和运行手册已演练。

## 8. 本次静态核查说明

本评审已读取模块内全部 `.cs` 源文件和 `.csproj`，并核对宿主/解决方案引用。`dotnet build DbAdmin.Service/DbAdmin.Service.csproj --no-restore -v:minimal` 返回成功；构建过程中存在仓库其他项目的既有 NuGet/可空性/XML 注释警告，未发现 DbAdmin 编译错误。静态编译成功不能覆盖真实数据库方言、路由装配、权限、性能与故障恢复，因此上述集成和灰度门禁仍为上线必要条件。
