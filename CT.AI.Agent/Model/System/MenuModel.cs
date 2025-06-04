using System.ComponentModel.DataAnnotations;

namespace CT.AI.Agent.Model.System;
public class MenuModel
{
    [Key]
    public string ID { get; set; }
    public string Title { get; set; }
    public string PID { get; set; }
    public string Url { get; set; }
    public string Idx { get; set; }
    public string Icon { get; set; } = "";
    public string IsParent { get; set; } ="0";
    public int IsDel { get; set; } = 0;
    public int IsNew { get; set; } = 0;
    /// <summary>
    /// 判断连接类型是否是 target="_blank" rel="noopener"
    /// </summary>
    public int IsTarget { get; set; } = 0;
    /// <summary>
    /// 文字颜色
    /// </summary>
    public string TextColorCss { get; set; } = "";
}
