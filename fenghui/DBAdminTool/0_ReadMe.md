
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
/api/db-sources/{id}/tables【04-DataWeaving的DbEntityManageService】


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








IDbDialect中的很多实现方法是不是可以放在一个基类或者抽像类中？因为我们目前使用的是SqlSugar框架，增删改查这种用SqlSugar是不是都能兼容，不需要各种数据库单独实现？【咨询过AI，不建议这么做，这种底层数据库管理工具最好直接用底层ADO，速度更快，为了和项目框架保持一致；使用SqlSugar的ADO也可以，它并没有经过 SqlSugar ORM 的实体映射层】
导入校验可参考现有项目框架对比看下是否还可以进一步完善
D:\code\iotplatformv5\02-应用模块\06-DbAdmin\DbAdmin.Service\MetadataAppService.cs这个类的GetIndexesAsync方法中有根据数据库类型判断执行不同的SQL（该类中的好几个方法都有这种判断），我觉得这样的判断不太合理，不方便未来扩展数据库类型，请推荐一种更好的可扩展的实现方式，比如参照IDbDialect接口一样的实现方式，或者直接放到IDbDialect中一起是否合适，请分析并找出合适的解决方案，
注意：
不要直接到处写 ExecuteCommandAsync，定义统一接口
public interface IDbExecutor
{

    Task<int> ExecuteAsync(
        string sql);


    Task<List<T>> QueryAsync<T>(
        string sql);


}
public class SqlSugarDbExecutor 
       : IDbExecutor
这样以后如果换：Dapper    原生ADO.NET  国产数据库驱动；  都不用改业务。

目前DbAdmin还在开发阶段，现在发现一个问题，当前DbAdmin模块是不是和SqlSugar绑定太深了，到处直接调SqlSugar的方法（比如ExecuteCommandAsync），希望能定义统一接口，这样以后如果换Dapper或原生ADO.NET或国产数据库驱动，都不用改业务。请分析下是否有必要重构，如果需要请提供一个markdown格式的方案文档。
06-DbAdmin模块中，SqlSafetyAnalyzer这个SQL安全分析器类，目前能支持DbEngineType中的所有数据库吗？如果不能支持请优化代码，要求扩展性高，方便未来扩展新的数据库管理系统，该模块的开发需求见【D:\code\iotplatformv5\03-应用服务\IotPlatform\DbAdmin_最终开发文档.md】
日志表本身就有创建人信息，所以OperatorId和OperatorName可以去掉了
创建人id，创建人名称，修改人，修改人名称

  下一步我建议直接继续两件事：
  1. 重新启动 IotPlatform.Web.Core。
  2. 用真实接口验证 schemas / indexes / ddl，再决定是否需要动 SqlServerDialect.GetTableDdlAsync(...) 和 ExecutePagedQueryAsync(...) 的分页归一化链路。
   

更改应用模块名称
DbAdmin.Infrastructure模块代码优化
请总体上从代码扩展性、健壮性、高性能等各方面综合分析DbAdmin.Infrastructure模块和DbAdmin.Interface的代码，给出还有哪些可优化的地方




请根据你的建议逐条改造P0中的如下7点建议：
1. 多 Schema/同名表元数据可能取错对象
2. 数值列精度映射存在明确错误
3. 命令执行契约缺少取消、超时与执行边界
4. 连接上下文仍泄漏完整数据源密钥，并且没有生命周期契约
5. SQL 安全策略存在两套实现且语义不一致
6. DDL 构造的输入约束不完整
7. DDL 回退结果可能被误当成可执行建表脚本


根据你的前几轮的分析，我也发现IDbDialect接口过于臃肿，需要按能力拆为元数据、DDL、分页等纯契约接口，请提供详细的接口拆分和实现方案
【完成】IDbDialect实现类中：移除死方法 TryNormalizePagedQuerySource 和重复安全方法
方言的元数据 API 用同步 GetDataTable/DbMaintenance 后套 Task.FromResult，高并发时会阻塞线程池。优先使用驱动异步 API；无法异步时应通过每数据源并发阈值、短 TTL 元数据缓存、single-flight 防止缓存击穿，而不是 Task.Run。
DataSourceResolver 与 DbConnectionContextFactory 重复缓存同一个 DbLink：D:/code/iotplatformv5/02-应用模块/16-DbAdmin/DbAdmin.Infrastructure/Connections/DataSourceResolver.cs:27、D:/code/iotplatformv5/02-应用模块/16-DbAdmin/DbAdmin.Infrastructure/Connections/DbConnectionContextFactory.cs:41。应统一为内部IDbSourceSnapshotProvider，以 SourceId + UpdatedTime/版本号 缓存，并在配置更新后精确失效。
【完成】ExecuteInTransactionAsync 的回调没有显式事务执行器、隔离级别、超时或取消令牌：D:/code/iotplatformv5/02-应用模块/16-DbAdmin/DbAdmin.Interface/Interface/IDbCommandExecutor.cs:25。建议回调接收 transaction-bound executor，避免误用其他连接上下文。
【完成】偏移分页直接拼入整数，未在方言层防御非法页码、溢出和深分页：D:/code/iotplatformv5/02-应用模块/16-DbAdmin/DbAdmin.Infrastructure/Dialects/PostgreSqlDialect.cs:44。引入 PageRequest，使用 checked offset；大表导出/浏览支持 keyset cursor。
DialectFactory 对重复引擎注册直接由 ToDictionary 抛异常：D:/code/iotplatformv5/02-应用模块/16-DbAdmin/DbAdmin.Infrastructure/Dialects/DialectFactory.cs:12。应在启动阶段做显式重复注册、缺失能力和驱动版本校验，错误信息包含实现类型。


【完成】目前只有修改表注释，为什么不让修改表名？能不能改造这个接口允许同时修改表名和表注释



单元测试不需要
OpenGaussDialect 和 KingbaseEsDialect也先不用实现

   

1.添加表和修改表：business_dbentitymanage,  修改表【原功能：先DropTable再CreateTable，所以表中有数据时不允许修改表结构】，修改表的时候需要前端告诉接口三个数据（1.新增字段列表；2.修改字段列表；3.删除字段列表）
2.数据建模：business_tablefield    

实体管理，如果表中有数据则无法修改。这里不能简单的放开不限制，原来的修改表结构是先drop table再created table


D:\code\iotplatformv5\02-应用模块\16-DbAdmin是数据库管理工具模块的代码，目前在接口开发阶段，前端还未接入，16-DbAdmin模块中DbAdmin.Service下面，SchemaDesignAppService、MetadataAppService、SqlConsoleAppService、TableDataAppService、ImportExportAppService这几个Service是核心接口，

【已完成】工程结构调整、扩展性、兼容性、性能、风险
【已完成】表结构设计服务SchemaDesignAppService
【已完成】元数据查询服务MetadataAppService
【已完成】表数据服务TableDataAppService  
【已完成】表数据导入导出服务ImportExportAppService
SQL控制台服务SqlConsoleAppService：SQL查询和SQL执行是否需要分开？哪个更合适？
其他：创建数据库、生成表模板SQL、实体管理页面相关接口
接口分类及代码结构调整，swagger注释，属性注释、log日志、error日志、审计日志、完善注释（无注释的方法，英文注释的方法，类的属性注释）
单元测试 
接口测试
接口文档整理：比如创建表、修改表接口需要详细说明，不同类型传如何传数据
前后端联调
功能测试
构造方言SQL源代码
进一步分析项目代码

性能排查：查询、更新元数据或表数据相关SQL
风险排查：sql注入风险，sql控制台语句权限？其他风险


本周总结：
  排产算法优化：（1）指定模具不会被排到不配对设备 （2）处理动态作业与固定作业之间的过渡关系，确保它们在同一机器上时满足换模准备时间约束。
  DbAdmin开发：完成工程结构调整、表结构设计服务、元数据查询服务、表数据服务，表数据导入导出服务相关接口功能，完成这些接口功能的代码分析、代码逻辑优化、风险排查、性能排查、复用并重构通用逻辑、审计日志等。大致看了下由AI生成底层数据库方言源码，后期还需要详细阅读和分析。
下周计划：
  SQL控制台服务相关接口
  其他接口：创建数据库、生成表模板SQL、实体管理页面相关接口、结合前端功能页面排查缺少的接口并补全
  接口分类及代码结构调整，完善注释、swagger注释、log日志、error日志、审计日志
  单元测试
  接口测试
  底层数据库方言源码



## 现有接口
### 表单设计 
    获取表字段信息：/table/{linkId}/Table/{tableName}
    添加字段：/table/{linkId}/addFields
    添加表单：不创建表，仅生成VisualDevEntity
    发布表单：会在无表转有表时自动创建表



## 字段测试
  varchar和text字段测试
  字段长度、精度、小数位
  默认值只支持 NULL、布尔值、数字、单引号字符串或受控时间函数：目前支持的函数包括"NULL", "TRUE", "FALSE", "CURRENT_TIMESTAMP", "CURRENT_TIMESTAMP()", "CURRENT_DATE", "CURRENT_TIME", "NOW()"【新增表和修改表结构接口，检查下
  【已处理】修改表结构：不支持在本接口中修改主键字段，不支持在本接口中删除主键字段,不支持通过新增字段创建主键成员
  修改表结构：主键、外键约束、主键自增测试

## 在正式开始测试之前：
  处理GlobalUsing，单元测试->让AI只看接口定义设计单元测试，测试所有可能的应用场景
  检查关键步骤日志是否完善，关键步骤异常处理是否完完善，异常日志是否完善
  检查并完善注释，swagger分组及注释，英文注释改成中文注释
  

## 待完成任务：
  给前端生成接口文档：特别是创建表结构和修改表结构，不同类型应该如何传递字段值，数据库表数据CRUD；给前端生成一份接口调用指南（比如更新表数据接口，通过什么接口获取表数据及主键，如何传递入参）
  单元测试（先删除现有零散的测试）：需要给DbAdmin添加单元测试，未来需要给其他模块也添加单元测试，请提供建议，在当前解决方案中应该将单元测试放在哪里比较合适？
  实体管理页面：数据源接口、查询列表接口、常用字段，实体类分页查询接口、实体类详情查询接口、常用字段相关接口、标签
  数据库CRUD：添加数据库接口，删除数据库接口是否需要？修改数据库
  创建表、更新表、查询字段列表等接口的字段属性名称最好能和旧接口一致或者接近
  D:\code\iotplatformv5\02-应用模块\16-DbAdmin是数据库管理工具模块的代码，目前在接口开发阶段，前端还未接入，16-DbAdmin模块中DbAdmin.Service下面，SchemaDesignAppService、MetadataAppService、SqlConsoleAppService、TableDataAppService、ImportExportAppService这几个Service是核心接口。目前发现各服务之间有些代码可以复用但目前没有复用，请根据以下要求项目代码综合考虑扩展性、健壮性、长期可维护性对代码进行优化，适当的时候可以使用设计模式提升代码质量，也可将通用逻辑下沉到DbAdmin.Infrastructure中，具体如何实施请你根据综合情况择优处置：1.获取表字段、主键、索引、等功能，如能复用请尽量复用，但不能复用时不要勉强。2.“TableDataAppService中的表数据查询”和“ImportExportAppService中的表数据导出”的分页数据查询及排序等逻辑，如能复用请尽量复用，但不能复用时不要勉强，目前的表数据表导出功能无法根据指定条件导出数据（你可以帮我补上这个功能，最好能和查询表数据的条件逻辑共用）。3.SqlConsoleAppService中的控制台查询数据功能，对查询结果进行分页展示和导出的逻辑如果能复用“表数据查询”则可复用，但不能复用时请勿勉强。【补充：考虑实际情况复用代码逻辑，择优处置即可】
  排查的可复用代码逻辑，如存在则尽量复用。【补充：考虑实际情况复用代码逻辑，择优处置即可】
  修改接口路由
  固定常量不要直接写到逻辑代码中，至少应该定义在类的属性中，比如if (values.Count > 1000) throw new ArgumentException("IN 条件值数量不能超过 1000");排查项目中是否还存在类似的情况并修复
  【已完成】TableDataAppService的查询接口，QueryFilterItem.Operator操作符请使用枚举，QueryFilterItem.Logic请使用枚举
  【已完成】TableDataAppService的删除接口和更新接口，只需要按主键删除或更新数据即可，不需要按唯一键删除或更新数据，请改造代码
  【已完成】TableDataAppService的新增接口需要支持一次添加多行数据，更新接口需要支持一次更新多行数据
  【已完成】请分析TableDataAppService中的四个接口的代码，【如果存在sql注入风险则进行优化（已执行，未发现将用户值拼接为sql的问题）】，如果性能上有优化空间则进行优化
  【已完成】16-DbAdmin模块中DbAdmin.Service下面，SchemaDesignAppService、MetadataAppService、SqlConsoleAppService、TableDataAppService、ImportExportAppService这几个服务，发现有些功能缺少异常log日志，请补全。另外，如果缺少异常审计日志是否应该加上比较合适？如果你认为需要则请加上。如发现接口和关键方法缺少注释则请加上中文注释，如发现英文注释则请改成中文注释。
 【已完成】删除表结构接口：强制限制不能删除有数据的表
 【已完成】修改请求路由，参数封装
 【已完成】日志完善：DbEntityManageSyncService类中的方法很重要，请增加详细的infomation日志，在执行SQL后增加相关的结果日志，如“创建实体管理表数据，结果：{}"，你可以帮我优化下方案描述，结果是true或者false，如果是更新成功则结果是更新的数量
 【已完成】修改表元数据接口，目前只能修改表名称和表说明信息，我希望扩展该接口功能，允许该接口不但能修改表名和表说明信息，同时也能添加字段、修改字段、删除字段，类似于navicat这种数据库管理工具一样允许用户在前端一次性编辑和提交
  【已完成】创建表结构和修改表结构：当前的字段默认值是否已达到上线使用的标准，有没有优化空间？比如目前支持的默认值函数是否完整等等，请帮我补齐相关代码逻辑】
  【已完成】全面检查和分析“创建表结构接口”和“修改表结构接口”，判断是否已经达到生产环境可用的标准，如有优化空间或更好的解决方案，请帮我优化相关代码逻辑
  【已完成】DbAdmin.Service中的Internal中的类是否放在DbAdmin.Infrastructure中更合适？
  【】在SchemaDesignAppService中新增一个实体类列表分页查询接口，具体查询逻辑在DbEntityManageSyncService中实现（该类需要增加实体类列表分页查询接口（参数：表名、configId），同时请看下是否需要取一个更合适的类名称），可参照IotPlatform.DataWeaving.Servers.DbEntityManageService.DbEntityManagePage中的方法实现
  【】在SchemaDesignAppService中新增一个实体类详情查询接口，具体查询逻辑在DbEntityManageSyncService中实现，可参照IotPlatform.DataWeaving.Servers.DbEntityManageService.GetInfo实现。
  【】分析MetadataAppService中的接口代码，判断是否已经达到生产环境可用的标准，如有优化空间或更好的解决方案，请帮我优化相关代码逻辑
  
### 优化方向：
  1.大数据量建议增加游标分页，50至500万条数据建议使用游标分页，大于500万条数据强制使用游标分页
  2.DbAdmin引用了DataWeaving中的DbEntityManage实体
  3.DbAdmin相关接口暂不兼容现有前端，前端需要重新对接，创建表、更新表、查询字段列表等接口的字段属性名称，最好能和原来一致或者接近
  4.数据库表导入导出：最大导出 50,000 行，最大导入100,000。导入文件不能超过 50 MB。导出大量数据时，如果数据在查询过程中发生变化（增删改），可能导致数据重复或遗漏
  5.控制台SQL执行权限及限制？
### 风险
  删除字段，丢失数据，修改主键
  导出大量数据：如果数据在查询过程中发生变化（增删改），可能导致数据重复或遗漏
  
  


取消令牌参数的作用：cancellationToken.ThrowIfCancellationRequested();
基础排版标题：用 # 表示，1个 # 是一级标题，2个 # 是二级标题，最多到六级。加粗与斜体：文字前后加 ** 是加粗，加 * 是斜体。删除线：文字前后加 ~~ 即可添加删除线。
结构与引用列表：用 - 或 * 加空格表示无序列表，用数字加 . 表示有序列表。引用：在段落前加 > 符号，适合摘抄或重点提示。分割线：在一行中输入三个 - 或 *。
高级与媒体链接与图片：用 [文字](网址) 插入链接，用 ![替代文字](图片链接) 插入图片。代码块：单行代码用 ` 包裹，多行代码用三个反引号 ``` 包裹并可指定语言。表格：用 | 隔开不同列，用 |---| 隔开表头与内容。

## P0：上线前建议完成

  1. 多 Schema/同名表元数据可能取错对象

     三个方言的表、视图、列读取大量依赖 DbMaintenance，调用时未携带 database/schema；返回 DTO 的 Schema 只是请求值，不是数据库实际值。例如 D:/code/iotplatformv5/02-应用模块/16-DbAdmin/DbAdmin.Infrastructure/Dialects/MySqlDialect.cs:76、D:/code/iotplatformv5/02-应用模块/16-DbAdmin/DbAdmin.Infrastructure/
     Dialects/SqlServerDialect.cs:110。

     MySQL 索引查询固定使用 database()，忽略传入的 database 参数：D:/code/iotplatformv5/02-应用模块/16-DbAdmin/DbAdmin.Infrastructure/Dialects/MySqlDialect.cs:142。回退逻辑也只传 table：D:/code/iotplatformv5/02-应用模块/16-DbAdmin/DbAdmin.Infrastructure/Metadata/MetadataFallbackHelper.cs:27。

     方案：引入不可变 DbObjectName(Database, Schema, Name)，所有元数据、DDL、索引、回退 API 均以它定位对象。以各数据库系统目录的参数化 SQL 作为唯一真源，SqlSugar DbMaintenance 仅作为无 Schema 场景的兼容回退。

  2. 数值列精度映射存在明确错误

     三个方言将 Precision 和 Scale 都赋值为 DecimalDigits，数值精度无法被正确表达，例如 D:/code/iotplatformv5/02-应用模块/16-DbAdmin/DbAdmin.Infrastructure/Dialects/MySqlDialect.cs:117。

     方案：改用系统目录中的 numeric_precision、numeric_scale；若继续使用 SqlSugar，则确认 DbColumnInfo 中精度对应字段后分别赋值。为 decimal(18,2)、numeric(30,8)、非数值列建立跨库集成测试。

  3. 命令执行契约缺少取消、超时与执行边界

     D:/code/iotplatformv5/02-应用模块/16-DbAdmin/DbAdmin.Interface/Interface/IDbCommandExecutor.cs:10 的全部方法没有 CancellationToken、超时或执行选项；执行器直接调用 SqlSugar：D:/code/iotplatformv5/02-应用模块/16-DbAdmin/DbAdmin.Infrastructure/Execution/SqlSugarDbCommandExecutor.cs:13。

     方案：增加 DbExecutionOptions，至少包含 CommandTimeout、最大返回行数/字节数、CancellationToken、操作名称。默认超时由数据源配置控制，禁止请求自行无限放大。数据库驱动不支持取消时，也要在 Web 请求断开后停止后续分页、导出和审计写入。

  4. 连接上下文仍泄漏完整数据源密钥，并且没有生命周期契约

     D:/code/iotplatformv5/02-应用模块/16-DbAdmin/DbAdmin.Interface/Interface/IDbConnectionContext.cs:10 向 Service 暴露 DbLink，通常含账号、密码和连接串。实现还持有客户端，但 IDbConnectionContext 未定义释放/租约语义：D:/code/iotplatformv5/02-应用模块/16-DbAdmin/DbAdmin.Infrastructure/Connections/
     SqlSugarDbConnectionContext.cs:10。

     方案：契约改为仅暴露 SourceId、显示名、引擎、目标数据库等非敏感 DbSourceDescriptor。真实 DbLink 和 ISqlSugarClient 留在 Infrastructure；上下文改为内部连接租约，明确由执行器负责获取和释放。

  5. SQL 安全策略存在两套实现且语义不一致

     统一分析器限制首语句为 SELECT/WITH：D:/code/iotplatformv5/02-应用模块/16-DbAdmin/DbAdmin.Infrastructure/Security/SqlSafetyAnalyzer.cs:23，但 IDbDialect 又定义了未被调用、且更弱的方言安全分析方法：D:/code/iotplatformv5/02-应用模块/16-DbAdmin/DbAdmin.Interface/Interface/IDbDialect.cs:56。

     手写 tokenizer 对 PostgreSQL dollar quote、MySQL version comment、嵌套注释、SQL Server 转义方括号等语法覆盖不足。

     方案：删除 IDbDialect.AnalyzeSqlSafety 及三份重复实现，保留一个按引擎配置的安全策略服务。生产默认“解析失败即拒绝”，用 SQL AST 解析器或按数据库适配的 parser；将允许语句、系统库访问、跨库访问定义为显式策略，而非关键词黑名单。

  6. DDL 构造的输入约束不完整

     默认值和未知数据类型会直接拼入 SQL，例如 D:/code/iotplatformv5/02-应用模块/16-DbAdmin/DbAdmin.Infrastructure/Dialects/MySqlDialect.cs:299。标识符虽转义，但未形成完整的对象名/类型/默认值白名单模型。

     方案：ColumnDefinition 使用受限类型枚举，DefaultValue 改为结构化 DefaultExpression，只允许 NULL、受控字面量及少量方言函数；所有 DDL 请求在方言构造前统一校验名称、列数量、长度和精度范围。

  7. DDL 回退结果可能被误当成可执行建表脚本

     回退 DDL 未包含完整 schema、标识符转义、索引、约束、identity 等信息：D:/code/iotplatformv5/02-应用模块/16-DbAdmin/DbAdmin.Infrastructure/Metadata/MetadataFallbackHelper.cs:62。

     方案：GetTableDdlAsync 返回 TableDdlResult，含 Sql、Source、IsExecutable、Warnings。回退结果仅标识为预览，不能用于迁移或导入。

  8. 测试缺口过大

     当前模块未发现测试项目，只有业务 DTO DbSourceTestInput。方言、元数据、分页、SQL 安全、事务、导入导出均缺少自动化保护。

     方案：至少建立 MySQL、SQL Server、PostgreSQL 的 Testcontainers 集成测试；覆盖多 Schema 同名表、复合主键、大小写标识符、特殊注释、分页边界、超时取消和权限拒绝。

  ## P1：扩展性与性能演进

  - IDbDialect 同时承担元数据、分页、DDL、DML、类型映射和安全分析，已成为大接口：D:/code/iotplatformv5/02-应用模块/16-DbAdmin/DbAdmin.Interface/Interface/IDbDialect.cs:11。不建议立刻拆成大量 Provider；先移除死方法 TryNormalizePagedQuerySource 和重复安全方法，再按能力拆为元数据、DDL、分页等纯契约接口，且只使
    用 IDbConnectionContext、DTO 和值对象。

  - 方言的元数据 API 用同步 GetDataTable/DbMaintenance 后套 Task.FromResult，高并发时会阻塞线程池。优先使用驱动异步 API；无法异步时应通过每数据源并发阈值、短 TTL 元数据缓存、single-flight 防止缓存击穿，而不是 Task.Run。
  - DataSourceResolver 与 DbConnectionContextFactory 重复缓存同一个 DbLink：D:/code/iotplatformv5/02-应用模块/16-DbAdmin/DbAdmin.Infrastructure/Connections/DataSourceResolver.cs:27、D:/code/iotplatformv5/02-应用模块/16-DbAdmin/DbAdmin.Infrastructure/Connections/DbConnectionContextFactory.cs:41。应统一为内部
    IDbSourceSnapshotProvider，以 SourceId + UpdatedTime/版本号 缓存，并在配置更新后精确失效。

  - DbCommandParameter 只有名称和值：D:/code/iotplatformv5/02-应用模块/16-DbAdmin/DbAdmin.Interface/Interface/DbCommandParameter.cs:5。应增加 DbType、长度、精度、Scale、Direction，并改为不可变 record；物理文件也应移至 Entity DTO 目录，与 namespace 一致。
  - ExecuteInTransactionAsync 的回调没有显式事务执行器、隔离级别、超时或取消令牌：D:/code/iotplatformv5/02-应用模块/16-DbAdmin/DbAdmin.Interface/Interface/IDbCommandExecutor.cs:25。建议回调接收 transaction-bound executor，避免误用其他连接上下文。
  - 偏移分页直接拼入整数，未在方言层防御非法页码、溢出和深分页：D:/code/iotplatformv5/02-应用模块/16-DbAdmin/DbAdmin.Infrastructure/Dialects/PostgreSqlDialect.cs:44。引入 PageRequest，使用 checked offset；大表导出/浏览支持 keyset cursor。
  - OpenGaussDialect 和 KingbaseEsDialect 基本直接继承 PostgreSQL：D:/code/iotplatformv5/02-应用模块/16-DbAdmin/DbAdmin.Infrastructure/Dialects/OpenGaussDialect.cs:11。同时数据源映射未见 OpenGauss 分支：D:/code/iotplatformv5/02-应用模块/16-DbAdmin/DbAdmin.Infrastructure/Connections/
    DbConnectionContextFactory.cs:86。应建立引擎能力矩阵和开关，集成验证通过后再开放功能。

  - DialectFactory 对重复引擎注册直接由 ToDictionary 抛异常：D:/code/iotplatformv5/02-应用模块/16-DbAdmin/DbAdmin.Infrastructure/Dialects/DialectFactory.cs:12。应在启动阶段做显式重复注册、缺失能力和驱动版本校验，错误信息包含实现类型。
  - 增加统一的 OpenTelemetry 指标与慢 SQL 诊断：连接建立耗时、命令耗时、超时、失败类别、元数据缓存命中率、每数据源并发数；日志中仅保存脱敏摘要，不能记录连接串和参数值。

  建议实施顺序：先完成对象定位、执行治理、密钥边界、统一 SQL 安全和集成测试；随后处理缓存、能力拆分、参数模型和深分页。这一顺序能先消除“读错表、失控执行、敏感信息暴露”三类生产风险。

  
    // [HttpPost("/api/db-admin/schema/columns/add")]
    // public async Task<int> AddColumn([FromBody] AddColumnRequest input)
    // {
    //     var id = input.Target.SourceId;
    //     var table = input.Target.TableName;
    //     DbSchemaRequestValidator.ValidateColumn(input.Column);
    //     await using var context = await _connectionContextFactory.CreateAsync(id, input.Target.Database);
    //     var dialect = _dialectResolver.Get<IDbSchemaDdlDialect>(context.EngineType);
    //     input.Target.TableName = DbAdminRequestGuard.QualifiedTable(input.Target.Schema, table);
    //     var sql = dialect.BuildAddColumnSql(input);
    //     var result = await ExecuteDdlAsync(context, id, input.Target.Database, input.Target.Schema, table, sql);
    //     _logger.LogInformation("新增字段完成，SourceId={SourceId}, Table={Table}, Column={Column}, AffectedRows={AffectedRows}, SqlDigest={SqlDigest}", id, table, input.Column.Name, result, DbAdminLogHelper.TrimSql(sql));
    //     return result;
    // }
    //
    // [HttpPost("/api/db-admin/schema/columns/alter")]
    // public async Task<int> AlterColumn([FromBody] AlterColumnRequest input)
    // {
    //     var id = input.Target.SourceId;
    //     var table = input.Target.TableName;
    //     var column = input.Target.ColumnName;
    //     DbAdminRequestGuard.RequireIdentifier(column, "字段名");
    //     if (string.IsNullOrWhiteSpace(input.Column.Name))
    //     {
    //         input.Column.Name = column;
    //     }
    //
    //     DbSchemaRequestValidator.ValidateColumn(input.Column);
    //     await using var context = await _connectionContextFactory.CreateAsync(id, input.Target.Database);
    //     var dialect = _dialectResolver.Get<IDbSchemaDdlDialect>(context.EngineType);
    //     var metadataDialect = _dialectResolver.Get<IDbMetadataDialect>(context.EngineType);
    //     var existingColumn = (await metadataDialect.GetColumnsAsync(context, new DbObjectName(input.Target.Database, input.Target.Schema, table)))
    //         .FirstOrDefault(item => item.ColumnName.Equals(column, StringComparison.OrdinalIgnoreCase))
    //         ?? throw Oops.Oh($"待修改字段不存在：{column}");
    //     input.Target.TableName = DbAdminRequestGuard.QualifiedTable(input.Target.Schema, table);
    //     input.Target.ColumnName = column;
    //
    //     var sql = dialect.BuildAlterColumnSql(input, existingColumn);
    //     var result = await ExecuteDdlAsync(context, id, input.Target.Database, input.Target.Schema, table, sql);
    //     _logger.LogInformation("修改字段完成，SourceId={SourceId}, Table={Table}, Column={Column}, AffectedRows={AffectedRows}, SqlDigest={SqlDigest}", id, table, column, result, DbAdminLogHelper.TrimSql(sql));
    //     return result;
    // }
    //
    // [HttpPost("/api/db-admin/schema/columns/drop")]
    // public async Task<int> DropColumn([FromBody] DropColumnRequest input)
    // {
    //     var id = input.Target.SourceId;
    //     var table = input.Target.TableName;
    //     var column = input.Target.ColumnName;
    //     DbAdminRequestGuard.RequireIdentifier(column, "字段名");
    //     await using var context = await _connectionContextFactory.CreateAsync(id, input.Target.Database);
    //     var dialect = _dialectResolver.Get<IDbSchemaDdlDialect>(context.EngineType);
    //     input.Target.TableName = DbAdminRequestGuard.QualifiedTable(input.Target.Schema, table);
    //     input.Target.ColumnName = column;
    //     var sql = dialect.BuildDropColumnSql(input);
    //     var result = await ExecuteDdlAsync(context, id, input.Target.Database, input.Target.Schema, table, sql);
    //     _logger.LogInformation("删除字段完成，SourceId={SourceId}, Table={Table}, Column={Column}, AffectedRows={AffectedRows}, SqlDigest={SqlDigest}", id, table, column, result, DbAdminLogHelper.TrimSql(sql));
    //     return result;
    // }
    //
    // [HttpPost("/api/db-admin/schema/indexes/create")]
    // public async Task<int> CreateIndex([FromBody] CreateIndexRequest input)
    // {
    //     var id = input.Target.SourceId;
    //     var table = input.Target.TableName;
    //     DbAdminRequestGuard.RequireIdentifier(input.Index.Name, "索引名");
    //     if (input.Index.Columns.Count == 0) throw Oops.Oh("索引必须包含字段");
    //     foreach (var column in input.Index.Columns) DbAdminRequestGuard.RequireIdentifier(column, "索引字段");
    //     await using var context = await _connectionContextFactory.CreateAsync(id, input.Target.Database);
    //     var dialect = _dialectResolver.Get<IDbSchemaDdlDialect>(context.EngineType);
    //     input.Target.TableName = DbAdminRequestGuard.QualifiedTable(input.Target.Schema, table);
    //     var sql = dialect.BuildCreateIndexSql(input);
    //     var result = await ExecuteDdlAsync(context, id, input.Target.Database, input.Target.Schema, table, sql);
    //     _logger.LogInformation("创建索引完成，SourceId={SourceId}, Table={Table}, Index={Index}, AffectedRows={AffectedRows}, SqlDigest={SqlDigest}", id, table, input.Index.Name, result, DbAdminLogHelper.TrimSql(sql));
    //     return result;
    // }
    //
    // [HttpPost("/api/db-admin/schema/indexes/drop")]
    // public async Task<int> DropIndex([FromBody] DropIndexRequest input)
    // {
    //     var id = input.Target.SourceId;
    //     var table = input.Target.TableName;
    //     var index = input.Target.IndexName;
    //     DbAdminRequestGuard.RequireIdentifier(index, "索引名");
    //     await using var context = await _connectionContextFactory.CreateAsync(id, input.Target.Database);
    //     var dialect = _dialectResolver.Get<IDbSchemaDdlDialect>(context.EngineType);
    //     input.Target.TableName = DbAdminRequestGuard.QualifiedTable(input.Target.Schema, table);
    //     input.Target.IndexName = index;
    //     var sql = dialect.BuildDropIndexSql(input);
    //     var result = await ExecuteDdlAsync(context, id, input.Target.Database, input.Target.Schema, table, sql);
    //     _logger.LogInformation("删除索引完成，SourceId={SourceId}, Table={Table}, Index={Index}, AffectedRows={AffectedRows}, SqlDigest={SqlDigest}", id, table, index, result, DbAdminLogHelper.TrimSql(sql));
    //     return result;
    // }


    _tableMetadataProvider is null
                ? new DbTableMetadataSnapshot(
                    await metadataDialect.GetColumnsAsync(context, new DbObjectName(target.Database, target.Schema, originalTableName), cancellationToken),
                    await metadataDialect.GetIndexesAsync(context, new DbObjectName(target.Database, target.Schema, originalTableName), cancellationToken),
                    await metadataDialect.GetTableConstraintsAsync(context, new DbObjectName(target.Database, target.Schema, originalTableName), cancellationToken))
                : 