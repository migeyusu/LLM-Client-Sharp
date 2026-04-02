using Microsoft.Extensions.AI;
using System.Xml.Serialization;
using LLMClient.Endpoints;

namespace LLMClient.Test;

public class ToolCallStreamingFormatTests
{
    [Fact]
    public void FunctionCallXmlFragment_CanBeParsedAsToolCallXml()
    {
        var functionCallContent = new FunctionCallContent("call-1", "search_files", new Dictionary<string, object?>
        {
            ["query"] = "a < b & c",
            ["limit"] = 2,
            ["includeHidden"] = true,
        });

        var xml = $"<tool_calls>{functionCallContent.ToToolCallXmlFragment()}</tool_calls>";
        var serializer = new XmlSerializer(typeof(ToolCallsElement));
        using var reader = new StringReader(xml);
        var toolCalls = Assert.IsType<ToolCallsElement>(serializer.Deserialize(reader));
        var toolCall = Assert.Single(toolCalls.ToolCalls);
        Assert.Equal("search_files", toolCall.Name);
        Assert.Contains("\"query\"", toolCall.Arguments);
        Assert.Contains("\\u003C", toolCall.Arguments);
        Assert.Contains("\\u0026", toolCall.Arguments);
        Assert.Contains("\"limit\": 2", toolCall.Arguments);
    }

    [Fact]
    public void FunctionResultXmlFragment_CanBeParsedAsToolCallResultXml_WhenExceptionContainsXmlSensitiveCharacters()
    {
        var functionResultContent = new FunctionResultContent("call-1", null)
        {
            Exception = new InvalidOperationException("bad < data > & more")
        };

        var xml = $"<tool_call_results>{functionResultContent.ToToolCallResultXmlFragment()}</tool_call_results>";
        var serializer = new XmlSerializer(typeof(ToolCallResultsContainer));
        using var reader = new StringReader(xml);
        var results = Assert.IsType<ToolCallResultsContainer>(serializer.Deserialize(reader));
        var toolResult = Assert.Single(results.ToolCalls);
        Assert.Equal("call-1", toolResult.Name);
        Assert.Contains("InvalidOperationException", toolResult.ResultContent);
        Assert.Contains("bad < data > & more", toolResult.ResultContent);
    }

    [Fact]
    public void FunctionCallXmlFragment_PreservesStructuredArguments()
    {
        var functionCallContent = new FunctionCallContent("call-2", "read_file", new Dictionary<string, object?>
        {
            ["path"] = "README.md"
        });

        var xml = $"<tool_calls>{functionCallContent.ToToolCallXmlFragment()}</tool_calls>";
        var serializer = new XmlSerializer(typeof(ToolCallsElement));
        using var reader = new StringReader(xml);
        var toolCalls = Assert.IsType<ToolCallsElement>(serializer.Deserialize(reader));
        var toolCall = Assert.Single(toolCalls.ToolCalls);
        Assert.Equal("read_file", toolCall.Name);
        Assert.Contains("README.md", toolCall.Arguments);
    }
}

