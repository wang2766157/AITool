namespace ExceptionExtensionsNs;

public static class ExceptionExtensions
{
    // 扩展方法：将字符串首字母大写
    public static string GetInnerExceptionMessage(this Exception ex)
    {
        Func<Exception, string> GetMsg = null;
        GetMsg = (Exception ee) =>
        {
            string res = "";
            if (ee.InnerException != null)
            {
                res += ee.InnerException.Message + Environment.NewLine;
                res += GetMsg(ee.InnerException);
            }
            return res;
        };
        return GetMsg(ex);
    }
}
