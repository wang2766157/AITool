//using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using CT.AI.Agent.Model.System;
using CT.AI.Agent.Pages.Components;
using Microsoft.Win32;
using System.Diagnostics;

namespace CT.AI.Agent.Services;

#region BaseService
public class BaseService
{
    public readonly static string LOCAL_SESSION_KEY = "CURUSE";
    //public ProtectedSessionStorage _sessionStorage;
}
#endregion
#region SystemService
/// <summary>
/// 系统处理
/// </summary>
public class SystemService : BaseService
{
    #region 菜单临时数据
    /// <summary>
    /// 菜单临时数据
    /// </summary>
    /// <returns></returns>
    public Task<List<MenuModel>> GetMenuListAsync()
    {
        var mlist = new List<MenuModel> {
            new MenuModel{ ID="1",Title ="对话",PID="",Url=".", Idx="1",Icon="home",},
            new MenuModel{ ID="5",Title ="AI智能体设置",PID="",Url="./settings", Idx="5",Icon="brand_github_copilot",},
            new MenuModel{ ID="6",Title ="MCP设置",PID="",Url="./mcpsettings", Idx="6",Icon="settings",},
            new MenuModel{ ID="7",Title ="数据库工具",PID="",Url="./sqltool", Idx="7",Icon="package",},
        };
        return Task.FromResult(mlist);
    }
    #endregion
    #region 用户菜单
    public Task<List<DropDownModel>> GetUserSetList()
    {
        var uslist = new List<DropDownModel>{
            new DropDownModel { Name = "设置状态", Value = "" },
            new DropDownModel { Name = "个人资料及帐号", Value = "" },
            new DropDownModel { IsDivider=true },
            new DropDownModel { Name = "注销", Value = "", Href="/signin" },
        };
        return Task.FromResult(uslist);
    }
    #endregion
}
#endregion
#region ModalService
public class ModalService
{
    private readonly Dictionary<string, CTModal> _modals = new();
    public void RegisterModal(CTModal modal) => _modals[modal.Id] = modal;
    public void UnregisterModal(string id) => _modals.Remove(id);
    public async Task ShowAsync(string modalId)
    {
        if (_modals.TryGetValue(modalId, out var modal))
            await modal.ShowAsync();
    }
    public async Task HideAsync(string modalId)
    {
        if (_modals.TryGetValue(modalId, out var modal))
            await modal.HideAsync();
    }
}
#endregion
#region MCPCheckerService
public class MCPCheckerService
{
    public class EnvironmentCheckResult
    {
        public string Item { get; set; }
        public string Status { get; set; }
        public string Message { get; set; }
        public bool IsSuccess { get; set; }
    }
    public event Action<EnvironmentCheckResult> OnResultUpdated;
    public event Action OnCheckCompleted;
    public async Task RunEnvironmentCheck()
    {
        var results = new List<EnvironmentCheckResult>();
        // 1. 检查 Python
        var pythonResult = await CheckPythonAsync();
        OnResultUpdated?.Invoke(pythonResult);
        if (!pythonResult.IsSuccess) return;
        // 2. 检查 Python 包
        var packagesResult = await CheckPythonPackagesAsync(pythonResult.Message);
        packagesResult.ForEach(OnResultUpdated);
        // 3. 检查 ODBC 驱动
        var odbcResult = await CheckOdbcDriverAsync();
        OnResultUpdated?.Invoke(odbcResult);
        OnCheckCompleted?.Invoke();
    }
    // 1. 检查 Python
    private async Task<EnvironmentCheckResult> CheckPythonAsync()
    {
        try
        {
            // 修复 1: 确保总是获取可执行文件路径
            var pythonExe = FindPythonExecutable();
            if (!string.IsNullOrEmpty(pythonExe) && System.IO.File.Exists(pythonExe))
            {
                var versionInfo = FileVersionInfo.GetVersionInfo(pythonExe);
                if (versionInfo.FileMajorPart == 3)
                {
                    return new EnvironmentCheckResult
                    {
                        Item = "Python 3.x",
                        Status = "✓ 已安装",
                        Message = pythonExe,
                        IsSuccess = true
                    };
                }
            }
        }
        catch (Exception ex)
        {
            return new EnvironmentCheckResult
            {
                Item = "Python 3.x",
                Status = "✘ 检测错误",
                Message = ex.Message,
                IsSuccess = false
            };
        }
        return new EnvironmentCheckResult
        {
            Item = "Python 3.x",
            Status = "✘ 未安装",
            Message = "请从 https://www.python.org/downloads/ 安装",
            IsSuccess = false
        };
    }
    private string FindPythonExecutable()
    {
        // 1. 检查环境变量中的 Python
        var pythonExe = FindInPath("python.exe") ?? FindInPath("python3.exe");
        if (!string.IsNullOrEmpty(pythonExe) && System.IO.File.Exists(pythonExe)) return pythonExe;
        // 2. 检查注册表
        var registryPaths = new[]
        {
            @"SOFTWARE\Python\PythonCore",
            @"SOFTWARE\Wow6432Node\Python\PythonCore" // 32位 Python 在 64位系统
        };
        var views = new[] { RegistryView.Registry32, RegistryView.Registry64 };
        foreach (var view in views)
        {
            using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view))
            {
                foreach (var path in registryPaths)
                {
                    using (var pythonKey = baseKey.OpenSubKey(path))
                    {
                        if (pythonKey == null) continue;
                        foreach (var version in pythonKey.GetSubKeyNames().Where(v => v.StartsWith("3.")))
                        {
                            using (var installKey = pythonKey.OpenSubKey($@"{version}\InstallPath"))
                            {
                                var installPath = installKey?.GetValue("ExecutablePath") as string;
                                if (!string.IsNullOrEmpty(installPath) && System.IO.File.Exists(installPath)) return installPath;
                                // 修复 3: 如果 ExecutablePath 不存在，尝试构造标准路径
                                var defaultPath = installKey?.GetValue("") as string;
                                if (!string.IsNullOrEmpty(defaultPath))
                                {
                                    var exePath = Path.Combine(defaultPath, "python.exe");
                                    if (System.IO.File.Exists(exePath))
                                    {
                                        return exePath;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        return null;
    }
    // 2. 检查 Python 包
    private async Task<List<EnvironmentCheckResult>> CheckPythonPackagesAsync(string pythonPath)
    {
        var results = new List<EnvironmentCheckResult>();
        var packages = new Dictionary<string, string>
        {
            { "pyodbc", "4.0.39" },
            { "pydantic", "2.0.0" },
            { "mcp", "0.1.0" }
        };
        foreach (var package in packages)
        {
            var result = await CheckPythonPackageAsync(pythonPath, package.Key, package.Value);
            results.Add(result);
            if (!result.IsSuccess)
            {
                await InstallPythonPackageAsync(pythonPath, package.Key, package.Value);
                // 重新检查安装结果
                results.Add(await CheckPythonPackageAsync(pythonPath, package.Key, package.Value));
            }
        }
        return results;
    }
    private async Task<EnvironmentCheckResult> CheckPythonPackageAsync(string pythonPath, string package, string minVersion)
    {
        if (string.IsNullOrEmpty(pythonPath) || !System.IO.File.Exists(pythonPath))
        {
            return new EnvironmentCheckResult
            {
                Item = package,
                Status = "✘ Python 路径无效",
                Message = $"找不到 Python 可执行文件: {pythonPath}",
                IsSuccess = false
            };
        }
        try
        {
            // 使用更可靠的版本检测方法
            string command = $"-c \"from importlib.metadata import version; version = version('{package}'); print(version);exit(0)\"";
            var startInfo = new ProcessStartInfo
            {
                FileName = pythonPath,
                Arguments = command,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = new Process { StartInfo = startInfo };
            process.Start();
            process.StandardInput.AutoFlush = true;
            process.StandardInput.WriteLine(command);
            await process.WaitForExitAsync();
            if (process.ExitCode == 0)
            {
                var versionOutput = (await process.StandardOutput.ReadToEndAsync()).Trim();
                // 处理版本号可能包含额外信息的情况
                var version = versionOutput.Split('\n').FirstOrDefault()?.Trim();
                if (version != null && !version.StartsWith("No module named") && !version.StartsWith("module") && !version.Contains("has no attribute"))
                {
                    if (CompareVersions(version, minVersion) >= 0)
                    {
                        return new EnvironmentCheckResult
                        {
                            Item = package,
                            Status = "✓ 已安装",
                            Message = $"版本: {version} (>= {minVersion})",
                            IsSuccess = true
                        };
                    }
                    return new EnvironmentCheckResult
                    {
                        Item = package,
                        Status = "⚠ 版本过低",
                        Message = $"当前: {version}, 需要: {minVersion}+",
                        IsSuccess = false
                    };
                }
                return new EnvironmentCheckResult
                {
                    Item = package,
                    Status = "✘ 未安装",
                    Message = versionOutput,
                    IsSuccess = false
                };
            }
            else
            {
                var error = await process.StandardError.ReadToEndAsync();
                return new EnvironmentCheckResult
                {
                    Item = package,
                    Status = "✘ 检测错误",
                    Message = error,
                    IsSuccess = false
                };
            }
        }
        catch (Exception ex)
        {
            return new EnvironmentCheckResult
            {
                Item = package,
                Status = "✘ 检测错误",
                Message = $"执行命令时出错: {ex.Message}",
                IsSuccess = false
            };
        }
    }
    private async Task InstallPythonPackageAsync(string pythonPath, string package, string minVersion)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = pythonPath,
                Arguments = $"-m pip install {package}>={minVersion}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = new Process { StartInfo = startInfo };
            process.Start();
            await process.WaitForExitAsync();
        }
        catch
        {
            // 忽略安装错误，会在后续检查中体现
        }
    }
    // 3. 检查 ODBC 驱动
    private async Task<EnvironmentCheckResult> CheckOdbcDriverAsync()
    {
        try
        {
            if (IsOdbcDriverInstalled())
            {
                return new EnvironmentCheckResult
                {
                    Item = "ODBC Driver 17",
                    Status = "✓ 已安装",
                    Message = "ODBC Driver 17 for SQL Server",
                    IsSuccess = true
                };
            }
            return new EnvironmentCheckResult
            {
                Item = "ODBC Driver 17",
                Status = "✘ 未安装",
                Message = "请从以下链接下载安装:\n" +
                          "Windows: https://go.microsoft.com/fwlink/?linkid=2187220\n" +
                          "Linux: https://learn.microsoft.com/en-us/sql/connect/odbc/linux-mac/installing-the-microsoft-odbc-driver-for-sql-server\n" +
                          "macOS: https://learn.microsoft.com/en-us/sql/connect/odbc/linux-mac/install-microsoft-odbc-driver-sql-server-macos",
                IsSuccess = false
            };
        }
        catch (Exception ex)
        {
            return new EnvironmentCheckResult
            {
                Item = "ODBC Driver 17",
                Status = "✘ 检测错误",
                Message = ex.Message,
                IsSuccess = false
            };
        }
    }
    private static bool IsOdbcDriverInstalled()
    {
        try
        {
            // Windows检测方法
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                using var odbcKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\ODBC\ODBCINST.INI\ODBC Drivers");
                return odbcKey?.GetValue("ODBC Driver 17 for SQL Server") != null;
            }
            // Linux/macOS检测方法
            var startInfo = new ProcessStartInfo
            {
                FileName = "odbcinst",
                Arguments = "-q -d",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = Process.Start(startInfo);
            process.WaitForExit(5000);
            if (process.ExitCode == 0)
            {
                var output = process.StandardOutput.ReadToEnd();
                return output.Contains("ODBC Driver 17 for SQL Server");
            }
        }
        catch
        {
            // 忽略错误
        }
        return false;
    }
    private static int CompareVersions(string v1, string v2)
    {
        var version1 = ParseVersion(v1);
        var version2 = ParseVersion(v2);
        for (int i = 0; i < Math.Max(version1.Length, version2.Length); i++)
        {
            int part1 = i < version1.Length ? version1[i] : 0;
            int part2 = i < version2.Length ? version2[i] : 0;
            if (part1 < part2) return -1;
            if (part1 > part2) return 1;
        }
        return 0;
    }
    private static int[] ParseVersion(string version)
    {
        return version.Split('.')
            .Take(4) // 最多比较4段版本号
            .Select(part =>
            {
                var t = new string(part.TakeWhile(char.IsDigit).ToArray());
                return int.TryParse(t, out int num) ? num : 0;
            }).ToArray();
    }
    private static string FindInPath(string fileName)
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in path.Split(Path.PathSeparator))
        {
            try
            {
                var fullPath = Path.Combine(dir, fileName);
                if (System.IO.File.Exists(fullPath))
                {
                    return fullPath;
                }
            }
            catch
            {
                // 忽略无效路径
            }
        }
        return null;
    }
}
#endregion
//===================纠结的分隔线==================//
