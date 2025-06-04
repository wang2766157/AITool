using CT.AI.Agent.Services;
using CT.AI.Agent.Services.File;
using BaseFormNs;
using Microsoft.AspNetCore.Components.WebView.WindowsForms;
using Microsoft.Extensions.DependencyInjection;
using WrtStoreNs;

namespace CT.AI.Agent;
#pragma warning disable WFO1000 

public partial class MainForm : BaseForm
{
    public static bool NewFlag { get; set; } = false;
    //加载标记
    private bool _flagLoading = false;
    //版本
    private static DateTime middle_patch = new DateTime(2025, 5, 12);
    private string correct_patch = "03-beta";
    public MainForm()
    {
        _flagLoading = false;
        InitializeComponent();
        //窗体设定
        var version = GenerateSemanticVersion(correct_patch);
        Width = 1200;
        Height = 800;
        Text = "AI - Tool " + version;
        MinimumSize = new Size(800, 600);

        //blazor 初始化设定与配置
        var service = new ServiceCollection();
        service.AddWindowsFormsBlazorWebView();
        service.AddBlazorWebViewDeveloperTools();
        //绑定数据服务
        service.AddScoped<SystemService>();
        service.AddScoped<FileService>();
        service.AddScoped<ModalService>();

        var blazor = new BlazorWebView();
        blazor.AutoScroll = false;
        blazor.Dock = DockStyle.Fill;

        blazor.HostPage = "wwwroot/index.html";
        blazor.Services = service.BuildServiceProvider();
        blazor.RootComponents.Add<App>("#app");
        Controls.Add(blazor);
        //blazor.UrlLoading += (sender, urlLoadingEventArgs) =>
        //{
        //    if (urlLoadingEventArgs.Url.Host != "0.0.0.0")
        //    {
        //        urlLoadingEventArgs.UrlLoadingStrategy = UrlLoadingStrategy.OpenInWebView;
        //    }
        //};
        _flagLoading = true;
        //自动生成初始配置文件
        var fs = blazor.Services.GetRequiredService<FileService>();
        string p = fs.RootPath + "/" + fs.FileName + WrtStore.Extension;
        if (!File.Exists(p))
        {
            fs.SaveTempData(new TempModel { PageInfo = "ApiServerValue", Tags = "https://api.siliconflow.cn/v1" });
            fs.SaveTempData(new TempModel { PageInfo = "TokenText", Tags = "1" });
            fs.SaveTempData(new TempModel { PageInfo = "ApiModelValue", Tags = "Qwen/QwQ-32B" });
            NewFlag = true;
        }
    }
    #region 解决运行时总会闪烁
    /// <summary>
    /// 解决运行时总会闪烁
    /// </summary>
    protected override CreateParams CreateParams
    {
        get
        {
            if (!_flagLoading)
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x02000000;
                return cp;
            }
            else
            {
                return base.CreateParams;
            }
        }
    }
    #endregion
    //版本号
    static string GenerateSemanticVersion(string buildMetadata)
    {
        // 使用当前日期作为 MAJOR.MINOR.PATCH
        DateTime now = middle_patch;
        return $"{now.Year % 100}.{now.Month}.{now.Day}.{buildMetadata}";
    }
    //==================纠结的分割线==================//
}
