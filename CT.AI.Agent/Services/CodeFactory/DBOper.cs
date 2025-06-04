using CodeTool.BaseClass.CodeFactory;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace CT.AI.Agent.Services.CodeFactory;

public class DBOper : DbContext
{
    protected DbContext GetDbContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<DbContext>().UseSqlServer(connectionString).Options;
        return new DbContext(options);
    }
    public IEnumerable<TS> GetSysDataBases<TS>(IServer svr) where TS : IServer, new()
    {
        try
        {
            string configStr = svr.ConfigStr;
            string sqlstr = $" select '{svr.ServerName}' ServerName, name DbName,'{svr.UserName}' UserName,'{svr.Pwd}' Pwd from sysdatabases where dbid >=6 order by name ";
            var context = GetDbContext(configStr);
            return context.Database.SqlQueryRaw<TS>(sqlstr);
        }
        catch (Exception ex)
        {
            throw ex;
        }
    }
    public IEnumerable<TT> GetAllTable<TT>(IServer svr, string where = "") where TT : ITable, new()
    {
        try
        {
            string wherename = "";
            if (!string.IsNullOrEmpty(where)) wherename = " and d.name = '" + where + "'";
            string configStr = svr.ConfigStr;
            string sqlstr = @" 
select 0 [Index], d.name TableName,isnull(f.value,'') TableTitle
from sys.sysobjects AS d
left join sys.extended_properties f on d.id=f.major_id and f.minor_id=0
where d.xtype='U' and d.name<>'dtproperties' and d.name<>'sysdiagrams'
" + wherename + @" ORDER BY d.name ";
            var context = GetDbContext(configStr);
            var tlist = context.Database.SqlQueryRaw<TT>(sqlstr);
            int i = 0;
            List<TT> mlist = new();
            foreach (TT item in tlist)
            {
                item.Index = i + 1;
                item.Svr = svr;
                mlist.Add(item);
                i++;
            }
            return mlist;
        }
        catch (Exception ex)
        {
            throw ex;
        }
    }
    public DataTable GetTableContent(ITable tbl)
    {
        string configStr = tbl.ConfigStr;
        string sqlstr = " SELECT top 1000 * FROM " + tbl.TableName;
        var context = GetDbContext(configStr);
        var dt = new DataTable();
        var conn = context.Database.GetDbConnection();
        var command = conn.CreateCommand();
        command.CommandText = sqlstr;
        conn.Open();
        using var reader = command.ExecuteReader();
        dt.Load(reader); // 将 IDataReader 转换为 DataTable
        conn.Close();
        return dt;
    }
}
