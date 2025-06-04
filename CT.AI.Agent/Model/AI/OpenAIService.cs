using CT.AI.Agent.Model.AI.Base;
using CT.AI.Agent.Model.System;
using OpenAI.Chat;
using System.Runtime.CompilerServices;

namespace CT.AI.Agent.Model.AI;
public class OpenAIService
{
    #region 构造函数
    public readonly OpenAIBase AiBase;
    public List<Microsoft.Extensions.AI.AITool> AiToolList = new();
    public OpenAIService(OpenAIBase ai)
    {
        AiBase = ai;
    }
    #endregion
    #region ResponseAsync
    public async IAsyncEnumerable<StreamingChatCompletionUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatCompletionOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var ic = AiBase.Client.GetChatClient(AiBase.Model);

        if (options == null) options = new ChatCompletionOptions();
        if (AiToolList.Count > 0)
        {
            options = GetAIFunction(options);
        }

        var requestBody = ic.CompleteChatStreamingAsync(messages, options, cancellationToken);
        await foreach (var item in requestBody.WithCancellation(cancellationToken))
        {
            yield return item;
        }
    }
    #endregion
    #region AIFunction
    [Testing]
    public ChatCompletionOptions GetAIFunction(ChatCompletionOptions options)
    {
        var TLGetPrice = ChatTool.CreateFunctionTool("GetPrice");//?
        options.Tools.Add(TLGetPrice);
        options.ToolChoice = ChatToolChoice.CreateAutoChoice();
        return options;
    }
    #endregion
}
