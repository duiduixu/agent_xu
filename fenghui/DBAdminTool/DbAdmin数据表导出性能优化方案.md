
【注：当前文档应该没有考虑通过“带条件的SQL语句”的导出，请考虑带条件时流式导出】

【resume-session：DbAdmin数据表导出性能优化方案】

当前 100 万行导出耗时 3～4 分钟，首要原因不是文件写入，而是数据库读取链路采用了“小页分页 + DataTable 中转”。

  核心瓶颈

  1. 分页大小只有 1000 行

  /D:/code/iotplatformv5/02-应用模块/16-DbAdmin/DbAdmin.Service/ImportExportAppService.cs:34 使用 DbAdminPagingConstants.MaxPageSize，当前值为 1000。

  100 万行至少需要约 1000 次数据库查询，每次查询都包含：

  - 创建 DbCommand
  - 创建 DbDataReader
  - 读取并填充 DataTable
  - 创建大量 DataRow
  - 释放 DataTable

  相关代码：/D:/code/iotplatformv5/02-应用模块/16-DbAdmin/DbAdmin.Service/ImportExportAppService.cs:351

  2. 分页 SQL 在后续页性能会持续下降

  SQL Server 使用：

  ROW_NUMBER() OVER (...)

  MySQL 使用：

  LIMIT @size OFFSET @offset

  相关代码：

  - /D:/code/iotplatformv5/02-应用模块/16-DbAdmin/DbAdmin.Infrastructure/Dialects/SqlServer/SqlServerPagingDialect.cs:31
  - /D:/code/iotplatformv5/02-应用模块/16-DbAdmin/DbAdmin.Infrastructure/Dialects/MySql/MySqlPagingDialect.cs:31

  第 1000 页通常需要数据库先扫描、排序或跳过前 999000 行，再返回最后 1000 行。数据量越大，后续页越慢。

  3. 每页通过 DataTable 进行二次转换

  /D:/code/iotplatformv5/02-应用模块/16-DbAdmin/DbAdmin.Infrastructure/Execution/SqlSugarDbCommandExecutor.cs:108 开启 Reader 后，将每一行写入 DataTable/DataRow。导出逻辑随后再次从 DataRow
  读取字段。

  这会产生较多对象分配和 GC 压力，尤其是 100 万行、几十列的场景。

  4. CSV 每行多次字符串和数组分配

  /D:/code/iotplatformv5/02-应用模块/16-DbAdmin/DbAdmin.Service/ImportExportAppService.cs:434 每行都会：

  - 创建 string[]
  - 对每个单元格调用 FormatExport
  - 创建转义字符串
  - 调用 string.Join
  - 调用一次 WriteLineAsync

  另外，FormatExport 内部每个单元格都会重新判断字段类型，类型信息可以提前缓存。

  5. Excel 导出额外使用 Dictionary

  /D:/code/iotplatformv5/02-应用模块/16-DbAdmin/DbAdmin.Service/ImportExportAppService.cs:411 每行创建一个 Dictionary<string, object?>，这对 XLSX 导出会增加明显 CPU 和内存开销。

  ———

  ## 优先级最高的优化建议

  ### 一、增加真正的流式导出接口

  建议在 IDbCommandExecutor 增加类似接口：

  IAsyncEnumerable<DbDataReaderRow> ExecuteReaderAsync(...)

  或者提供回调式接口：

  Task ExecuteReaderAsync(
      ...,
      Func<DbDataReader, CancellationToken, ValueTask> onRowAsync,
      CancellationToken cancellationToken);

  导出时直接：

  DbDataReader.ReadAsync
      -> 读取字段
      -> 格式化
      -> 写入 CSV/XLSX

  这样可以去掉：

  - 分页查询
  - DataTable
  - DataRow
  - 每页对象集合
  - 1000 次数据库往返

  这是收益最大的改造，通常比单纯调大分页大小更有效。

  需要注意：当前导出支持最大 200 万行，因此 DbExecutionOptions.MaxResultRows 不能继续使用默认的 1000 限制，应该为导出场景提供专用读取选项或直接绕过 DataTable 限制。

  ### 二、CSV 优先采用单连接、单 Reader 连续读取

  CSV 不需要分页，也不需要知道总行数。建议：

  1. 构造一次查询 SQL。
  2. 打开一次连接。
  3. 执行一次 ExecuteReaderAsync(CommandBehavior.SequentialAccess).
  4. 使用 ReadAsync 持续写入文件。
  5. 通过 MaxExportRows 控制最大行数。

  这会将约 1000 次查询降低为 1 次查询，通常是最直接的性能提升方案。

  ### 三、分页场景改为 Keyset Pagination

  如果因为 Excel、断点续传或兼容性原因仍然需要分页，不建议继续使用页码分页。

  当前已经强制要求稳定排序，并且会优先追加主键作为 TieBreaker，这为 Keyset Pagination 提供了基础。

  例如按主键升序：

  WHERE Id > @lastId
  ORDER BY Id
  LIMIT @pageSize

  复合排序需要生成：

  WHERE (SortColumn > @lastValue)
     OR (SortColumn = @lastValue AND Id > @lastId)

  建议扩展 IDbPagingDialect，增加：

  BuildKeysetQuery(...)

  并在导出过程中保存上一页最后一行的排序键，而不是传递 pageIndex。

  ### 四、短期先提高导出专用批量大小

  如果暂时不改流式 Reader，至少应将导出页大小从 1000 提高到 5000～20000，并与普通查询分页限制解耦。

  当前导出错误地复用了：

  DbAdminPagingConstants.MaxPageSize = 1000

  建议单独定义：

  private const int ExportPageSize = 10_000;

  并增加配置项，根据数据库和服务器内存进行调整。

  但这只是短期优化，无法解决 OFFSET/ROW_NUMBER 后续页越来越慢的问题。

  ### 五、缓存导出字段的格式化信息

  在导出开始时，为每个字段预计算：

  - 字段序号
  - DbTableValueKind
  - 日期格式
  - 是否二进制
  - 是否需要 CSV 转义

  当前 FormatExport 每个单元格都会重复执行类型判断。可以将：

  ExportField

  扩展为包含缓存后的格式化信息，减少每行每列的重复判断。

  CSV 还可以改为使用一个可复用的 StringBuilder，避免每行创建 string[] 和大量中间字符串。

  ### 六、优化 XLSX 导出策略

  当前 XLSX 每行创建 Dictionary，建议确认 MiniExcel 是否支持基于数组或对象序列的写入方式。如果支持，应优先使用：

  object?[] / string?[]

  而不是：

  Dictionary<string, object?>

  如果业务允许，100 万行建议默认导出 CSV，Excel 仅用于较小数据量。XLSX 本身有工作表最大行数限制，且 XML 压缩、共享字符串和单元格结构会带来额外 CPU 消耗。

  ### 七、增加导出阶段耗时指标

  目前只记录总耗时：

  /D:/code/iotplatformv5/02-应用模块/16-DbAdmin/DbAdmin.Service/ImportExportAppService.cs:202

  建议拆分记录：

  - 建立连接耗时
  - 数据库读取耗时
  - 格式化耗时
  - 文件写入耗时
  - 每页耗时
  - 实际读取行数
  - 每秒处理行数
  - 文件大小
  - GC 或进程内存变化

  这样可以确认瓶颈究竟位于数据库排序、网络传输、DataTable 构造还是 CSV/XLSX 序列化。

  ———

  ## 建议实施顺序

  ### 第一阶段：低风险优化

  - 导出页大小独立配置，先提升到 5000～10000。
  - 缓存字段类型和字段序号。
  - CSV 使用复用的 StringBuilder。
  - XLSX 尽量改为数组行，减少 Dictionary。
  - 增加分页、数据库、写文件阶段耗时日志。

  ### 第二阶段：主要性能改造

  - 为导出增加 DbDataReader 流式读取接口。
  - CSV 改为单查询、单连接、持续读取。
  - Excel 也基于流式行枚举生成。
  - 保留 DataTable 分页接口给普通表格查询，避免影响现有功能。

  ### 第三阶段：大数据和复杂排序优化

  - 为各数据库方言实现 Keyset Pagination。
  - 对无合适索引的排序字段给出提示或限制。
  - 对导出任务改为后台任务模式，接口立即返回任务编号，前端轮询进度并下载文件。

  ———

  ## 还需注意的问题

  测试代码与当前实现存在不一致：

  /D:/code/iotplatformv5/04-test/DbAdmin.UnitTests/ImportExport/ImportLargeFileLimitTests.cs:78 期望 MaxImportRows = 1_000_000，但当前实现为 2_000_000。这与导出性能无直接关系，但说明相关容
  量限制测试可能已经过期，应在后续调整时同步修正。

  综合判断，最值得优先实施的是“导出专用 DbDataReader 流式读取 + 单次查询”。仅把分页大小从 1000 调到 10000，可以减少往返次数，但无法根治大页码 OFFSET/ROW_NUMBER 的扫描成本；流式读取和
  Keyset Pagination 才能从根本上改善百万级导出性能。