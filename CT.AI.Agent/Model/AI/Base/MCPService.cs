using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol.Transport;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CT.AI.Agent.Model.AI.Base;

#region MCPService
public static class MCPService
{
    public static async Task<List<McpClientTool>?> GetToolsAsync()
    {
        var serverList = new List<MCPServerConfig>();
        try
        {
            var path = AppContext.BaseDirectory;
            string jsonString = File.ReadAllText("mcp_settings.json");
            var mcpServerDictionary = JsonSerializer.Deserialize<McpServerDictionary>(jsonString);
            if (mcpServerDictionary?.Servers != null)
            {
                foreach (var server in mcpServerDictionary.Servers)
                {
                    var args = server.Value.Args;
                    if (args.Contains("{localpath}"))
                        args = args.Replace("{localpath}", path);
                    MCPServerConfig serverConfig = new MCPServerConfig();
                    serverConfig.Name = server.Key;
                    serverConfig.Command = server.Value.Command;
                    serverConfig.Args = args;
                    serverList.Add(serverConfig);
                }
            }
            McpClientOptions options = new() { ClientInfo = new() { Name = "AIE-Studio", Version = "1.0.0" } };
            List<StdioClientTransport> mcpServerConfigs = new List<StdioClientTransport>();
            foreach (var server in serverList)
            {
                //Todo :  这里手工需要判断 StdIo
                var clientTransport = new StdioClientTransport(new StdioClientTransportOptions
                {
                    Name = server.Name,
                    Command = server.Command,
                    Arguments = [server.Args],
                });
                mcpServerConfigs.Add(clientTransport);
            }
            List<McpClientTool> mappedTools = new List<McpClientTool>();
            foreach (var config in mcpServerConfigs)
            {
                var client = await McpClientFactory.CreateAsync(config);
                var listToolsResult = await client.ListToolsAsync();
                mappedTools.AddRange(listToolsResult);
            }
            return mappedTools;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error parsing settings: {ex.Message}");
            throw ex;
        }
    }
}
public partial class MCPServerConfig
{
    public string Name { get; set; }
    public string Command { get; set; }
    public string Args { get; set; }
    public IList<McpClientTool> tools { get; set; } = new List<McpClientTool>();
}
public class McpServerDictionary
{
    [JsonPropertyName("mcpServers")]
    public Dictionary<string, ServerConfig> Servers { get; set; } = new Dictionary<string, ServerConfig>();
}
public class ServerConfig
{
    [JsonPropertyName("command")]
    public string Command { get; set; }

    [JsonPropertyName("args")]
    public string Args { get; set; }
}
#endregion