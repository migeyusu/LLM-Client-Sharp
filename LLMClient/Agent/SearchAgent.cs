using System.Text.Json.Serialization;
using LLMClient.Abstraction;
using LLMClient.Component.CustomControl;
using LLMClient.Data;
using MaterialDesignThemes.Wpf;

namespace LLMClient.Agent;

/// <summary>
/// search agent 会自动根据内容产生搜索请求，并将结果添加到对话中。
/// <para>目的是节约tokens消耗</para>
/// </summary>
public abstract class SearchAgent : IAgent, ISearchOption
{
    [JsonIgnore] public abstract string Name { get; }

    public abstract string GetUniqueId();

    [JsonIgnore]
    public virtual ThemedIcon Icon => new LocalThemedIcon(PackIconKind.HeadDotsHorizontalOutline.ToImageSource());

    public bool CheckCompatible(ILLMChatClient client)
    {
        return true;
    }

    public abstract Task ApplySearch(DialogContext context);

    public abstract object Clone();
}