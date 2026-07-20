
02-应用模块/05-DbAdmin/

web版数据库管理工具，目前支持的常用功能：
1、mysql，sqlserver，postgresql、opengauss
2、数据库创建、导入和导出
3、创建表、表数据修改、表结构修改、表索引修改、表数据导入导出、自定义筛选
4、自定义 SQL 语句执行

数据库管理工具目的：主要在项目实施阶段使用，系统上线后甲方也能进行业务数据库的基本操作。

目前平台缺少的功能：
1.数据库：数据库创建、导入和导出，数据库导入和导出的内容是什么？
2.表结构：表索引创建和修改
3.表数据：数据修改、自定义筛选、数据导入、数据导出
4.SQL查询：目前有SQL查询的导出但非异步
5.数据源管理模块、元数据浏览模块、数据浏览与编辑模块、结构设计模块、异步导入导出模块、SQL控制台模块
6.数据库方言层设计


现有系统的功能：
/table/{dataBase}/executeCommand动态执行SQL(不支持查询)
/dbLink/dynamic-query数据连接-动态执行SQL
目前iotPlatform中可以对表实体进行编辑，可以增加、修改字段名，但是，如果表已经存在数据则不能修改字段名


1.导入导出异步任务化：是否需要限制并行任务数？是否要使用Furion中的任务调度以避免过多任务同时运行导致服务宕机？
2.导入成功后文件如何清理
3.导出后，服务器上的文件如何处理
4.高危关键词黑名单：补全mysql,postgresql,sqlserver,OpenGaussDialect 高危关键词黑名单
5.SqlSugar本身支持各种数据库，是否所有数据库操作都要经过IDbDialect层


自定义 SQL 语句执行：增删改查？

现存的问题：
原本自动生成的sql语句是仅支持mysql的，pg里表名字写法不是单引号，比如 select * from `table1` limit 10;


推荐策略：新建 DbAdmin 路由与应用层。旧接口保留并冻结，不承接新需求，仅做缺陷修复和兼容。
异常任务导入导出文件管理
四种导入模式权限过大：涉及删除旧表、清空数据
前端需要同步修改，让AI整理新旧接口对应关系以便前端更快接入。




限制：
本次不允许修改现用接口的所有代码，仅新增接口





接下来需要在IotPlatform中实现数据库管理工具的开发需求，当前目录下的【关系数据库管理工具（DbAdmin）实现分析报告.md】是上级提供的需求分析及建议的实现方案，允许我根据实际情况调整方案并实现相应功能。我看了文档后有几点建议如下：
1.导入导出异步任务化：是否需要限制并行任务数？是否要使用Furion中的任务调度以避免过多任务同时运行导致服务宕机？
2.异步导入成功后文件如何清理
3.异步导出后，服务器上的文件如何处理
4.高危关键词黑名单：请补全mysql,postgresql,sqlserver,OpenGaussDialect 高危关键词黑名单
5.SqlSugar本身支持各种数据库，是否所有数据库操作都要经过IDbDialect层？
6.文档中的【2.3 与现有代码的共存策略】我有点拿不准，不知道应该如何进行，是完全重写接口还是在现有接口下改造比较好？请帮我分析下。
综上所述，请帮我分析下可行的实现方案并更新文档。


当前目录下的【关系数据库管理工具（DbAdmin）实现分析报告.md】是一份关于数据库管理工具的开发需求，经过深入分析后，决定对一些需求和功能进行修改。以下是修改要求：
1.数据源权限不需要实现，不加字段也不改功能，原样复用现有数据源功能即可，请去掉相关修改需求和建议。
3.数据库表主键使用雪花id
4.数据实体：继承EntityBase得到相关运维字段，在增加和修改时需要设置相关字段值，比如创建人、修改人、创建时间、修改时间等
5.接口路由统一用POST或GET，文档中的Delete和Put都改成POST
6.导入导出模块（ImportExport）：先不做异步任务处理，只需要有基本的导入导出数据的功能即可，请去掉相关修改需求和建议
7.代码模块分层修改：不需要有WebApi层及其Controller，接口及业务放在DbAdmin.Application层，用IDynamicApiController和ITransient暴露接口；DbAdmin.Domain层包含Entity,Dto,Enum和Interface，DbAdmin.Infrastructure里放基础设施，负责方言实现、元数据查询、安全分析

再增加三点如下要求：
sql按制台查询现在是限制最多1000行，请修改成不限制但分页返回，就像阿里云上DMS数据库管理工具一样默认先返回前20条，用户可通过翻页查看后续数据
异常处理：必须记录详细的错误日志及关键参数
详细记录必要的info日志

DbAdmin_最终开发文档.md需要再调整下，模块名称请改成“06-DbAdmin”，模块中的结构请按如下方式调整：06-DbAdmin中包含三个项目，分别是DbAdmin.Service（代替现有的DbAdmin.Application）、DbAdmin.Entity（原DbAdmin.Domain中除Interface之外的部份、DbAdmin.Interface（原DbAdmin中的Interface）

基于这份文档把“首批项目骨架 + 每个项目首批类清单”定下来，然后请基本这份文档进行编码实现，实现过程中请以这份 DbAdmin_最终开发文档.md 作为唯一实现依据。


  已知还没做完、但现在已经进入“实装阶段”的点：
  - MetadataProvider 虽然可用，但 DDL / 索引 / Schema 细节还偏基础
  - SchemaDesignAppService 的“修改表注释”当前还是按通用 SQL 走，跨库兼容性还要继续细化
  - TableDataAppService 现在做了白名单和参数化，但对表名本身的标识符包裹、不同方言细节还可继续收口到方言层
  - SqlConsoleAppService 的总数统计目前已经比之前稳，但复杂 SQL 场景仍建议后续再补一个更严格的 count 包装策略

  我建议下一步继续做这 3 件：
  1. 细化 MetadataProvider，把 GetIndexesAsync、GetTableDdlAsync、GetSchemasAsync 做得更真实
  2. 继续增强 SchemaDesignAppService，补表注释/字段注释/索引删除的跨方言细节
  3. 给 TableDataAppService 和 SqlConsoleAppService 增加统一审计落库，真正把 DbOperationLog 用起来



需要你知道的边界：
  - MetadataProvider.GetTableDdlAsync 现在已经从“占位版”提升到“可用版”，但 PostgreSQL / openGauss 仍是拼装式 DDL 预览，不是完整 pg_dump 级别还原
  - SQL Server 的表/字段注释目前用 sp_addextendedproperty，如果目标对象已存在同名扩展属性，后续还应补“存在则更新、否则新增”的处理
  - TableDataAppService 当前已完成审计接入，但你前面提到的“表名标识符包裹、复合主键、方言收口”这条线我这轮没有继续展开，仍是下一步重点

下一步继续补TableDataAppService：1.表名/字段名统一走方言层 WrapIdentifier；2.Update/Delete 对复合主键做完整支持；3.删除 SQL 再进一步收口，减少手写拼接面；
然后继续下面的优化：
1.MetadataProvider.GetTableDdlAsync 现在已经从“占位版”提升到“可用版”，但 PostgreSQL / openGauss 仍是拼装式 DDL 预览，不是完整 pg_dump 级别还原
2.SQL Server 的表/字段注释目前用 sp_addextendedproperty，如果目标对象已存在同名扩展属性，后续还应补“存在则更新、否则新增”的处理
3.TableDataAppService 当前已完成审计接入，但你前面提到的“表名标识符包裹、复合主键、方言收口”这条线我这轮没有继续展开，仍是下一步重点



下一步继续做：
1.继续收 SqlConsoleAppService，把查询/非查询分支和更严格的总数统计做完。
2.MetadataProvider.GetIndexesAsync 在不同库里进一步保证“主键/唯一索引识别完整且列顺序绝对准确”
3.TableDataAppService 对“表无主键但存在多个唯一索引”的场景，必要时增加更明确的键选择约束，而不是完全自动猜测
4.完善导入导出服务，要求支持Excel和CSV的导入和导出，导入时采用分批写入。导出时采用流式输出。避免一次性加载全部数据到内存。如遇超大数据量场景，本次以限制单次操作规模为主，不额外引入任务体系。只需基本的导入功能即可（无须实现异步导入）

你现在是我的高级.NET架构开发工程师。
任务：根据上一会话总结的文档内容继续完善代码。上一会话总结文档是@DbAdminPhase_ThirdVersion.md
规则：
1. 不进入Plan Mode。
2. 不输出开发计划。
3. 不停下来等待确认。
4. 按要求逐步实现。




在正式开始测试之前：处理GlobalUsing，更改应用模块名称，单元测试项目


