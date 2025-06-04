using CT.AI.Agent.Model.System;
using Aspose.Cells;
using Microsoft.Extensions.AI;
using Newtonsoft.Json;
using System.ComponentModel;
using System.Diagnostics;

namespace CT.AI.Agent.Model.AI.Base;

public sealed class ToolsFunction
{
    [Description("系统这里是分析数据")]
    public string ExtractExcelContent([Description("文件路径")] string filePath)
        => ConvertToMarkdown(filePath);
    [Description("系统这里是打开文件")]
    public void GetOpenFile([Description("文件路径")] string path)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true // 让系统选择关联程序打开
        });
    }
    [Description("系统这里是保存记录")]
    public void ToSaveFile([Description("文本内容")] string content, [Description("是否需要打开文件")] bool isOpen = false)
    {
        string dirPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        string filePath = dirPath + "\\" + $"Output_{Guid.CreateVersion7()}.txt";
        // 将文本直接保存到文件（会覆盖原有内容）
        File.WriteAllText(filePath, content);
        if (isOpen)
            GetOpenFile(filePath);
    }
    [Testing]
    [Description("系统这里是袜子的金额，返回值是金额")]
    public float GetPrice([Description("这里输入袜子的数量，计算金额")] int count) => count * 15.9f;

    //TODO
    public static string ConvertToMarkdown(string path)
    {
        string res = "";
        var tcWorkBook = new Workbook(path);
        var cells = tcWorkBook.Worksheets[0].Cells;
        if (cells.MaxDataRow != -1 && cells.MaxDataColumn != -1)
        {
            var dt = cells.ExportDataTable(0, 0, cells.MaxDataRow + 1, cells.MaxDataColumn + 1, true);
            //这里需要研究一下能不能压缩表格 考虑改成csv的形式
            var json = JsonConvert.SerializeObject(dt, Formatting.None);
            res = json;
        }
        return res;
    }
}
public static class FunctionService
{
    public static List<Microsoft.Extensions.AI.AITool> GetTools()
    {
        var tf = new ToolsFunction();
        var TLExtractExcelContent = AIFunctionFactory.Create(tf.ExtractExcelContent);
        var TLGetPrice = AIFunctionFactory.Create(tf.GetPrice);
        var TLGetOpenFile = AIFunctionFactory.Create(tf.GetOpenFile);
        var TLToSaveFile = AIFunctionFactory.Create(tf.ToSaveFile);
        return new List<Microsoft.Extensions.AI.AITool>
        {
            TLExtractExcelContent,
            TLGetPrice,
            TLGetOpenFile,
            TLToSaveFile,
        };
    }
}