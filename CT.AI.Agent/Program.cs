using System.ComponentModel;

namespace CT.AI.Agent;

internal static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main(string[] args)
    {
        try
        {
            #region 捕获未捕获的异常
            //处理未捕获的异常
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            //处理UI线程异常
            Application.ThreadException += Application_ThreadException;
            //处理非UI线程异常
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            #endregion
            #region 应用程序的主入口点
            // 若要自定义应用程序配置，例如设置高DPI设置或默认字体，
            // 详见 https://aka.ms/applicationconfiguration.
            //Application.SetHighDpiMode(HighDpiMode.SystemAware);
            //Application.EnableVisualStyles();
            //Application.SetCompatibleTextRenderingDefault(true);
            ApplicationConfiguration.Initialize();
            if (args.Length > 0 && args[0] == "-god")
            {
                //Application.Run(new TestMenu());
            }
            else
                Application.Run(new MainForm());
            #endregion
        }
        catch (Win32Exception)
        {
            //用户取消操作
        }
        catch (Exception ex)
        {
            #region 捕获错误
            string str = "";
            string strDateInfo = "出现应用程序未处理的异常：" + DateTime.Now.ToString() + "\r\n";
            if (ex != null)
                str = string.Format(strDateInfo + "异常类型：{0}\r\n异常消息：{1}\r\n异常信息：{2}\r\n", ex.GetType().Name, ex.Message, ex.StackTrace);
            else
                str = string.Format("应用程序线程错误:{0}", ex);
            MessageBox.Show(str);
            #endregion
        }
    }
    #region 捕获未捕获的异常
    static void Application_ThreadException(object sender, ThreadExceptionEventArgs e)
    {
        string str = "";
        string strDateInfo = "出现应用程序未处理的异常：" + DateTime.Now + "\r\n";
        Exception error = e.Exception as Exception;
        if (error != null)
        {
            str = string.Format(strDateInfo + "异常类型：{0}\r\n异常消息：{1}\r\n异常信息：{2}\r\n",
            error.GetType().Name, error.Message, error.StackTrace);
        }
        else
            str = string.Format("应用程序线程错误:{0}", e);
        MessageBox.Show(str);
        //异常之后刷新页面重置状态
    }

    static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        string str = "";
        Exception error = e.ExceptionObject as Exception;
        string strDateInfo = "出现应用程序未处理的异常：" + DateTime.Now + "\r\n";
        if (error != null)
            str = string.Format(strDateInfo + "Application UnhandledException:{0};\n\r堆栈信息:{1}", error.Message, error.StackTrace);
        else
            str = string.Format("应用程序未处理错误:{0}", e);
        MessageBox.Show(str);
    }
    #endregion
}