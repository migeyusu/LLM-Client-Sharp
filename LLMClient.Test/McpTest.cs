using System.Diagnostics;
using System.Text;
using LLMClient.ToolCall;
using ModelContextProtocol.Client;
using Xunit.Abstractions;

namespace LLMClient.Test;

public class McpTest
{
    private readonly ITestOutputHelper _output;

    public McpTest(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task ConnectRider()
    {
        var httpClient = new HttpClient(new CustomHttpHandler(new HttpClientHandler())
        {
            BufferedRequest = true,
            RemoveCharSet = true,
        });
        var client = await McpClient.CreateAsync(new HttpClientTransport(new HttpClientTransportOptions()
        {
            Name = "Rider MCP",
            Endpoint = new Uri("http://localhost:64342/sse"),
            TransportMode = HttpTransportMode.Sse,
            AdditionalHeaders = new Dictionary<string, string>
            {
                ["IJ_MCP_SERVER_PROJECT_PATH"] = @"E:/OpenSource/LLM-Client-Sharp/LLM-Client-Sharp/",
            }
        }, httpClient));
        var clientTools = await client.ListToolsAsync();
        var stringBuilder = new StringBuilder();
        foreach (var mcpClientTool in clientTools)
        {
            stringBuilder.AppendLine("Name: " + mcpClientTool.Name);
            // stringBuilder.AppendLine("Description: " + mcpClientTool.Description);
            stringBuilder.AppendLine("Parameters: " + mcpClientTool.JsonSchema);
            stringBuilder.AppendLine("Returns: " + mcpClientTool.ReturnJsonSchema);
        }
        _output.WriteLine(stringBuilder.ToString());
        /*

        ValueKind = Object : "{"properties":{"directoryPath":{"type":"string","description":"Path relative to the project root"},"maxDepth":{"type":"integer","description":"Maximum recursion depth"},"timeout":{"type":"integer","description":"Timeout in milliseconds"},"projectPath":{"type":"string","description":" The project path. Pass this value ALWAYS if you are aware of it. It reduces numbers of ambiguous calls. \n In the case you know only the current working directory you can use it as the project path.\n If you\u0027re not aware about the project path you can ask user about it."}},"required":["directoryPath"],"type":"object"}"
         */
        var toolResult = await clientTools.First((tool => tool.Name == "get_symbol_info"))
            .CallAsync(new Dictionary<string, object?>()
            {
                ["directoryPath"] = ".",
                ["timeout"] = 5000,
                ["projectPath"] = @"E:/OpenSource/LLM-Client-Sharp/LLM-Client-Sharp/"
            });
        _output.WriteLine(clientTools.Count.ToString());
    }

    [Fact]
    public void TestJavaCommand()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = @"D:\Program Files\JetBrains Rider 2025.2.3\jbr\bin\java.exe",
            Arguments = "-version",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        process.WaitForExit();

        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();

        _output.WriteLine($"Exit Code: {process.ExitCode}");
        _output.WriteLine($"Output: {output}");
        _output.WriteLine($"Error: {error}");
    }
}