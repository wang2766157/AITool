using CT.AI.Agent.Model.AI.Base;
using CT.AI.Agent.Model.System;
using Microsoft.Extensions.AI;
using System.Runtime.CompilerServices;

namespace CT.AI.Agent.Model.AI;

public class AIService
{
    #region 构造函数
    public readonly OpenAIBase AiBase;
    public List<Microsoft.Extensions.AI.AITool> AiToolList = new();
    public AIService(OpenAIBase ai)
    {
        AiBase = ai;
    }
    #endregion
    #region Building
    private IChatClient ClientBuilding()
    {
        var ic = AiBase.Client.GetChatClient(AiBase.Model);
        var clientBuiider = new ChatClientBuilder(ic.AsIChatClient());
        if (AiToolList.Count > 0)
            clientBuiider = clientBuiider.UseFunctionInvocation(); //启用函数调用功能
        var client = clientBuiider.Build();
        return client;
    }
    private ChatOptions OptionBuilding(ChatOptions? options, List<Microsoft.Extensions.AI.AITool> tlist)
    {
        if (options == null) options = new ChatOptions();
        var pp = new AdditionalPropertiesDictionary { { "stream", true }, { "max_tokens", 512 },
            { "temperature", 0.7 },  { "top_p", 0.7 }, { "top_k", 50 }, { "frequency_penalty", 0.5 }, };
        options.AdditionalProperties = pp;
        if (AiToolList.Count > 0) options = GetAIFunction(options, tlist);
        return options;
    }
    #endregion
    #region ResponseAsync
    public async Task<ChatResponse> GetChatClientResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options = OptionBuilding(options, AiToolList);
        var client = ClientBuilding();
        var result = await client.GetResponseAsync(messages, options, cancellationToken);
        return result;
    }
    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        options = OptionBuilding(options, AiToolList);
        var client = ClientBuilding();
        var request = client.GetStreamingResponseAsync(messages, options, cancellationToken);
        await foreach (var item in request.WithCancellation(cancellationToken))
        {
            yield return item;
        }
    }
    #endregion
    #region AIFunction
    public ChatOptions GetAIFunction(ChatOptions options, List<Microsoft.Extensions.AI.AITool> tlist)
    {
        options.Tools = tlist;
        options.ToolMode = ChatToolMode.Auto;
        return options;
    }
    #endregion

    [Testing]
    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync2(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var client = ClientBuilding();

        //options = GetAIFunction();

        var requestBody = client.GetStreamingResponseAsync(messages, options, cancellationToken);
        await foreach (var item in requestBody.WithCancellation(cancellationToken))
        {
            yield return item;
        }
    }
}

