using Microsoft.Extensions.AI;
using OpenAI;
using System.ClientModel;
using System.Collections;
using System.Reflection;

namespace CT.AI.Agent.Model.AI.Base;

public class OpenAIBase
{
    public string Model { get; set; }
    private readonly OpenAIClient _client;
    public OpenAIClient Client { get { return _client; } }
    public OpenAIBase(string uri, string token, string model = "Qwen/QwQ-32B")
    {
        var op = new OpenAIClientOptions { Endpoint = new(uri) };
        _client = new OpenAIClient(new ApiKeyCredential(token), op);
        Model = model;
    }
}
public class ChatTalk
{
    public ChatMessage? ChatMsg { get; set; }
    public string Thinking { get; set; } = "";
    public int ChatIndex { get; set; }
    public bool IsWait { get; set; } = false;
    public DateTime ChatTime { get; set; }
    public UsageDetails? Usage { get; set; }
}
public static class ChatTalkExtensions
{
    public static ChatTalk TalkBuilding(this ChatTalk ct, string role, int idx, string text)
    {
        if (role.ToLower() == "") role = "assistant";
        var r = new ChatRole(role.ToLower());
        ct.ChatIndex = idx;
        ct.ChatTime = DateTime.Now;
        ct.ChatMsg = new ChatMessage(r, text);
        return ct;
    }
    public static bool IsUser(this ChatTalk ct)
    {
        if (ct.ChatMsg == null) return false;
        return ct.ChatMsg.Role == ChatRole.User;
    }
    public static string GetTokenInfo(this ChatTalk ct)
    {
        var res = "";
        if (ct.Usage != null)
        {
            List<string> list = new List<string>();
            long? inputTokenCount = ct.Usage.InputTokenCount;
            if (inputTokenCount.HasValue)
            {
                long valueOrDefault = inputTokenCount.GetValueOrDefault();
                list.Add($"{"输入"} = {valueOrDefault}");
            }
            inputTokenCount = ct.Usage.OutputTokenCount;
            if (inputTokenCount.HasValue)
            {
                long valueOrDefault2 = inputTokenCount.GetValueOrDefault();
                list.Add($"{"输出"} = {valueOrDefault2}");
            }
            inputTokenCount = ct.Usage.TotalTokenCount;
            if (inputTokenCount.HasValue)
            {
                long valueOrDefault3 = inputTokenCount.GetValueOrDefault();
                list.Add($"{"总数"} = {valueOrDefault3}");
            }
            var additionalCounts = ct.Usage.AdditionalCounts;
            if (additionalCounts != null)
            {
                foreach (KeyValuePair<string, long> item in additionalCounts)
                {
                    list.Add($"{item.Key} = {item.Value}");
                }
            }
            res = string.Join(", ", list);
        }
        return res;
    }
    public static List<ChatMessage?> DecodeMessageList(this List<ChatTalk> ChatTalks)
        => ChatTalks.OrderBy(x => x.ChatIndex).Select(x => x.ChatMsg).ToList();
}
/// <summary>
/// 获取 think 思维过程
/// </summary>
public static class ChatCompletionUpdateExtensions
{
    public static Func<object, string?> ThinkingAccessor { get; } = CreateStreamingReasoningContentAccessor();
    /// <summary>
    /// 创建一个从 StreamingChatCompletionUpdate 对象中提取 reasoning_content 的委托。
    /// 如果未找到或无法解析，则返回 null。
    /// </summary>
    /// <returns>Func<object, string?></returns>
    public static Func<object, string?> CreateStreamingReasoningContentAccessor()
    {
        var bindingflags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        //获取 StreamingChatCompletionUpdate 类型
        Type streamingChatType = typeof(OpenAI.Chat.StreamingChatCompletionUpdate);
        //获取 internal 属性 "Choices"
        //类型：IReadOnlyList<InternalCreateChatCompletionStreamResponseChoice>
        PropertyInfo? choicesProp = streamingChatType.GetProperty("Choices", bindingflags)
            ?? throw new InvalidOperationException("Unable to reflect property 'Choices' in StreamingChatCompletionUpdate.");
        // 3. 获取 Choices 的泛型参数 T = InternalCreateChatCompletionStreamResponseChoice
        Type? choicesPropType = choicesProp.PropertyType ?? throw new InvalidOperationException("Unable to determine the property type of 'Choices'."); // IReadOnlyList<T>
        if (!choicesPropType.IsGenericType || choicesPropType.GetGenericArguments().Length != 1)
            throw new InvalidOperationException("Property 'Choices' is not the expected generic type IReadOnlyList<T>.");
        // 取得 T
        Type choiceType = choicesPropType.GetGenericArguments()[0];
        // 4. 从 choiceType 中获取 internal 属性 "Delta"
        PropertyInfo? deltaProp = choiceType.GetProperty("Delta", bindingflags)
            ?? throw new InvalidOperationException("Unable to reflect property 'Delta' in choice type.");
        // 5. 获取 Delta 对象的类型，然后从中获取 "SerializedAdditionalRawData"
        Type deltaType = deltaProp.PropertyType;
        PropertyInfo? rawDataProp = deltaType.GetProperty("SerializedAdditionalRawData", bindingflags)
            ?? throw new InvalidOperationException("Unable to reflect property 'SerializedAdditionalRawData' in delta type.");
        // 创建并返回委托，在委托中使用上述缓存的 PropertyInfo
        return streamingChatObj =>
        {
            if (streamingChatObj == null) return null;
            // 拿到 choices 数据
            object? choicesObj = choicesProp.GetValue(streamingChatObj);
            if (choicesObj is not IEnumerable choicesEnumerable) return null;
            foreach (object? choice in choicesEnumerable)
            {
                if (choice == null) continue;
                // 获取 Delta 对象
                object? deltaObj = deltaProp.GetValue(choice);
                if (deltaObj == null) continue;
                // 获取字典 SerializedAdditionalRawData
                object? rawDataValue = rawDataProp.GetValue(deltaObj);
                if (rawDataValue is not IDictionary<string, BinaryData> dict) continue;
                // 从字典里查找 "reasoning_content"
                if (dict.TryGetValue("reasoning_content", out BinaryData? binaryData))
                    return binaryData.ToObjectFromJson<string>();
            }
            // 如果所有 Choice 中都没有找到则返回 null
            return null;
        };
    }
}