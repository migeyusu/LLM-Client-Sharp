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

    [Fact]
    public async void GetGithubModels()
    {
        var httpClient = new HttpClient();
        using (var message = await httpClient.GetAsync("https://xiaoai.plus/api/user/models"))
        {
            message.EnsureSuccessStatusCode();
            await using (var stream = await message.Content.ReadAsStreamAsync())
            {
                using (var reader = new StreamReader(stream))
                {
                    var content = await reader.ReadToEndAsync();
                    output.WriteLine(content);
                }
            }
        }
    }
}