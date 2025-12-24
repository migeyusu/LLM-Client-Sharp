using Microsoft.SemanticKernel;
// 如果需要特定配置支持

namespace LLMClient.ContextEngineering;

public static class PromptTemplateRenderer
{
    // 创建一个空的 Kernel 实例。
    // 在 SK 中，渲染模版需要一个 Kernel 上下文（即使里面没有挂载任何 AI 服务），
    // 主要是为了将来如果在模版里使用了 Helper Function (例如 {{$time.now}}) 能够找到对应的插件。
    private static readonly Kernel _emptyKernel = new Kernel();

    private static readonly KernelPromptTemplateFactory _templateFactory = new KernelPromptTemplateFactory();

    /// <summary>
    /// 使用 Semantic Kernel 的标准语法渲染模版
    /// </summary>
    /// <param name="templateText">SK 格式的模版字符串 (e.g. "Values: {{$a}} and {{$b}}")</param>
    /// <param name="variables">命名参数字典</param>
    /// <returns>渲染后的纯字符串</returns>
    public static async Task<string> RenderAsync(string templateText, Dictionary<string, object?> variables)
    {
        if (string.IsNullOrWhiteSpace(templateText)) return string.Empty;

        // 1. 将普通字典转换为 SK 的 KernelArguments
        var arguments = new KernelArguments(variables);

        // 2. 创建模版配置
        var config = new PromptTemplateConfig(templateText);

        // 3. 创建模版实例
        // SK 的工厂会解析模版语法
        var template = _templateFactory.Create(config);

        // 4. 执行渲染
        // 这里传入 _emptyKernel 是必须的，但因为它不包含 Service，所以没有任何网络请求开销
        string renderedPrompt = await template.RenderAsync(_emptyKernel, arguments);

        return renderedPrompt;
    }
}