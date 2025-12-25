using Microsoft.KernelMemory.AI;

namespace LLMClient.Abstraction;

public interface ITokensCounter
{
    /// <summary>
    /// 计算Tokens数量
    /// </summary>
    /// <returns>Tokens数量</returns>
    Task<long> CountTokens(string text);
}

public class DefaultTokensCounter : ITokensCounter
{
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
            return (long)(text.Length / 2.8f);
        }
    }
}