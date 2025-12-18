using System.Diagnostics;
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
        var httpClient = new HttpClient();
        var client = await McpClient.CreateAsync(new HttpClientTransport(new HttpClientTransportOptions()
        {
            Name = "Rider MCP",
            Endpoint = new Uri("http://localhost:64342/sse"),
            TransportMode = HttpTransportMode.Sse,
            /*AdditionalHeaders = new Dictionary<string, string>
            {
                ["IJ_MCP_SERVER_PROJECT_PATH"] = "/e/OpenSource/LLM-Client-Sharp/LLM-Client-Sharp/",
            }*/
        }, httpClient));
        /*string riderBasePath = @"D:\Program Files\JetBrains Rider 2025.2.3";

        // 构建 JAR 列表
        var jars = new[]
        {
            $@"{riderBasePath}\plugins\mcpserver\lib\mcpserver-frontend.jar",
            $@"{riderBasePath}\lib\util-8.jar",
            $@"{riderBasePath}\lib\module-intellij.libraries.ktor.client.cio.jar",
            $@"{riderBasePath}\lib\module-intellij.libraries.ktor.client.jar",
            $@"{riderBasePath}\lib\module-intellij.libraries.ktor.network.tls.jar",
            $@"{riderBasePath}\lib\module-intellij.libraries.ktor.io.jar",
            $@"{riderBasePath}\lib\module-intellij.libraries.ktor.utils.jar",
            $@"{riderBasePath}\lib\module-intellij.libraries.kotlinx.io.jar",
            $@"{riderBasePath}\lib\module-intellij.libraries.kotlinx.serialization.core.jar",
            $@"{riderBasePath}\lib\module-intellij.libraries.kotlinx.serialization.json.jar"
        };

        // 每个路径加引号，然后用分号连接
        string classpath = string.Join(";", jars.Select(j => $"\"{j}\""));

        var client = await McpClient.CreateAsync(new StdioClientTransport(new StdioClientTransportOptions()
        {
            Command = @"D:\Program Files\JetBrains Rider 2025.2.3\jbr\bin\java.exe",

            Arguments = new[]
            {
                "-classpath",
                classpath,
                "com.intellij.mcpserver.stdio.McpStdioRunnerKt"
            },
            EnvironmentVariables = new Dictionary<string, string?>()
            {
                {
                    "IJ_MCP_SERVER_PORT", "64342"
                }
            }
        }));*/
        var clientTools = await client.ListToolsAsync();
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