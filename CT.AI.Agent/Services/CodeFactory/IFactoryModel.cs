namespace CodeTool.BaseClass.CodeFactory;

/// <summary>
/// 服务器接口
/// </summary>
public interface IServer
{
    /// <summary>
    /// 服务器地址
    /// </summary>
    string ServerName { get; set; }
    /// <summary>
    /// 数据库名
    /// </summary>
    string DbName { get; set; }
    /// <summary>
    /// 登录用户
    /// </summary>
    string UserName { get; set; }
    /// <summary>
    /// 密码
    /// </summary>
    string Pwd { get; set; }
    /// <summary>
    /// 合成连接字符串
    /// </summary>
    string ConfigStr { get; }
    IServer Clone();
}
/// <summary>
/// 表接口
/// </summary>
public interface ITable
{
    /// <summary>
    /// 索引
    /// </summary>
    int Index { get; set; }
    /// <summary>
    /// 表名
    /// </summary>
    string TableName { get; set; }
    /// <summary>
    /// 表说明
    /// </summary>
    string TableTitle { get; set; }
    /// <summary>
    /// 所属服务器
    /// </summary>
    IServer Svr { get; set; }
    /// <summary>
    /// 合成连接字符串
    /// </summary>
    string ConfigStr { get; }
    /// <summary>
    /// 是否为业务主表
    /// </summary>
    bool IsMainTable { get; set; }
}
/// <summary>
/// 列接口
/// </summary>
public interface ICol
{
    /// <summary>
    /// 索引
    /// </summary>
    int Index { get; set; }
    /// <summary>
    /// 列名
    /// </summary>
    string ColName { get; set; }
    /// <summary>
    /// 列描述
    /// </summary>
    string ColTitle { get; set; }
    /// <summary>
    /// 数据库类型
    /// </summary>
    string ColumnType { get; set; }
    /// <summary>
    /// 是否主键
    /// </summary>
    bool PrimaryKey { get; set; }
    /// <summary>
    /// 是否自增列
    /// </summary>
    bool AutoIncreaseColumn { get; set; }
    /// <summary>
    /// 默认值
    /// </summary>
    string DefaultColumn { get; set; }
    /// <summary>
    /// 允许为空
    /// </summary>
    bool NotNull { get; set; }
    int Lens { get; set; }

    /// <summary>
    /// 控件名
    /// </summary>
    string CtlName { get; set; }
    /// <summary>
    /// 编辑页控件类型
    /// </summary>
    string CtlType { get; set; }
    /// <summary>
    /// 列表类型
    /// </summary>
    string ListType { get; set; }
    /// <summary>
    /// 画面必填验证
    /// </summary>
    bool Required { get; set; }
    /// <summary>
    /// 画面隐藏
    /// </summary>
    bool IsHide { get; set; }
}
public interface IProc
{
    int Index { get; set; }
    string ProcName { get; set; }
    IServer Svr { get; set; }
    string ConfigStr { get; }
}
public interface IProcParam
{
    int Index { get; set; }
    string ParamName { get; set; }
    string ColumnType { get; set; }
    int ParamLength { get; set; }
}
/// <summary>
/// 控件类型
/// </summary>
public enum CtlEnum
{
    Default,
    TextBoxInput,
    DatePickerInput,
    DropdownInput,
    SelectBoxInput,
}
/// <summary>
/// 列表中的类型
/// </summary>
public enum ListEnum
{
    Default,
    StringCol,
    DateCol,
    DropdownCol,
    NumberCol
}
