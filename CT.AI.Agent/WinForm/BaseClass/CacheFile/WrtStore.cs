using System.IO.Compression;
using System.Text.Json;

namespace WrtStoreNs;

public class WrtStore
{
    #region 参数及内部方法
    public static string Extension = ".wrt";
    private static byte[] CompressionObject(object dataOriginal)
    {
        if (dataOriginal == null) return null;
        MemoryStream mStream = new MemoryStream();
        JsonSerializer.Serialize(mStream, dataOriginal);
        byte[] bytes = mStream.ToArray();
        MemoryStream oStream = new MemoryStream();
        DeflateStream zipStream = new DeflateStream(oStream, CompressionMode.Compress);
        zipStream.Write(bytes, 0, bytes.Length);
        zipStream.Flush();
        zipStream.Close();
        return oStream.ToArray();
    }
    private static object DecompressionObject(byte[] bytes)
    {
        if (bytes == null) return null;
        MemoryStream mStream = new MemoryStream(bytes);
        mStream.Seek(0, SeekOrigin.Begin);
        DeflateStream unZipStream = new DeflateStream(mStream, CompressionMode.Decompress, true);
        JsonElement dsResult = JsonSerializer.Deserialize<JsonElement>(unZipStream);
        return dsResult;
    }
    #endregion
    #region 写入
    public static void WriteInfo(object obj, string fullname)
    {
        if (obj == null) return;
        byte[] bs = CompressionObject(obj);
        FileStream fs = new FileStream(fullname, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Write);
        try
        {
            fs.Write(bs, 0, bs.Length);
        }
        catch (System.Exception ex)
        {
            throw ex;
        }
        finally
        {
            fs.Flush();
            fs.Close();
        }
    }
    #endregion
    #region 读取
    public static object ReadInfo(string fullname)
    {
        if (!File.Exists(fullname))
        {
            return null;
        }
        FileStream fs = File.Open(fullname, FileMode.Open);
        try
        {
            byte[] bss = new byte[fs.Length];
            int i = fs.Read(bss, 0, (int)fs.Length);
            object o = DecompressionObject(bss); //还原，ok 
            return o;
        }
        catch (System.Exception ex)
        {
            throw ex;
        }
        finally
        {
            fs.Flush();
            fs.Close();
        }
    }
    #endregion
    //==================纠结的分割线==================//
}
