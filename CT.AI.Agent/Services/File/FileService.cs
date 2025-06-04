using System.Text.Json;
using WrtStoreNs;

namespace CT.AI.Agent.Services.File;

public class FileService : BaseService, IWrtBuffer
{
    #region 成员
    public string RootPath { get { return Environment.CurrentDirectory; } }
    public string FileName { get { return "TempData"; } }
    public List<TempModel> CoreList = new List<TempModel>();
    #endregion
    #region 读写临时文件
    public void WriteInfo(object obj)
    {
        if (obj == null) return;
        string fullname = RootPath + FileName + WrtStore.Extension;
        WrtStore.WriteInfo(obj, fullname);
    }
    public object ReadInfo()
    {
        string fullname = RootPath + FileName + WrtStore.Extension;
        var obj = WrtStore.ReadInfo(fullname);
        var res = new List<TempModel>();
        if (obj != null)
        {
            var tt = (JsonElement)obj;
            res = JsonSerializer.Deserialize<List<TempModel>>(tt);
            CoreList = res;
        }
        return res;
    }
    #endregion
    //
    public Task<TempModel> ReadTempData(string key)
    {
        string fullname = RootPath + "/" + FileName + WrtStore.Extension;
        var obj = WrtStore.ReadInfo(fullname);
        var res = new List<TempModel>();
        if (obj != null)
        {
            var tt = (JsonElement)obj;
            res = JsonSerializer.Deserialize<List<TempModel>>(tt);
            CoreList = res;
        }
        var mlist = CoreList.Where(x => x.PageInfo == key).ToList();
        if (mlist.Count > 0)
        {
            return Task.FromResult(mlist[0]);
        }
        return Task.FromResult(new TempModel());
    }
    public void SaveTempData(TempModel tm)
    {
        string fullname = RootPath + "/" + FileName + WrtStore.Extension;
        var obj = WrtStore.ReadInfo(fullname);
        var res = new List<TempModel>();
        if (obj != null)
        {
            var tt = (JsonElement)obj;
            res = JsonSerializer.Deserialize<List<TempModel>>(tt);
            CoreList = res;
        }
        CoreList.Add(tm);
        CoreList = DistinctModelList(CoreList);
        WrtStore.WriteInfo(CoreList, RootPath + "/" + FileName + WrtStore.Extension);
    }
    public List<TempModel> DistinctModelList(List<TempModel> slist)
    {
        if (slist == null) slist = new List<TempModel>();
        slist = slist.GroupBy(x => new { x.PageInfo }).Select(x => x.Last()).ToList();
        return slist;
    }
}
#region WrtModel
[Serializable]
public class TempModel : IWrtModel
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string PageInfo { get; set; }
    public string Tags { get; set; }//必须要给对象类型
}
#endregion

