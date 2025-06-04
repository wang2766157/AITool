using CT.AI.Agent.Model.AI.Base;
using CT.AI.Agent.Model.System;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;

namespace CT.AI.Agent.Model.AI;

public class AIServiceEmbeddingGenerator
{
    #region 构造函数
    public readonly OpenAIBase AiBase;
    public AIServiceEmbeddingGenerator(OpenAIBase ai)
    {
        AiBase = ai;
    }
    #endregion
    #region ResponseAsync
    public async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(IEnumerable<string> values, EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var generator = AiBase.Client.AsEmbeddingGenerator(AiBase.Model);
        var embeddings = await generator.GenerateAsync(values, options, cancellationToken);
        return embeddings;
    }
    public async IAsyncEnumerable<string> GenerateEmbeddingVectorAsync(IEnumerable<string> values, EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var generator = AiBase.Client.AsEmbeddingGenerator(AiBase.Model);
        foreach (var prompt in values)
        {
            var embedding = await generator.GenerateEmbeddingVectorAsync(prompt, options, cancellationToken);
            yield return string.Join(", ", embedding.ToArray());
        }
    }
    #endregion
    [Testing]
    public async Task<string> Test()
    {
        var cloudServices = new List<CloudService>()
        {
            new CloudService
                {
                    Key=0,
                    Name="Azure 应用服务",
                    Description=@"
""Azure 应用服务"" 是微软 Azure 平台提供的一项全托管服务，支持您直接部署 .NET、Java、Node.js 和 Python 编写的 Web 应用程序和 API。
您只需将代码部署到 Azure，即可自动获得基础设施管理能力，包括高可用性、负载均衡和自动扩缩容等功能，您无需关注底层服务器维护等运维工作。"
                },
            new CloudService
                {
                    Key=1,
                    Name="Azure 服务总线",
                    Description=@"
""Azure 服务总线（Azure Service Bus）"" 是微软 Azure 平台提供的全托管企业级消息中间件，支持点对点通信（Point-to-Point）和发布-订阅模式（Publish-Subscribe），
适用于构建松耦合应用、实现基于队列的负载均衡，或作为微服务间的通信桥梁。
"
                },
            new CloudService
                {
                    Key=2,
                    Name="Azure Blob 存储",
                    Description=@"Azure Blob 存储允许您的应用程序在云中存储和检索文件。Azure存储具有高度可扩展性，可以存储大量数据，并且数据被冗余存储以确保高可用性。"
                },
            new CloudService
                {
                    Key=3,
                    Name="微软 Entra ID",
                    Description="管理用户身份并控制对应用程序、数据和资源的访问。"
                },
            new CloudService
                {
                    Key=4,
                    Name="Azure 密钥库",
                    Description="将连接字符串和API密钥等应用程序机密存储和访问在加密的保险库中，并限制访问，以确保您的机密和应用程序不会泄露。"
                },
            new CloudService
                {
                    Key=5,
                    Name="Azure 人工智能搜索",
                    Description="传统和会话搜索应用程序的大规模信息检索，具有安全性和人工智能丰富和矢量化选项。"
                }
        };

        //var cloudServices2 = new List<CloudService>();
        //string path = "D:\\123.xlsx";
        //var tcWorkBook = new Workbook(path);
        //var cells = tcWorkBook.Worksheets[0].Cells;
        //if (cells.MaxDataRow != -1 && cells.MaxDataColumn != -1)
        //{
        //    var dt = cells.ExportDataTable(0, 0, cells.MaxDataRow + 1, cells.MaxDataColumn + 1, true);
        //    //这里需要研究一下能不能压缩表格 考虑改成csv的形式
        //    //var json = JsonConvert.SerializeObject(dt, Formatting.Indented);
        //    for (int i = 0; i < dt.Rows.Count; i++)
        //    {
        //        var cs = new CloudService();
        //        cs.Key = i;
        //        cs.Name = dt.Rows[i][0].ToString();
        //        cs.Description= JsonConvert.SerializeObject(dt.Rows[i], Formatting.Indented);
        //        cloudServices2.Add(cs);
        //    }
        //}

        var generator = AiBase.Client.AsEmbeddingGenerator(AiBase.Model);

        var vectorStore = new InMemoryVectorStore();
        IVectorStoreRecordCollection<int, CloudService> cloudServicesStore = vectorStore.GetCollection<int, CloudService>("cloudServices");
        await cloudServicesStore.CreateCollectionIfNotExistsAsync();
        foreach (CloudService service in cloudServices)//
        {
            service.Vector = await generator.GenerateEmbeddingVectorAsync(service.Description);
            await cloudServicesStore.UpsertAsync(service);
        }
        string query = "按销售量合计排名";
        ReadOnlyMemory<float> queryEmbedding = await generator.GenerateEmbeddingVectorAsync(query);

        VectorSearchResults<CloudService> results = await cloudServicesStore.VectorizedSearchAsync(queryEmbedding, new VectorSearchOptions<CloudService>() { Top = 1 });

        string res = "";
        await foreach (VectorSearchResult<CloudService> result in results.Results)
        {
            res += $"名称: {result.Record.Name}";
            res += $"描述: {result.Record.Description}";
            res += $"矢量匹配得分: {result.Score}";
        }
        return res;

    }

}
