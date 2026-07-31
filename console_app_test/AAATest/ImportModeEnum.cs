/// <summary>
/// 导入模式。
/// </summary>
public enum ImportModeEnum
{
    /// <summary>
    /// 重建表并导入.
    /// </summary>
    RebuildTableAndImport = 1,
    /// <summary>
    /// 清空表后导入.
    /// </summary>
    TruncateAndImport = 2,
    /// <summary>
    /// 仅创建表.
    /// </summary>
    CreateTableOnly = 3,
    /// <summary>
    /// 仅插入数据.
    /// </summary>
    InsertDataOnly = 4
}
