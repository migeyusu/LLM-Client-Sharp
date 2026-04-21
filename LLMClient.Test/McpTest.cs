using System.Diagnostics;
using System.Text;
using LLMClient.ToolCall;
using LLMClient.ToolCall.MCP;
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
        if (process == null)
        {
            return;
        }

        process.WaitForExit();

        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();

        _output.WriteLine($"Exit Code: {process.ExitCode}");
        _output.WriteLine($"Output: {output}");
        _output.WriteLine($"Error: {error}");
    }

    [Fact]
    public async Task CreateTransportOptions_PythonScriptCommand_WrapsScriptAndSetsDefaults()
    {
        var tempDirectory = Directory.CreateTempSubdirectory();
        try
        {
            var scriptPath = Path.Combine(tempDirectory.FullName, "word_mcp_server.py");
            await File.WriteAllTextAsync(scriptPath, "print('startup log')");

            var stdIoServerItem = new StdIOServerItem()
            {
                Name = "word",
                Command = "\"python\"",
                Argument = new[] { $"\"{scriptPath}\"", "--port", "9000" }
            };

            var options = stdIoServerItem.CreateTransportOptions();

            Assert.Equal(tempDirectory.FullName, options.WorkingDirectory);
            Assert.NotNull(options.Arguments);

            if (OperatingSystem.IsWindows())
            {
                Assert.Equal("powershell.exe", options.Command);
                Assert.Collection(options.Arguments!,
                    arg => Assert.Equal("-NoProfile", arg),
                    arg => Assert.Equal("-Command", arg),
                    arg =>
                    {
                        Assert.Contains("mcp_python_stdio_bootstrap.py", arg);
                        Assert.Contains(scriptPath, arg);
                        Assert.Contains("--port", arg);
                        Assert.Contains("9000", arg);

                        var bootstrapPath = Path.Combine(Extension.TempPath, "mcp_python_stdio_bootstrap.py");
                        Assert.True(File.Exists(bootstrapPath));
                        Assert.Contains("exec(compile", File.ReadAllText(bootstrapPath));
                    });
            }
            else
            {
                Assert.Equal("python", options.Command);
                Assert.Collection(options.Arguments!,
                    arg => Assert.Equal("-u", arg),
                    arg =>
                    {
                        Assert.EndsWith("mcp_python_stdio_bootstrap.py", arg);
                        Assert.True(File.Exists(arg));
                        Assert.Contains("exec(compile", File.ReadAllText(arg));
                    },
                    arg => Assert.Equal(scriptPath, arg),
                    arg => Assert.Equal("--port", arg),
                    arg => Assert.Equal("9000", arg));
            }

            Assert.NotNull(options.EnvironmentVariables);
            Assert.Equal("stdio", options.EnvironmentVariables!["MCP_TRANSPORT"]);
            Assert.Equal("1", options.EnvironmentVariables["PYTHONUNBUFFERED"]);
        }
        finally
        {
            tempDirectory.Delete(true);
        }
    }

    [Fact]
    public async Task CreateTransportOptions_PythonScriptCommand_PreservesExplicitWorkingDirectoryAndEnvironment()
    {
        var tempDirectory = Directory.CreateTempSubdirectory();
        var workingDirectory = Directory.CreateTempSubdirectory();
        try
        {
            var scriptPath = Path.Combine(tempDirectory.FullName, "server.py");
            await File.WriteAllTextAsync(scriptPath, "print('startup log')");

            var stdIoServerItem = new StdIOServerItem()
            {
                Name = "word",
                Command = "python.exe",
                WorkingDirectory = $"\"{workingDirectory.FullName}\"",
                Argument = new[] { scriptPath },
                EnvironmentVariable = new[]
                {
                    new VariableItem() { Name = "MCP_TRANSPORT", Value = "sse" },
                    new VariableItem() { Name = "PYTHONUNBUFFERED", Value = "0" },
                    new VariableItem() { Name = " CUSTOM_FLAG ", Value = "yes" }
                }
            };

            var options = stdIoServerItem.CreateTransportOptions();

            Assert.Equal(workingDirectory.FullName, options.WorkingDirectory);
            Assert.Equal(OperatingSystem.IsWindows() ? "powershell.exe" : "python.exe", options.Command);
            Assert.NotNull(options.EnvironmentVariables);
            Assert.Equal("stdio", options.EnvironmentVariables!["MCP_TRANSPORT"]);
            Assert.Equal("1", options.EnvironmentVariables["PYTHONUNBUFFERED"]);
            Assert.Equal("yes", options.EnvironmentVariables["CUSTOM_FLAG"]);
        }
        finally
        {
            tempDirectory.Delete(true);
            workingDirectory.Delete(true);
        }
    }

    [Fact]
    public void CreateTransportOptions_NonPythonCommand_OnlyNormalizesQuotedTokens()
    {
        var stdIoServerItem = new StdIOServerItem()
        {
            Name = "node-server",
            Command = "\"node\"",
            WorkingDirectory = "\"C:\\Temp\\mcp\"",
            Argument = new[] { "\"server.js\"", "--stdio" }
        };

        var options = stdIoServerItem.CreateTransportOptions();

        Assert.Equal("node", options.Command);
        Assert.Equal(@"C:\Temp\mcp", options.WorkingDirectory);
        Assert.NotNull(options.Arguments);
        Assert.Equal(new[] { "server.js", "--stdio" }, options.Arguments);
        Assert.Null(options.EnvironmentVariables);
    }
}