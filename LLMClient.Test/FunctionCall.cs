using System.ComponentModel;
using LLMClient.ToolCall.DefaultPlugins;
using LLMClient.ToolCall.Servers;
using Microsoft.SemanticKernel;
using Xunit.Abstractions;

namespace LLMClient.Test;

public class FunctionCall
{
    private ITestOutputHelper _output;

    public FunctionCall(ITestOutputHelper output)
    {
        this._output = output;
    }

    [Fact]
    public void KernelFunction()
    {
        var fileSystemPlugin = new FileSystemPlugin();
        var kernelFunction = KernelFunctionFactory.CreateFromMethod(fileSystemPlugin.CreateDirectoryAsync);
        var s = kernelFunction.JsonSchema.ToString();
        _output.WriteLine("Kernel Function JSON Schema:");
        _output.WriteLine(s);
        _output.WriteLine("Function Name: " + kernelFunction.Name);
        _output.WriteLine("Function Description: " + kernelFunction.Description);
        _output.WriteLine("Plugin Name: " + kernelFunction.PluginName);

        var kernelPlugin = KernelPluginFactory.CreateFromObject(fileSystemPlugin);
        _output.WriteLine("Plugin Name: " + kernelPlugin.Name);
        _output.WriteLine("Plugin Description: " + kernelPlugin.Description);
        var firstOrDefault = kernelPlugin.FirstOrDefault((function => function.Name == kernelFunction.Name));
        Assert.NotNull(firstOrDefault);
        Assert.Equal(kernelFunction.Description, firstOrDefault.Description);
    }

    [Fact]
    public void KernelPluginDescription()
    {
        var fileSystemPlugin = new FileSystemPlugin();
        var kernelFunction = KernelFunctionFactory.CreateFromMethod(fileSystemPlugin.CreateDirectoryAsync);
        var fromFunctions = KernelPluginFactory.CreateFromFunctions("TestPlugin", "Test Plugin Description",
            new List<KernelFunction>
            {
                kernelFunction
            });
        _output.WriteLine("Plugin Name: " + fromFunctions.Name);
        _output.WriteLine("Plugin Description: " + fromFunctions.Description);
    }

    [Fact]
    public void WithResult()
    {
        var testPlugin = new TestPlugin();
        var kernelFunction = KernelFunctionFactory.CreateFromMethod(testPlugin.TestFunction);
        var s = kernelFunction.JsonSchema;
        _output.WriteLine("Kernel Function JSON Schema:");
        _output.WriteLine(s.ToString());
        var returnParameterSchema = kernelFunction.Metadata.ReturnParameter.Schema?.ToString();
        // Assert & Output
        _output.WriteLine("Kernel Function Return Value JSON Schema:");
        _output.WriteLine(returnParameterSchema);
    }

    [Fact]
    public async Task Call()
    {
        var testPlugin = new TestPlugin();
        var kernelFunction = KernelFunctionFactory.CreateFromMethod(testPlugin.TestFunction);
        var invokeAsync = await kernelFunction.InvokeAsync();
    }

    [Fact]
    public async Task WebFetch()
    {
        var webFetcherPlugin = new UrlFetcherPlugin();
        var fetchHtmlAsync = await webFetcherPlugin.FetchMarkdownAsync(
            "https://plugins.jetbrains.com/docs/intellij/tools-intellij-platform-gradle-plugin-plugins.html");
        _output.WriteLine(fetchHtmlAsync);
    }
}

public class TestPlugin
{
    [KernelFunction, Description("test function description")]
    public Task<ComplexResultClass> TestFunction()
    {
        return Task.FromResult(new ComplexResultClass() { Name = "sdadf", Age = 12 });
    }
}

public class ComplexResultClass
{
    [Description("name description")] public string Name { get; set; } = string.Empty;

    [Description("age description")] public int Age { get; set; }
}