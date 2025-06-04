using System.ComponentModel;

namespace CT.AI.Agent.Pages.Code;

public enum ChatPlacementEnum
{
    [Description("chat-start")]
    ChatStart,
    [Description("chat-end")]
    ChatEnd,
}

public static class EnumExtensions
{
    public static string GetDescription(this Enum value)
    {
        // 获取枚举类型和字段名称
        var field = value.GetType().GetField(value.ToString());
        // 获取 DescriptionAttribute 属性
        var attribute = (DescriptionAttribute)Attribute.GetCustomAttribute(field, typeof(DescriptionAttribute));
        // 返回描述或默认值
        return attribute?.Description ?? value.ToString();
    }
}
