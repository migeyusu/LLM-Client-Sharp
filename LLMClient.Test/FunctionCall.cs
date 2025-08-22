using System.ComponentModel;
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
    public void ComplexResult()
    {
        var testPlugin = new TestPlugin();
        var kernelFunction = KernelFunctionFactory.CreateFromMethod(testPlugin.TestFunction);
        var s = kernelFunction.JsonSchema.ToString();
        var returnParameterSchema = kernelFunction.Metadata.ReturnParameter.Schema;
        var schemaJsonString = returnParameterSchema?.ToString();
        // Assert & Output
        _output.WriteLine("Kernel Function Return Value JSON Schema:");
        _output.WriteLine(schemaJsonString);
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
    [Description("name description")]
    public string Name { get; set; } = string.Empty;
    
    [Description("age description")]
    public int Age { get; set; }
}