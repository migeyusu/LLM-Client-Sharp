using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.PromptTemplates.Handlebars;

namespace LLMClient.ContextEngineering.Prompt;

public static class PromptTemplateRenderer
{
    private static readonly Kernel _emptyKernel = new Kernel();
    private static readonly HandlebarsPromptTemplateFactory _handlebarsFactory = new HandlebarsPromptTemplateFactory();
    private static readonly KernelPromptTemplateFactory _defaultFactory = new KernelPromptTemplateFactory();

    /// <summary>
    /// 使用 Handlebars 渲染（支持原始输出，不转义）
    /// 变量语法：{{{variable}}} - 不转义，{{variable}} - 转义
    /// </summary>
    public static async Task<string> RenderHandlebarsAsync(string templateText, Dictionary<string, object?> variables)
    {
        if (string.IsNullOrWhiteSpace(templateText)) return string.Empty;

        var arguments = new KernelArguments(variables);
        var config = new PromptTemplateConfig(templateText)
        {
            TemplateFormat = HandlebarsPromptTemplateFactory.HandlebarsTemplateFormat
        };

        var template = _handlebarsFactory.Create(config);
        return await template.RenderAsync(_emptyKernel, arguments);
    }

    /// <summary>
    /// 使用 SK 默认语法渲染（会转义特殊字符）
    /// 变量语法：{{$variable}}
    /// </summary>
    public static async Task<string> RenderAsync(string templateText, Dictionary<string, object?> variables)
    {
        if (string.IsNullOrWhiteSpace(templateText)) return string.Empty;

        var arguments = new KernelArguments(variables);
        var config = new PromptTemplateConfig(templateText);
        var template = _defaultFactory.Create(config);
        return await template.RenderAsync(_emptyKernel, arguments);
    }
}