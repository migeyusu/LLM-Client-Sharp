using LLMClient.Component.Render;
using Markdig;
using Markdig.Syntax;
using Microsoft.Extensions.AI;

namespace LLMClient.Test;

public class ToolCallStreamingFormatTests
{
    [Fact]
    public void FunctionCallXmlFragment_CanBeParsedByToolCallBlockParser()
    {
        var functionCallContent = new FunctionCallContent("call-1", "search_files", new Dictionary<string, object?>
        {
            ["query"] = "a < b & c",
            ["limit"] = 2,
            ["includeHidden"] = true,
        });

        var markdown = $"{ToolCallBlockParser.FunctionCallTag}{Environment.NewLine}{functionCallContent.ToToolCallXmlFragment()}{Environment.NewLine}{ToolCallBlockParser.FunctionCallEndTag}";
        var document = Markdown.Parse(markdown, CustomMarkdownRenderer.DefaultPipeline);

        var block = Assert.Single(document.Descendants().OfType<ToolCallBlock>());
        Assert.Null(block.Raw);

        Assert.NotNull(block.ToolCalls);
        var toolCalls = block.ToolCalls!;
        var toolCall = Assert.Single(toolCalls.ToolCalls);
        Assert.Equal("search_files", toolCall.Name);
        Assert.Contains("\"query\"", toolCall.Arguments);
        Assert.Contains("\\u003C", toolCall.Arguments);
        Assert.Contains("\\u0026", toolCall.Arguments);
        Assert.Contains("\"limit\": 2", toolCall.Arguments);
    }

    [Fact]
    public void FunctionResultXmlFragment_CanBeParsedByToolCallResultBlockParser_WhenExceptionContainsXmlSensitiveCharacters()
    {
        var functionResultContent = new FunctionResultContent("call-1", null)
        {
            Exception = new InvalidOperationException("bad < data > & more")
        };

        var markdown = $"{ToolCallResultBlockParser.FunctionResultTag}{Environment.NewLine}{functionResultContent.ToToolCallResultXmlFragment()}{Environment.NewLine}{ToolCallResultBlockParser.FunctionResultEndTag}";
        var document = Markdown.Parse(markdown, CustomMarkdownRenderer.DefaultPipeline);

        var block = Assert.Single(document.Descendants().OfType<ToolCallResultBlock>());
        Assert.Null(block.Raw);

        Assert.NotNull(block.ToolCallResults);
        var results = block.ToolCallResults!;
        var toolResult = Assert.Single(results.ToolCalls);
        Assert.Equal("call-1", toolResult.Name);
        Assert.Contains("InvalidOperationException", toolResult.ResultContent);
        Assert.Contains("bad < data > & more", toolResult.ResultContent);
    }

    [Fact]
    public void MarkdownParse_CanParseToolCallBlockWhenStructuredOutputStartsOnNewLine()
    {
        var functionCallContent = new FunctionCallContent("call-2", "read_file", new Dictionary<string, object?>
        {
            ["path"] = "README.md"
        });

        var markdown = string.Join(Environment.NewLine,
            "assistant text without trailing newline",
            string.Empty,
            ToolCallBlockParser.FunctionCallTag,
            functionCallContent.ToToolCallXmlFragment(),
            ToolCallBlockParser.FunctionCallEndTag);

        var document = Markdown.Parse(markdown, CustomMarkdownRenderer.DefaultPipeline);
        var block = Assert.Single(document.Descendants().OfType<ToolCallBlock>());
        Assert.NotNull(block.ToolCalls);
        var toolCalls = block.ToolCalls!;
        var toolCall = Assert.Single(toolCalls.ToolCalls);
        Assert.Equal("read_file", toolCall.Name);
    }
}

