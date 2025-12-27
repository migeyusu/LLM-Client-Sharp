using LLMClient.Endpoints;
using Microsoft.Extensions.AI;
using Xunit.Abstractions;

namespace LLMClient.Test;

public class JsonSerializationTest
{
    private ITestOutputHelper _testOutputHelper;

    public JsonSerializationTest(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    [Fact]
    public void FunctionCall()
    {
        var content = new FunctionResultContent("dsaf", null)
            { Exception = new LlmBadRequestException("test exception") };
        var json = System.Text.Json.JsonSerializer.Serialize(content,
            LLMClient.Extension.DefaultJsonSerializerOptions);
        _testOutputHelper.WriteLine(json);
    }
}