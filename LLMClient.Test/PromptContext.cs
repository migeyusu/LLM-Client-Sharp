using System.Diagnostics;
using LLMClient.ContextEngineering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace LLMClient.Test;

public class PromptContext
{
    private ITestOutputHelper _output;

    public PromptContext(ITestOutputHelper output)
    {
        _output = output;
    }


    [Fact]
    public async Task TestBuild()
    {
        ContextExtension.RegisterMSBuild();
        // 配置
        var config = new AnalyzerConfig
        {
            IncludeTestProjects = true,
            IncludePrivateMembers = true,
            MaxConcurrency = 4
        };

        // 日志
        ServiceCollection serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging((builder => builder.AddConsole()));
        await using (var buildServiceProvider = serviceCollection.BuildServiceProvider())
        {
            var iLogger = buildServiceProvider.GetService<ILogger<RoslynProjectAnalyzer>>()!;
            // 缓存管理
            var cacheManager = new InfoCacheManager();
            var solutionPath = @"E:\OpenSource\LLM-Client-Sharp\LLM-Client-Sharp\AIClient.sln";
            // 分析
            using var analyzer = new RoslynProjectAnalyzer(iLogger, config);
            var summary = await analyzer.AnalyzeProjectAsync(
                @"E:\OpenSource\LLM-Client-Sharp\LLM-Client-Sharp\LLMClient.Avalonia\LLMClient.Avalonia.csproj");
            _output.WriteLine(summary.Name);
            summary = await analyzer.AnalyzeProjectAsync(
                @"E:\OpenSource\LLM-Client-Sharp\LLM-Client-Sharp\LLMClient\LLMClient.csproj");
            _output.WriteLine(summary.Name);
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            summary = await analyzer.AnalyzeProjectAsync(
                @"E:\OpenSource\LLM-Client-Sharp\LLM-Client-Sharp\LLMClient\LLMClient.csproj");
            /*var summary = await cacheManager.GetOrGenerateAsync(
                solutionPath,
                async () => await analyzer.AnalyzeSolutionAsync(solutionPath)
            );*/
            stopwatch.Stop();
            _output.WriteLine($"Analysis completed in {stopwatch.ElapsedMilliseconds}ms");
            // 格式化输出
            var formatterOptions = new FormatterOptions
            {
                IncludeMembers = true,
                IncludePackages = true
            };

            var formatter = new MarkdownSummaryFormatter(formatterOptions);
            var output = formatter.Format(summary);

            // 输出到文件
            var outputPath = "summary.md";
            await File.WriteAllTextAsync(outputPath, output);
            _output.WriteLine($"Summary written to {outputPath}");
        }
    }
}