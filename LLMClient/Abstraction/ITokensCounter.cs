using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.AI;
using Microsoft.KernelMemory.AI;

namespace LLMClient.Abstraction;

public interface ITokensCounter
{
    /// <summary>
    /// 计算Tokens数量
    /// </summary>
    /// <returns>Tokens数量</returns>
    Task<long> CountTokens(string text);

    Task<long> CountTokens(IReadOnlyList<ChatMessage> messages);
}

public class DefaultTokensCounter : ITokensCounter
{
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "get_DebuggerDisplay")]
    public static extern string GetDebuggerDisplay(FunctionCallContent content);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "get_DebuggerDisplay")]
    public static extern string GetDebuggerDisplay(FunctionResultContent content);

#pragma warning disable KMEXP00
    private readonly ITextTokenizer _tokenizer = new GPT4Tokenizer();
#pragma warning restore KMEXP00
    public async Task<long> CountTokens(string text)
    {
        try
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }

            return await Task.Run(() => _tokenizer.CountTokens(text));
        }
        catch (Exception e)
        {
            Trace.TraceWarning("计算Tokens数量失败，使用估算方法：" + e);
            return (long)(text.Length / 2.8f);
        }
    }

    /// <summary>
    /// 由于各种历史压缩策略和剔除策略，token的历史记录不可靠，加上不同模型的tokenizer的不同，衡量tokens非常不可靠
    /// </summary>
    public Task<long> CountTokens(IReadOnlyList<ChatMessage> messages)
    {
        return Task.Run(() =>
        {
            var contentBuilder = new StringBuilder();
            foreach (var message in messages)
            {
                foreach (var content in message.Contents)
                {
                    switch (content)
                    {
                        case TextContent textContent:
                            contentBuilder.Append(textContent.Text);
                            break;
                        case TextReasoningContent reasoningContent:
                            contentBuilder.Append(reasoningContent.Text);
                            break;
                        case FunctionCallContent functionCallContent:
                            contentBuilder.Append(GetDebuggerDisplay(functionCallContent));
                            break;
                        case FunctionResultContent functionResultContent:
                            contentBuilder.Append(GetDebuggerDisplay(functionResultContent));
                            break;
                    }
                }
            }

            var text = contentBuilder.ToString();
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }

            try
            {
                return _tokenizer.CountTokens(text);
            }
            catch (Exception e)
            {
                Trace.TraceWarning("计算Tokens数量失败，使用估算方法：" + e);
                return (long)(text.Length / 2.8f);
            }
        });
    }
}