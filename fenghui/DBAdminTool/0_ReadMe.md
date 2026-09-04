
## 前后端联调
测试数据库管理工具：不同数据类型，不同方言数据库
创建数据库：接口已开发，没有UI，暂时没有前端页面实现
导入功能：目前实现了【仅插入数据】这种导入模式，未实现UI中的重建表并导入、仅创建表、清空表后导入，其中“清空表导入”不太安全，容易误操作清空业务数据，可代替的操作是让用户先在控制台写SQL删除再回到这个导入页面进行数据导入，以后可以给SQL控制台加权限。另外的“重建表并导入、仅创建表”不清楚作用是什么。
与navicat的区别，在iotPlat平台的数据库管理工具中，创建表默认会自动创建实体信息，兼容旧功能的实体管理；修改表时如果没有实体也会自动创建实体信息

导出按扭，加个“是否确定导出全表数据“的提示，防止误操作，有些表数据量很大，大量操作会导致数据库宕机。
添加和编辑表字段，缺少“是否自增长id”
导出：空值默认用\N填充
数据库管理工具，如果接口返回失败，你要把后端返回的错误详情显示到提醒消息中
默认值：'a'::character varying，无法重现
【已完成】修改表结构："BLOB/TEXT column 'name' used in key specification without a key length"
【已完成】拆分出独立的“添加索引接口”和“删除索引接口”
【已完成】导出：表没有主键是数据导出失败
【已完成】表数据查询传参
【已完成】默认值：修改默认值有Constant，删除默认值用Clear
【已完成】修改表结构，部份成功部份失败如何处理？【部分失败时把失败/跳过操作放到外层 Data，把错误汇总文本放到外层 Errors】
【已完成】所有接口的DbTarget中的DataBase字段都不需要传
【已完成】复DDL查询不元数据长度转换失败问题（longtext 字段）
检查是否存在异步阻塞问题及其他性能问题

提交
发布117，验证DbAdmin
修改数据库连接，编写版本变更信息
提交并推送
publish 
打包并上传禅道
联系吴工：表单模块数据导入相关问题都已处理，已发布117，项目代码已打包并上传至禅道，可以发布了


议将 UpdateTableMeta 改为“顺序编排 + 即时记录”，不再预生成队列。

  核心调整如下：

  1. 删除 CreateOperationQueue、DequeueOperation 以及 FinishBatch 中对未执行操作标记 Skipped 的逻辑。
  2. 在 ExecuteDdlOperationAsync 内部创建当前操作对象：

  var operation = new TableSchemaOperationResultDto
  {
      Sequence = batch.Operations.Count + 1,
      OperationType = operationType,
      ObjectName = objectName
  };

  batch.Operations.Add(operation);

  3. CompleteDdlOperation 同样直接创建并加入操作结果，用于“主键字段变更但无需执行 DDL”等组合操作场景。
  4. ExecuteEntitySyncAsync 也创建一条 SyncEntityManage 操作记录，成功或失败都更新该对象状态。
  5. CompleteUpdateAsync 只根据当前已经执行过的操作计算结果：

  result.Success = result.Operations.Count > 0 &&
                   result.Operations.All(operation => operation.Success);

  失败时，当前失败操作会保留，后续代码直接 return await CompleteUpdateAsync()，因此不会返回任何未来步骤，也不会再生成 Skipped 记录。
  6. UpdateTableMeta 中的执行代码保持现有顺序不变：

  更新表元数据
  同步实体管理
  删除索引
  创建临时索引
  删除主键
  修改字段
  新增字段
  删除字段
  创建主键
  删除临时索引
  创建索引

  这种方式的优点是：新增步骤只需要在执行流程中增加一段代码，不需要再同步修改一个独立的队列生成方法；操作序号也由 batch.Operations.Count + 1 自动生成，执行顺序和返回结果天然一致。

  需要同步更新现有测试：

  - 删除或改写依赖 CreateOperationQueue 的测试；
  - 增加操作序号连续性测试；
  - 增加失败后不包含后续操作的测试；
  - 增加实体同步成功、失败时均写入 SyncEntityManage 结果的测试；
  - 保留 DDL 审计日志及既有主键、Identity 操作顺序测试。

修改表名及注释

请求示例：
添加索引
  {
    "Target": {
      "SourceId": 760596595065029,
      "TableName": "orders"
    },
    "Indexes": [
      {
        "Name": "IX_orders_code",
        "Columns": ["code"],
        "IsUnique": false
      }
    ]
  }

删除索引
  {
    "Target": {
      "SourceId": 760596595065029,
      "TableName": "orders"
    },
    "IndexNames": ["IX_orders_code"]
  }




Keep	忽略	创建时不设置；修改时保留原默认值
Clear	忽略	修改时删除默认值
Constant	字符串	常量。数字也必须传字符串，如 "0"、"12.50"；文本传 "未命名"
Null	忽略	默认值为 SQL NULL
CurrentTimestamp	忽略	数据库当前本地时间表达式
CurrentUtcTimestamp	忽略	数据库当前 UTC 时间表达式

## 现有接口
### 表单设计 
    获取表字段信息：/table/{linkId}/Table/{tableName}
    添加字段：/table/{linkId}/addFields
    添加表单：不创建表，仅生成VisualDevEntity
    发布表单：会在无表转有表时自动创建表

### 优化方向：
  1.大数据量建议增加游标分页，50至500万条数据建议使用游标分页，大于500万条数据强制使用游标分页
  2.DbAdmin引用了DataWeaving中的DbEntityManage实体
  3.DbAdmin相关接口暂不兼容现有前端，前端需要重新对接，创建表、更新表、查询字段列表等接口的字段属性名称，最好能和原来一致或者接近
  4.数据库表导入导出：最大导出 50,000 行，最大导入100,000。导入文件不能超过 50 MB。导出大量数据时，如果数据在查询过程中发生变化（增删改），可能导致数据重复或遗漏
  5.控制台SQL执行权限及限制？
  6.导出EXCEL：确保所有类型数据都能正常显示，二进制字段如何处理？
  7.导入模式：清空表后导入的安全性问题
  8.大批量数据导入导出时，增加警告提示：尽量在空闲时间执行，比如10万以上
  9.Sql控制台执行支持一请求包含多条 SQL（例如 SELECT; UPDATE），并按顺序实际执行
  10.stream.GetReader(useHeaderRow: true);会有额外成本
### 风险
  删除字段，丢失数据，修改主键，删除字段会引起数据丢失
  导出大量数据：如果数据在查询过程中发生变化（增删改），可能导致数据重复或遗漏
  sql控制台：执行风险SQL
  
  

## 待完成任务：
  D:\code\iotplatformv5\02-应用模块\16-DbAdmin是数据库管理工具模块的代码，请给前端生成一份接口调用指南，要求每个接口有调用示例，特别是创建表结构和修改表结构，比如不同字段类型应该如何传递字段值，不同数据类型的默认值如何指定等


## 测试
  varchar和text字段测试
  字段长度、精度、小数位
  默认值只支持 NULL、布尔值、数字、单引号字符串或受控时间函数：目前支持的函数包括"NULL", "TRUE", "FALSE", "CURRENT_TIMESTAMP", "CURRENT_TIMESTAMP()", "CURRENT_DATE", "CURRENT_TIME", "NOW()"【新增表和修改表结构接口，检查下
  【已处理】修改表结构：不支持在本接口中修改主键字段，不支持在本接口中删除主键字段,不支持通过新增字段创建主键成员
  修改表结构：主键、外键约束、主键自增测试
  导入导出接口性能测试


【已解决】索引类型字段多余
【已解决】新增表结构没有保存表注释及标签
【已解决】更新表结构接口缺少标签修改能力。
【已解决】生成数据表 SQL 模板：返回的SQL包含转义以及/n/r换行符，select语句没有限制返回行数
【已解决】数据库审计日志：目前记录的SQL缺少参数值，例delete from "t_db_test_student" where "id" = @p0
【已解决】数据表查询接口：按主键查询已经测试通过，其他查询条件未测试
【已解决】查询表数据：各种运算符测试、各种数据类型测试、多条件（0表示and，1表示or）
【已解决】多主键删除测试
【已解决】多主键更新测试
【已解决】TableDataAppService删除数据接口，没有按主键批量删除，请修改代码实现
【已解决】更新表结构接口：将varchar字段类型改为bigint时报错，"ErrorMessage": "42804: column \"name22\" cannot be cast automatically to type bigint"，这张表中目前没有数据不应该报这个错误，需要排查原因并修复，考虑其他数据类型转换是否存在同样的问题
【已解决】如果表中有数据，在数据类型能安全转换的情况下应自动转换，比如数值型转成字符串，在数据类型能安全转换但是转换后会导致存储容量不足时会报错，比如bigint转换成int，decimal(18,2)转换成decimal(10,2)。【注：小数点四位改成小数点两位会造成小数点数据丢失，测试了下navicat也有同样问题，暂不处理】
【已解决】表结构创建和修改接口：需要详细测试不同的数据类型，添加、修改、删除字段，添加删除索引
【已解决】表结构：多主键
【已解决】表结构：创建表默认值，【创创建PostgreSql表结构，在参数中指定了默认值，但是接口创建表后发现默认值没有设置成功 ，请排查原因并修复，修改表结构接口也存在同样的问题】
【已解决】表结构：修改表设置默认值，删除默认值
【已解决】表结构：新增表结构和修改表结构接口，需要将执行过的SQL语句记录进log日志和审计日志中，目前添加表和修改表接口仅记录一条审计日志，需要改成执行一条SQL就记录一条审计日志，如果失败也应记录失败日志。
【已解决】表结构：不支持通过字段属性变更主键成员：id22？？？
【已解决】表结构修改接口，1.不支持修改主键字段的自增或 Identity 属性；2."MySQL 自增主键字段不支持在本接口中修改、删除或扩展，请使用专项迁移方案处理；3.MySQL 不支持在本接口中新增自增主键字段，请使用专项迁移方案处理。请问这三条能不能都改成支持？
【已解决】表模板Sql
【已解决】查询数据表 DDL，换行符统一为\n，sql.Replace("\r\n", "\n");
【已解决】数据表导出接口：根据条件导出数据，清理过期临时文件
【已解决】数据表导入接口：导入数据，清理过期临时文件，“\\N”表示null
【已解决】根据SQL导出表数据（SQL控制台）
【已解决】SQL控制台：执行SQL查询，返回的结果rows和ResultSets两份数据重复了，是不是应该去掉其中一份？
【已解决】DbAdmin.Service.SqlConsoleAppService.ExecuteAsync接口的返回结果，如下这几个字段返回值不准确
        "HasExactTotal": false,
        "IsTruncated": false,
        "HasNextPage": false,
        "HasPrevPage": false，这几个字段感觉没什么作用，可以去掉
【已解决】SqlConsoleAppService的预检查SQL接口，SQL语法错误仍返回成功，感觉不太合理，请问能否进行优化
【已解决】SQL控制台：返回多数据集
SQL控制台：返回值去掉不需要的字段。        "IsSafe": true,
        "IsSyntaxValidated": true,
        "IsDangerous": false,
        "BlockedKeywords": [],
        "IsMultiStatement": false,
        "IsCrossDatabase": false,
        "ErrorMessage": null
【已解决】实体表数据测试
【已解决】Mysql数据库测试 
【已解决】大数据量原子导入和部份成功导入测试：
    优化前：      MySql导入（AllOrNothing）：导入完成，SourceId=836365078479045, Table=t_db_test_student, ImportMode=InsertDataOnly, ImportPolicy=AllOrNothing, InsertedRows=1000000, SkippedRows=0, IssueCount=0, FileName=测试_大批量导入数据.xlsx, DurationMs=160609
          MySql导入（BestEffort）：导入完成，SourceId=836365078479045, Table=t_db_test_student, ImportMode=InsertDataOnly, ImportPolicy=BestEffort, InsertedRows=1000000, SkippedRows=0, IssueCount=0, FileName=测试_大批量导入数据.xlsx, DurationMs=155948
          PostgreSql导入（AllOrNothing）：导入数据表完成，SourceId=835213193781445, Table=t_db_test_student, ImportMode=InsertDataOnly, ImportPolicy=AllOrNothing, InsertedRows=1000000, SkippedRows=0, IssueCount=0, FileName=t_db_test_student.xlsx, DurationMs=161616
          PostgreSql导入（BestEffort）：导入数据表完成，SourceId=835213193781445, Table=t_db_test_student, ImportMode=InsertDataOnly, ImportPolicy=BestEffort, InsertedRows=999999, SkippedRows=1, IssueCount=1, FileName=t_db_test_student.xlsx, DurationMs=360469
          MySql：导出完成，SourceId=836365078479045, Table=t_db_test_student, FileType=Xlsx, ExportedRows=1000000, DurationMs=252411
          PostgreSql：表导出完成，SourceId=835213193781445, Table=t_db_test_student, FileType=Xlsx, ExportedRows=999999, DurationMs=219302
    优化后(csv导入)： PostgreSql（BestEffort）  导入数据表完成，SourceId=835213193781445, Table=t_db_test_student, ImportMode=InsertDataOnly, ImportPolicy=BestEffort, InsertedRows=1000000, SkippedRows=0, IssueCount=0, FileName=t_db_test_student.csv, (csv文件导入)DurationMs=79319【xlsx文件 DurationMs是225575】
    优化后(csv导入)： MySql（BestEffort）  导入数据表完成，SourceId=836365078479045, Table=t_db_test_student, ImportMode=InsertDataOnly, ImportPolicy=BestEffort, InsertedRows=1000000, SkippedRows=0, IssueCount=0, FileName=t_db_test_student - 副本.csv, DurationMs=97956【xlsx文件 DurationMs是157348】
    Navicat导入（XSLX文件）：100万行数据实测耗时约04:20.48，该时间不包含文件上传时间，【，[IMP] Processed: 1000000, Added: 1000000, Updated: 0, Deleted: 0, Errors: 0】
    Navicat导入（CSV文件）：100万行数据实测耗时约07:33.83，该时间不包含文件上传时间
    Navicat导出（CSV文件）：100万行数据实测耗时约03:37.77
    Navicat导出（XSLX文件）：100万行数据实测耗时约03:42.90

【已解决】导入功能需要记录批次日志

DbAdmin接口已测试完成，目前仅测试了Mysql库和PostgreSql库，其他库暂不支持；
大批量数据方面，造了100万行测试数据（8个字段），测试了两种库的导入导出，导入100d万数据，Mysql大概在160多秒左右，PostgreSql有时候160多秒有时候360多秒（CSV格式最快104秒）。导出100万数据，MySql和PostgreSql导出EXCEL需要4分钟左右。
代码里目前先限制导入导出最多只能200万行，导入文件限制200M，够吗？

数据库管理工具：
1.表数据导入功能已完成优化，优化后百万数据导入PostgreSql用时(xlsx文件225秒，csv文件79秒）, 百万数据导入Mysql用时（xlsx文件157秒，csv文件97秒）；通过Navacat将百万数据导入Mysql用时（xlsx文件260秒，csv文件秒）
2.目前表数据导出用时与Navicat差不多，暂不优化

## 在正式开始测试之前：
  处理GlobalUsing，单元测试->让AI只看接口定义设计单元测试，测试所有可能的应用场景
  检查关键步骤日志是否完善，关键步骤异常处理是否完完善，异常日志是否完善
  检查并完善注释，swagger分组及注释，英文注释改成中文注释
  


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
其他：创建数据库、生成表模板SQL、实体管理页面相关接口、结合前端功能页面排查缺少的接口并补全、临时文件清理
接口分类及代码结构调整，swagger注释，属性注释、log日志、error日志、审计日志、完善注释（无注释的方法，英文注释的方法，类的属性注释）
单元测试 
接口测试
接口文档整理：比如创建表、修改表接口需要详细说明，不同类型传如何传数据
前后端联调
功能测试
构造方言SQL源代码
进一步分析项目代码
异常错误提示内容优化

性能排查：查询、更新元数据或表数据相关SQL
风险排查：sql注入风险，sql控制台语句权限？其他风险
审计日志优化：记录所有SQL




AI自我优化，代码审查


ImportExportAppService的“导出数据表”接口，目前导出100万数据需要大概3到4分钟左右，请分析代码并提出优化建议
ImportExportAppService的“导入数据表”接口代码，目前导入100万数据需要大概3到6分钟左右，请分析代码并优化
D:\\code\\iotplatformv5\\02-应用模块\\16-DbAdmin是新开发的数据库管理工具模块的代码，目前接口开发已经基本完成，ImportExportAppService的“导入数据表”接口能否优化下性能，目前发现导入接口在PreflightAsync中从文件里读取了一遍数据，后面又再次从文件里读取一遍数据，这个地方应该可以优化，请分析并进行代码优化
【采用智能双模式：严格模式继续保证完整预检与零写入失败语义；流式模式面向大文件，在一次读取中完成校验和分批写入，并明确报告部分提交。接下来需要锁定流式模式对 identity 列的处理，因为这是现有预检最关键、也最影响数据正确性的全量依赖。
现在的全量导入模式的代码是先分批写入临时表，所以我认为全量导入也不需要预检，当中间或尾部数据导入失败时清空临时表即可
暂存表没有合并到目标表前，尾部失败不会污染目标表，因此 AllOrNothing 可以避免为数据格式错误做一次完整预检。现有实现仍依赖预检的原因不在事务，而在开始暂存前
  要确定 identity 写入策略和所有有效行是否使用同一列集合；我会核对暂存表的建表与合并细节，判断是否能改为安全的单次流式暂存。
  
  】
获取实体列表和详情，目前查的是数据库表为准，再加上实体表的标签，【旧逻辑也如此】
独立的添加字段、添加索引接口【暂时不需要】
 【已完成】刚才修改的方案过于复杂，我已经把你改的代码去掉了，目前的数据库管理工具比较轻量，暂时不需要索引类型字段，我发现创建索引入参中的索引类型也没有使用，请帮我去掉创建索引入参中的索引类型字段
 【已完成】由于目前已有的实体管理表只有数据源id和表名确定唯一，没有存储Database和Schema。所以该EntityManageAppService中的创建表、修改表、查询实体管理表详情、删除表这几个接口在请求时只需要传数据源和表名即可，请去掉Database和Schema并修改相关代码；由于DbTableTarget在其他地方还需要用，请不要直接修改该类，可以创建新的类替换现在的参数，建议创建一个新的类DbTableEntityTarget（类中只有SourceId和TableName）。
 【已完成】D:\code\iotplatformv5\02-应用模块\16-DbAdmin是新开发的数据库管理工具模块的代码，目前接口开发已经基本完成，但是发现EntityManageAppService和创建表和修改表结构接口的添加索引入参中指定的索引类型没用起来，你看下是不是可以优化？，要求即使用起来用户也可以不指定索引类型
 【已完成】D:\code\iotplatformv5\02-应用模块\16-DbAdmin是新开发的数据库管理工具模块的代码，目前接口开发已经基本完成，请仔细检查并补齐DbAdmin模块的单元测试
【重复项，不处理】RequireDatabaseTarget和RequireTableTarget校验代码，很多地方都有引用，可以封装
【已完成】 TableDataAppService中的GetPrimaryKeyColumnsAsync   GetAllowedColumnsAsync等方法是否可以抽取出来放到公共类中？让DbAdmin各服务或者组件类调用?如果可行，请先收集DbAdmin各接口类中可抽取到公共实现的方法，再统一处理。
【已完成】 D:\code\iotplatformv5\02-应用模块\16-DbAdmin是新开发的数据库管理工具模块的代码（目前还在开发阶段，前端还未接入），请仔细检查TableDataAppService中的查询方法还有没有可优化的地方，我发现拼接此方法生成的SQL分页查询和过滤条件的用的都是公共逻辑，都调用了DbAdmin.Infrastructure.Dialects.Common.DbTableQueryCommandBuilder.Build方法。本次请不要改代码，仅输出优化建议即可
【已完成】 请仔细检查TableDataAppService中的查询方法还有没有可优化的地方，我发现拼接此方法生成的SQL分页查询和过滤条件的用的都是公共逻辑，如何能保证各方言都能正常执行SQL？再参考上一轮对TableDataAppService中的添加方法生成SQL及参数的最新优化结果，仔细分析TableDataAppService的查询方法有没有可以优化的地方，请不要改代码，仅输出优化建议即可
【已完成】刚刚针对IDbTableDataDialect的三个实现类的的改造，【BuildInsertCommand、BuildUpdateCommand、BuildDeleteCommand】这几个方法在不同方言的实现类中代码是不是完全一致？我突然想到一个办法，就是为IDbTableDataDialect建立一个抽象基类（或者接口的默认方法），把这些在方言中完成一致的方法都放在这个基类（或接口）中，这样方言类就不需要各自重复实现一遍了，你帮我评估一下哪种方式更好？或者你有没有更好的实现方案？
【已完成】DbAdmin.Interface.Interface.DbDialect.IDbTableDataDialect中的BuildInsertCommand、BuildUpdateCommand、BuildDeleteCommand这三个方法不同方言的逻辑基本一致，BuildInsertSql、BuildUpdateSql、BuildDeleteSql在不同方言中也是调用同样的逻辑，感觉有些乱，有没有更好的实现方案？比如把这些逻辑都放到数据库方言各自的实现类中。
【已完成】D:\code\iotplatformv5\02-应用模块\16-DbAdmin是新开发的数据库管理工具模块的代码（目前还在开发阶段，前端还未接入），TableDataAppService中的接口，以添加接口为例，目前是通过BuildInsertSql和BuildInsertParameters分别生成SQL及参数，然后再传给_dbCommandExecutor.ExecuteNonQueryAsync统一执行，有没有更好的实现方式，比如把BuildInsertSql和BuildInsertParameters合为一步，直接把相关参数传给具体的方言数据库，让不同的方言数据库实现各自的逻辑。
  【已完成】DbAdmin中,TableDataAppService的功能，发现部份SQL没有写到具体的方言数据库中，请检查并优化代码
 【已完成】 DbAdmin中,TableDataAppService负责对表数据的增删改查操作，目前使用的是各方言数据库通过原生SQL实现，由于当前DbAdmin是比较轻量的数据库管理工具，从长远角度来看，是否将TableDataAppService的增删改查操作改成使用SqlSugar实现会更好？我的想法是将这些操作放在基类中，如果将来SqlSugar对新增的数据库不支持的时候仍然可以在方言类中覆盖基类实现。你帮我评估下这样做是否比目前已有的方案更好？【评估后：不建议】
  【已完成】D:\code\iotplatformv5\02-应用模块\16-DbAdmin是新开发的数据库管理工具模块的代码，请仔细评估DbAdmin这个模块的“数据库连接”是否能正常使用，是否存在性能BUG，是否与当前项目的其他模块的数据库  连接存在冲突等问题
   【完成】D:\code\iotplatformv5\02-应用模块\16-DbAdmin是新开发的数据库管理工具模块的代码，1.目前该模块操作数据接口接收请求体中的 `Target.Database`不能为空，需要改为可以为空，如果用户不传DataBase则用默认数据库，如果用户指定了DataBase则使用用户指定的DataBase；2.用户指定了DataBase后，当前模块底层切换数据库的功能需要你仔细检查下，如果不正常则请你进行修复。
   【已完成】D:\code\iotplatformv5\02-应用模块\16-DbAdmin是新开发的数据库管理工具模块的代码，该模块第一版代码已开发完成，接下来需要交给前端对接，等对接完成并测试通过后就会发布生产，请为该模块的核心功能补齐单元测试并验证功能是否正常，如有问题请修复
  【已完成】D:\code\iotplatformv5\02-应用模块\16-DbAdmin是新开发的数据库管理工具模块的代码，该模块第一版代码已开发完成，接下来需要交给前端对接，等对接完成并测试通过后就会发布生产，请仔细分析该模块代码在发布生产前还有哪些P0问题必须解决，其中接口权限控制暂时不用管，最终请输出需要解决的问题清单及解决方案，以markdown格式输出到当前目录。【注：目前已有的“DbAdmin 生产发布前 P0 问题清单”中的第4个问题我已经处理过，第1个、第2个和第3个问题目前暂时不需要处理】
  【】即使接口中传了数据库也没有用，只能修改数据源配置中的指定数据库，需要改掉这个限制
  【已完成】D:\code\iotplatformv5\02-应用模块\16-DbAdmin是新开发的数据库管理工具模块的代码，sqlConsoleAppServie中的ExecuteAsync接口，既保存了DbSqlHistory又保存了DbOperationLog，我觉得有些冗余，请去掉DbSqlHistory的保存功能，“分页查询 SQL 执行历史”接口也不需要了
  【不处理，先回退数据库切换功能，目前先支持现有数据源】DbAdmin模块的“数据库连接及数据源切换功能”，与当前项目其他模块的“数据库连接及数据源切换”是否会存在冲突？请仔细检查，如存在问题请输出问题原因及解决办法，以防止发布生产后出现重大BUG。
  【不处理】添加和修改表结构：可以去掉索引，单独增加“添加索引接口”和“删除索引接口”
  工程结构调整，特别是原生批量导入（代码分散，回滚没有日志，其他日志，注释）
  【已完成】检查并补充16-DbAdmin模块中接口的swagger注释，为16-DbAdmin中所有公共方法和核心私有方法加上简单明了的注释，如果现英文注释则都改成中文注释，补全16-DbAdmin模块中的异常日志。【
  D:\code\iotplatformv5\02-应用模块\16-DbAdmin是新开发的数据库管理工具模块的代码，为 16-DbAdmin 模块中所有对外接口（Controller 层、公开服务方法）补全 Swagger 文档注解（中文）。
为所有公共方法和“核心私有方法”（模块中关键逻辑的私有方法）添加简洁明了的中文 JavaDoc 注释，解释方法作用、关键参数与返回值。
将现有英文注释全部翻译为中文（保留原意，不改实现）。
补全异常日志：确保所有 catch 块或上层异常处理器在异常发生时记录足够上下文，包括方法名、关键输入参数（敏感信息需掩码/脱敏）、请求 id / 用户 id（若可得）、异常消息与堆栈（使用 logger.error(msg, e) 以保留堆栈）。遵循“不记录敏感信息、记录可复现调试线索”的原则。】
   发现DbAdmin.Infrastructure.Dialects中使用的都是部份类，有没有更好的不使用部份类的实现方式？
  【已完成】D:\code\iotplatformv5\02-应用模块\16-DbAdmin是新开发的数据库管理工具模块的代码，IDbDialect的具体实现类目前看起来比较臃肿，接口拆开了，但所有实现类和方法都堆一块了，请分析有无必要为具体方言实现类拆分，如可以拆分，则请提供技术方案，请考虑设计模式，本次仅要求生成技术方案文档
  【已完成】实体管理页面：数据源接口、查询列表接口、常用字段，实体类分页查询接口、实体类详情查询接口标签
  【已完成】创建数据库接口，帮我分析下是否有必要增加形如MySql的字符集和排序规则这样的入参字段，以及前端选项列表接口。
  【已完成】创建表、更新表、查询字段列表等接口的字段属性名称最好能和旧接口一致或者接近
  【放弃】EntityManageAppService数据库表设计：将创建表、修改表、删除表等无数据代码下沉到Infrastructure封装类中，以便未来复用？？？
  【已完成】ColumnDefinition类的precision是精度字段，应该是可以通过计算得出，所以添加表、修改表、添加字段、修改字段等接口入参使用的地方是否可以不需要这个字段？如果可以请去掉该字段并更新修改相关代码实现
  【已完成】重构EntityManageAppService的创建表、修改表、删除表接口代码，目前方法中多次调用了ExecuteDdlAsync，且ExecuteDdlAsync方法中存在审计日志，现要求将审计日志移到外面实现，要求创建表、修改表、删除表这三个接口每执行一次只需要一条审计日志即可，如果失败时要求说明失败原因以及部份成功的动作，请修改代码实现。
  【已完成】SchemaDesignAppService更名为EntityManageAppService，同时修改对应路由名称。
  【已完成】将MetadataAppService更名为SchemaDesignAppService服务，同时修改对应路由名称，为该服务添加添加一个“生成表模板SQL”接口，接口逻辑参照【老接口IotPlatform.DataWeaving.Servers.DbEntityManageService.GetData】进行实现。
  【处理中】1.添加数据库和删除数据库接口要求必须传数据源id，否岀报错；2.删除数据库接口不允话删除系统默认数据库且只能删除无业务表的数据库；3.创建数据库和删除数据库各自实现业务逻辑，不要抽象到ExecuteDaytaBaseDdlAsync方法中。04.为创建数据库和删除数据库添加审计日志，成功时在操作日志后追加审批日志。异常时打印错误日志并在错误日志后生成审计日志，审计日志可以抽象到一个方法。
  【已完成】D:\code\iotplatformv5\02-应用模块\16-DbAdmin是新开发的数据库管理工具模块的代码，在DbAdmin.Service中添加DataBaseManageService，为该服务中添加“创建数据库”、删除数据库接口，将MetadataAppService中的查询数据源中的数据库列表、查询数据库中的架构列表、查询数据库中的数据表列表接口迁移到此服务中。
  【已完成】数据库表结构详情查询接口：需要有标签字段，"tags": ["123label"]，默认“未分组；字段类型下拉选项列表与现有表单及数据必须兼容；
  【已完成】需要改造DbAdmin的“数据库表字段对象”和“字段的数据类型”兼容旧接口以便兼容旧的功能，也方便前端快速替换成新接口，目前老接口新建表和修改表用的字段对象（Common.Dto.DataBase.TableFieldOutput）和新接口（DbAdmin.Entity.Dto.Schema.ColumnDefinition）不一样，可用的字段类型也不一样，刚才看了下老接口创建表用的是SqlSuguar（IotPlatform.DataWeaving.Servers.DbEntityManageService.Create），老接口查询字段详情时会通过Common.Core.Manager.DataBase.IDataBaseManager.ViewDataTypeConversion统一转换后给前端，所以我希望新接口在创建和修改表时的字段类型列表要求定义枚举，枚举值从ViewDataTypeConversion转换的结果可明确为varchar、int、bigint、decimal、datetime、text、tinyint，也就是说新接口需要兼容这几个枚举值基础上可再扩展其他类型，对于接口字段对象的属性最好也能做到在兼容老接口字段的基础上再扩展其他属性（老接口TableFieldOutput，新接口对象ColumnDefinition），对于数据类型的输出也得参考Common.Core.Manager.DataBase.IDataBaseManager.ViewDataTypeConversion在DbAdmin中统一封装实现。由于字段列表改动涉及的地方比较多，比如创建表、修改表、创建字段、修改字段以及所有引用字段对象的地方都要涉及修改，请先评估并输出技术开发方案文档，文档以markdown格式输出到当前文件夹下。
  if (_sqlSugarClient.CurrentConnectionConfig.DbType.Equals(DbType.Oracle) ||
                        _sqlSugarClient.CurrentConnectionConfig.DbType.Equals(DbType.Kdbndp) ||
                        _sqlSugarClient.CurrentConnectionConfig.DbType.Equals(DbType.PostgreSQL))
                    {
                        throw Oops.Oh(ErrorCode.D1519);
                    }
  【已完成】D:\code\iotplatformv5\02-应用模块\16-DbAdmin是新开发的数据库管理工具模块的代码，用于代替旧的接口代码，目前在接口开发阶段，后续前端会逐步从老接口过渡到新接口。IotPlatform.DataWeaving.Servers.DbEntityManageService.DbEntityManagePage中的DbEntityManagePage接口和GetInfo接口功能需要迁移到DbAdmin中，需要新增一个Service用于管理这两个接口，迁移前后的代码不需要一致，最好是根据DbAdmin模块重写代码，接口输入参数和输出参数需要在DbAdmin中重新定义，代码核心逻辑最好是能封装在DbAdmin.Service.Internal.DbEntityManageSyncService中（因为都是实体管理功能，都是从老代码迁移过来），如果有必要可以将DbEntityManageSyncService类下沉到DbAdmin.Infrastruture中并重命名一个更合理的名称。
  【已完成】查询、导出、控制台查询分页功能要求限制MaxPageSize，如果合适的话请定义在DbAdmin.Entity的Constant中
  【已完成】近期加的很多单元测试项目和文件都放在什么位置？接下来还需要给DbAdmin添加单元测试，未来需要给其他模块也添加单元测试，请提供建议，在当前解决方案中应该将单元测试放在哪里比较合适？
  【已完成】 SqlConsoleAppService控制台相关接口，不需要安全校验，所有语句都可执行，在将来的版本中再加安全和权限校验，当前最重要的是尽快发布第一个版本，请考虑实际情况进行代码改造和优化，以让SqlConsoleAppService满足上线标准。
 【已完成】 导出功能的临时文件存储位置改成和导入接口一样，目录名称统一用DbAdminTemp(Path.Combine(FileVariable.TemporaryFilePath, "DbAdminTemp"))，导出接口中添加文件清理功能（删除超过24小时的临时文件），SqlConsoleAppService控制台数据导出也参照此方案优化。
   【已完成】D:\code\iotplatformv5\02-应用模块\16-DbAdmin是数据库管理工具模块的代码，目前在接口开发阶段，前端还未接入，16-DbAdmin模块中DbAdmin.Service下面，DbAdmin.Service.ImportExportAppServiceImport导入接口目前只支持50M，太小了，需要支持更大文件导入，比如200M，请优化代码，是否先上传文件到临时文件夹再导入比较合适？如果你有有更好的方案则按你的方案进行改造。请注意志临时文件名称唯一性及临时文件清理，【尽量复用目前系统中已有的文件上传功能，参照Common.Core.Manager.Files.IFileManager】,临时文件清理时间同导入接口（24小时），
   【已完成】DbAdmin.Service.ImportExportAppService.Export这个导出接口需要支持百万数据，请优化代码，是否先将数据导出到临时文件夹再将文件返回前端比较合适？如果你有有更好的方案则按你的方案进行改造。请注意临时文件名称唯一性及临时文件清理，【尽量复用目前系统中已有的文件下载功能，参照Common.Core.Manager.Files.IFileManager】，【注：FileManager类中现有的方法不需要“取消令牌”，请勿添加取消令牌参数】
  【已完成】D:\code\iotplatformv5\02-应用模块\16-DbAdmin是数据库管理工具模块的代码，目前在接口开发阶段，前端还未接入，16-DbAdmin模块中DbAdmin.Service下面，DbAdmin.Service.ImportExportAppService.Import是数据导入接口，目前该导入接口的“AllOrNothing全量原子导入策略是否存在性能问题？如果有优化空间请进行代码优化，本次优化后该接口将会发布到生产环境使用。
  【已完成】导入接口代码，导入策略是AllOrNothing时确保全量原子导入是可以的，但是策略为BestEffort时调用的InsertStreamingRowsAsync方法中为什么还有全量事务管理？请确认代码是否正确，BestEffort这个策略是允许部份成功的，如有问题请进行相关代码改造
  【已完成】导入接口，如果导入策略是AllOrNothing但是计算出的useStaging为false时直接抛出异常说明不支持的原因，移除【InsertStreamingRowsAsync中所有跨批次事务会话、提交、回滚和释放代码。
  【已完成】请实现按数据库原生批量导入：SQL Server SqlBulkCopy、PostgreSQL COPY、MySQL MySqlBulkCopy，目前使用的事务也根据你的想法进行优化，如果考虑性能的话是是不是把全量导入功能去掉比较合适？
  【已完成】导入功能，目前的数据库原生批量导入，可能产生部份成功部份失败，适合现在导入策略ImportPolicyEnum.BestEffort。现在需要改造接口，如果用传入的导入策略是AllOrNothing时，要求导入的数据要么全部成功要么全部失败，不允许出现部份成功的情况，如果能实现则请改造代码。
  【已完成】BuildInsertBatchCommand生成的SQL是否合理？是否有更好的方案？
  【已完成】D:\code\iotplatformv5\02-应用模块\16-DbAdmin是数据库管理工具模块的代码，目前在接口开发阶段，前端还未接入，16-DbAdmin模块中DbAdmin.Service下面，SchemaDesignAppService、MetadataAppService、SqlConsoleAppService、【已完成】TableDataAppService、ImportExportAppService这几个Service是核心接口。目前发现各服务之间有些代码可以复用但目前没有复用，请根据以下要求项目代码综合考虑扩展性、健壮性、长期可维护性对代码进行优化，适当的时候可以使用设计模式提升代码质量，也可将通用逻辑下沉到DbAdmin.Infrastructure中，具体如何实施请你根据综合情况择优处置：1.获取表字段、主键、索引、等功能，如能复用请尽量复用，但不能复用时不要勉强。2.“TableDataAppService中的表数据查询”和“ImportExportAppService中的表数据导出”的分页数据查询及排序等逻辑，如能复用请尽量复用，但不能复用时不要勉强，目前的表数据表导出功能无法根据指定条件导出数据（你可以帮我补上这个功能，最好能和查询表数据的条件逻辑共用）。3.SqlConsoleAppService中的控制台查询数据功能，对查询结果进行分页展示和导出的逻辑如果能复用“表数据查询”则可复用，但不能复用时请勿勉强。【补充：考虑实际情况复用代码逻辑，择优处置即可】
  排查可复用代码逻辑，如存在则尽量复用。【补充：考虑实际情况复用代码逻辑，择优处置即可】
  【已完成】修改接口路由,去掉api
  【已完成】表数据导入接口，是否能正常导入所有数据类型。表数据导出接口是否能正常导出所有数据类型，二进制字段如何处理？【新增统一值编解码服务，按数据库引擎和字段类型处理文本、JSON、布尔、整数、数值、日期时间、Guid 等常用类型。不支持的类型（包括二进制）导入时返回类型不支持问题。】
  【已完成】导出数据接口，不支持的类型，不要直接报错，请直接导出长度字节，形如二进制一样输出【[二进制数据，{length} 字节]】
  DbAdmin.Service.ImportExportAppService中的表数据导入和导出接口，是否存在性能问题，如有则请进行优化
  【已完成】导入接口，IsIdentityLikeColumn是否有必要下沉到数据库方言IDbDialect中实现？ExecuteInsertBatchAsync中switch判断和底层数据库SQL是否有必要下沉到数据库方言中实现？如有必要请优化代码实现，整体排查下导入接口是否还在存类似的情况应该都要优化，根据DbEngineType判断实现不同逻辑的地方应该要下沉到数据库方言中实现，请先分析可行性再修改代码。
  【已完成】保留“预检一次、事务写入再读取一次”的流程：该设计保证 AllOrNothing 在数据库写入前完成校验，并能正确决定 identity 列处理方式；不为了减少一次读取而引入长事务或牺牲回滚安全性。
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
  【已完成】在SchemaDesignAppService中新增一个实体类列表分页查询接口，具体查询逻辑在DbEntityManageSyncService中实现（该类需要增加实体类列表分页查询接口（参数：表名、configId），同时请看下是否需要取一个更合适的类名称），可参照IotPlatform.DataWeaving.Servers.DbEntityManageService.DbEntityManagePage中的方法实现
  【已完成】在SchemaDesignAppService中新增一个实体类详情查询接口，具体查询逻辑在DbEntityManageSyncService中实现，可参照IotPlatform.DataWeaving.Servers.DbEntityManageService.GetInfo实现。
  【已完成】分析MetadataAppService中的接口代码，判断是否已经达到生产环境可用的标准，如有优化空间或更好的解决方案，请帮我优化相关代码逻辑

  


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

中风险：每请求创建 SqlSugarClient 有对象初始化成本

  当前实现：

  D:/code/iotplatformv5/02-应用模块/16-DbAdmin/DbAdmin.Infrastructure/Connections/DbConnectionContextFactory.cs:46

  这不会导致每次都新建物理 TCP 连接，因为底层 ADO.NET 通常按连接串复用连接池。但每次请求仍会产生：

  - SqlSugarClient 和内部上下文对象分配。
  - 连接串解析和配置初始化。
  - 密码解密和连接串重建。
  - 客户端释放开销。

  对于数据库管理工具这种低频、人工操作型模块，通常可以接受；如果存在大量分页查询或 SQL 批量操作，则建议缓存“数据源 + 目标数据库”对应的连接配置/连接串，至少避免每次密码解密和连接串解析。


3. 根据实际 QPS 决定是否缓存连接串/连接配置；普通管理操作暂时不需要引入复杂的单例 Scope 注册表。
  4. 增加并发验证：同时请求默认库、数据库 A、数据库 B，确认查询结果和客户端连接串始终对应各自目标。【是否存在这个问题？】
     1. 严重：SQL 控制台必然空引用，无法执行 SQL。
     /D:/code/iotplatformv5/02-应用模块/16-DbAdmin/DbAdmin.Service/SqlConsoleAppService.cs:45 声明了 _connectionOptions，但构造函数没有注入或赋值；ExecuteAsync 会直接访问其
     Value.EnableConsoleSql。构建已产生 CS0649 和 CS8618 警告。/db-admin/sql/execute 会失败。

  2. 高：PostgreSQL 驱动版本在宿主中不一致。
     DbAdmin 固定引用 /D:/code/iotplatformv5/02-应用模块/16-DbAdmin/DbAdmin.Infrastructure/DbAdmin.Infrastructure.csproj:18，但宿主依赖链同时引入 /D:/code/iotplatformv5/02-应用模块/03-
     BusApp/IotPlatform.Application/IotPlatform.Application.csproj:40，最终 IotPlatform 输出的是 Npgsql 9.0.3。这可能导致 PostgreSQL/Kingbase 的 SqlSugar 与原生批量导入代码在运行时出现 API
     或行为不兼容，应统一版本并做真实连接、查询、导入回归测试。

  3. 中：DbAdmin 自建的 dbadmin:source:{id} 缓存无法被现有数据连接管理服务失效。
     连接工厂缓存数据源 5 分钟，/D:/code/iotplatformv5/02-应用模块/16-DbAdmin/DbAdmin.Infrastructure/Connections/DbConnectionContextFactory.cs:64；旧模块更新连接时只清理
     CacheConst.KeyDbLink，/D:/code/iotplatformv5/02-应用模块/02-System/Systems.Core/System/DbLink/DbLinkService.cs:273。更新密码、地址、数据库名或删除连接后，DbAdmin 最多 5 分钟仍会使用旧
     凭据或已删除的数据源。

  4. 中：查询行数限制发生在结果完全加载后。
     /D:/code/iotplatformv5/02-应用模块/16-DbAdmin/DbAdmin.Infrastructure/Execution/SqlSugarDbCommandExecutor.cs:122 先 GetDataTableAsync，随后才判断最大行数。面对无分页的大表元数据或误用
     接口，内存、网络和数据库负载已发生，MaxResultRows 不能形成有效保护。

     DbAdmin 操作的是任意用户表，没有静态实体。SqlSugar 动态 Dictionary<string, object?> 的插入/更新需要处理表别名、主键条件、空值、枚举/JSON/二进制/日期类型、标识列等，实际会形成另一套更难验证的适配逻辑。


