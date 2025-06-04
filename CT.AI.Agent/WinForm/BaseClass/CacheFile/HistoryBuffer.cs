using WrtStoreNs;

namespace HistoryBufferNs;

public class HistoryBuffer : IWrtBuffer
{
    #region 成员
    public string FileName { get { return "tmp"; } }
    public string RootPath { get { return AppDomain.CurrentDomain.BaseDirectory; } }
    public List<BufferModel> CoreList = new List<BufferModel>();
    #endregion
    #region 添加查找临时数据
    public List<BufferModel> AddModelList(List<BufferModel> slist, TextBox tb)
    {
        if (slist == null)
            slist = new List<BufferModel>();
        BufferModel bm = new BufferModel
        {
            Id = Guid.NewGuid(),
            BaseClassInfo = tb.FindForm().Name,
            ControlInfo = tb.Name,
            Tidings = tb.Text,
            Tags = tb.Tag,
            NowTime = DateTime.Now,
        };
        slist.Add(bm);
        return slist;
    }
    public List<BufferModel> AddModelList(List<BufferModel> slist, TextBox[] tbs)
    {
        if (slist == null)
            slist = new List<BufferModel>();
        for (int i = 0; i < tbs.Length; i++)
        {
            BufferModel bm = new BufferModel
            {
                Id = Guid.NewGuid(),
                BaseClassInfo = tbs[i].FindForm().Name,
                ControlInfo = tbs[i].Name,
                Tidings = tbs[i].Text,
                Tags = tbs[i].Tag,
                NowTime = DateTime.Now,
            };
            slist.Add(bm);
        }
        return slist;
    }
    public List<TextBox> FindModel(List<BufferModel> slist, TextBox cutTb)
    {
        if (slist == null)
            slist = new List<BufferModel>();
        List<BufferModel> bmlist = slist.FindAll(m => m.BaseClassInfo == cutTb.FindForm().Name
            && m.ControlInfo == cutTb.Name).OrderByDescending(m => m.NowTime).ToList();
        List<TextBox> res = new List<TextBox>();
        foreach (var m in bmlist)
        {
            TextBox tb = new TextBox
            {
                Name = m.ControlInfo,
                Text = m.Tidings,
                Tag = m.Tags,
            };
            res.Add(tb);
        }
        return res;
    }
    public void SettingModel(List<BufferModel> slist, List<TextBox> tlist)
    {
        if (slist == null)
            slist = new List<BufferModel>();
        foreach (var t in tlist)
        {
            BufferModel bm = slist.Find(m => m.BaseClassInfo == t.FindForm().Name && m.ControlInfo == t.Name);
            if (bm != null)
            {
                t.Text = bm.Tidings;
                t.Tag = bm.Tags;
            }
        }
    }
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
        object obj = WrtStore.ReadInfo(fullname);
        if (obj != null)
            CoreList = (List<BufferModel>)obj;
        return obj;
    }
    #endregion
}
#region WrtModel
[Serializable]
public class BufferModel : IWrtModel
{
    public Guid Id { get; set; }
    public string BaseClassInfo { get; set; }
    public string ControlInfo { get; set; }
    public string Tidings { get; set; }
    public object Tags { get; set; }
    public DateTime NowTime { get; set; }
}
#endregion
