using System.Diagnostics;
using Xunit.Abstractions;

namespace LLMClient.Test;

public class UnitTest1
{
    private ITestOutputHelper output;

    public UnitTest1(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Fact]
    public async void Test1()
    {
        var task = Task.Run((async () => await Task.Delay(1)));
        await Task.Delay(10);
        output.WriteLine($"{task.IsCompleted}");
    }
}