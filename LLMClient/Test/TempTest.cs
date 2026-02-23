using System.Diagnostics;
using LLMClient.ToolCall.DefaultPlugins;
using LLMClient.ToolCall.Servers;
using Microsoft.SemanticKernel.Data;

namespace LLMClient.Test;

public class TempTest
{
    public async Task TestMethod()
    {
        var googleSearchPlugin = new GoogleSearchPlugin();
        await googleSearchPlugin.EnsureAsync(CancellationToken.None);
        var searchResult = await googleSearchPlugin.QueryAsync("最新的科技新闻", new TextSearchOptions()
        {
            Skip = 0,
            Top = 10,
        }, CancellationToken.None);
        if (searchResult is SKTextSearchResult skTextSearchResult)
        {
            var results = await skTextSearchResult.Results.ToArrayAsync();
            Debugger.Break();
        }
    }
}