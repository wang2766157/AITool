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
            #region ����δ������쳣
            //����δ������쳣
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            //����UI�߳��쳣
            Application.ThreadException += Application_ThreadException;
            //�����UI�߳��쳣
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            #endregion
            #region Ӧ�ó��������ڵ�
            // ��Ҫ�Զ���Ӧ�ó������ã��������ø�DPI���û�Ĭ�����壬
            // ��� https://aka.ms/applicationconfiguration.
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
            //�û�ȡ������
        }
        catch (Exception ex)
        {
            #region �������
            string str = "";
            string strDateInfo = "����Ӧ�ó���δ������쳣��" + DateTime.Now.ToString() + "\r\n";
            if (ex != null)
                str = string.Format(strDateInfo + "�쳣���ͣ�{0}\r\n�쳣��Ϣ��{1}\r\n�쳣��Ϣ��{2}\r\n", ex.GetType().Name, ex.Message, ex.StackTrace);
            else
                str = string.Format("Ӧ�ó����̴߳���:{0}", ex);
            MessageBox.Show(str);
            #endregion
        }
    }
    #region ����δ������쳣
    static void Application_ThreadException(object sender, ThreadExceptionEventArgs e)
    {
        string str = "";
        string strDateInfo = "����Ӧ�ó���δ������쳣��" + DateTime.Now + "\r\n";
        Exception error = e.Exception as Exception;
        if (error != null)
        {
            str = string.Format(strDateInfo + "�쳣���ͣ�{0}\r\n�쳣��Ϣ��{1}\r\n�쳣��Ϣ��{2}\r\n",
            error.GetType().Name, error.Message, error.StackTrace);
        }
        else
            str = string.Format("Ӧ�ó����̴߳���:{0}", e);
        MessageBox.Show(str);
        //�쳣֮��ˢ��ҳ������״̬
    }

    static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        string str = "";
        Exception error = e.ExceptionObject as Exception;
        string strDateInfo = "����Ӧ�ó���δ������쳣��" + DateTime.Now + "\r\n";
        if (error != null)
            str = string.Format(strDateInfo + "Application UnhandledException:{0};\n\r��ջ��Ϣ:{1}", error.Message, error.StackTrace);
        else
            str = string.Format("Ӧ�ó���δ�������:{0}", e);
        MessageBox.Show(str);
    }
    #endregion
}