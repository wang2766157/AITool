using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CodeTool.BaseClass.CodeFactory;

#region Sql
[Serializable]
public class ServerModel : IServer
{
    public string ServerName { get; set; }
    [Key]
    public string DbName { get; set; }
    public string UserName { get; set; }
    public string Pwd { get; set; }
    public string ConfigStr { get { return SettingConfigStr(); } }
    public IServer Clone()
    {
        IServer res = new ServerModel
        {
            ServerName = ServerName,
            DbName = DbName,
            UserName = UserName,
            Pwd = Pwd,
        };
        return res;
    }
    #region 获取链接字符串
    /// <summary>
    /// 获取链接字符串
    /// </summary>
    /// <returns></returns>
    private string SettingConfigStr()
    {
        string res = "";
        string dbname = "master";
        if (!string.IsNullOrEmpty(DbName))
            dbname = DbName; 
        if (!string.IsNullOrEmpty(UserName) && !string.IsNullOrEmpty(Pwd))       
            res = "Uid=" + UserName + ";Pwd=" + Pwd + ";Database=" + dbname + ";Server=" + ServerName + ";TrustServerCertificate=True;";     
        else    
            res = "Server=" + ServerName + ";Integrated Security=SSPI;Database=" + dbname + ";TrustServerCertificate=True;";   
        return res;
    }
    #endregion
}
[Serializable]
public class TableModel : ITable
{
    [Key]
    public int Index { get; set; }
    public string TableName { get; set; }
    public string TableTitle { get; set; }
    [NotMapped]
    public IServer Svr { get; set; }
    public string ConfigStr { get { return SettingConfigStr(); } }
    [NotMapped]
    public bool IsMainTable { get; set; } = false;
    [NotMapped]
    public string OwnMainTable { get; set; }
    #region 获取链接字符串
    private string SettingConfigStr()
    {
        if (Svr != null && !string.IsNullOrEmpty(Svr.DbName))
            return Svr.ConfigStr;
        else
            return "";
    }
    #endregion
}
[Serializable]
public class ColModel : ICol
{
    [Key]
    public int Index { get; set; }
    public string ColName { get; set; }
    public string ColTitle { get; set; }
    public string ColumnType { get; set; }
    public bool PrimaryKey { get; set; }
    public bool AutoIncreaseColumn { get; set; }
    public string DefaultColumn { get; set; }
    public bool NotNull { get; set; }
    public int Lens { get; set; }
    public string CtlName { get; set; }
    public string CtlType { get; set; } = CtlEnum.Default.ToString();
    public string ListType { get; set; } = ListEnum.Default.ToString();
    public bool Required { get; set; } = false;
    public bool IsHide { get; set; }
}
[Serializable]
public class ProcModel : IProc
{
    [Key]
    public int Index { get; set; }
    public string ProcName { get; set; }
    public IServer Svr { get; set; }
    public string ConfigStr { get { return SettingConfigStr(); } }
    #region 获取链接字符串
    private string SettingConfigStr()
    {
        if (Svr != null && !string.IsNullOrEmpty(Svr.DbName))
            return Svr.ConfigStr;
        else
            return "";
    }
    #endregion
}
[Serializable]
public class ProcParamModel : IProcParam
{
    public int Index { get; set; }
    public string ParamName { get; set; }
    public string ColumnType { get; set; }
    public int ParamLength { get; set; }
}
#endregion