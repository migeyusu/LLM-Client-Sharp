using System.Diagnostics;
using LLMClient.ContextEngineering;
using LLMClient.ContextEngineering.Analysis;
using LLMClient.ContextEngineering.Prompt;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace LLMClient.Test;

public class PromptContext
{
    private AnalyzerConfig _analyzerConfig;

    private ITestOutputHelper _output;

    const string solutionPath = @"E:\OpenSource\LLM-Client-Sharp\LLM-Client-Sharp\AIClient.sln";
    

    public PromptContext(ITestOutputHelper output)
    {
        _output = output;
        AnalyzerExtension.RegisterMsBuild();
        // 配置
        _analyzerConfig = new AnalyzerConfig
        {
            IncludeTestProjects = true,
            IncludePrivateMembers = true,
            MaxConcurrency = 4
        };
    }

    [Fact]
    public async Task FileTree()
    {
        using var analyzer = new RoslynProjectAnalyzer(null, _analyzerConfig);
        var projectInfo = await analyzer.AnalyzeSolutionAsync(solutionPath);
        var fileTreeFormatter = new FileTreeFormatter();
        var format = fileTreeFormatter.Format(projectInfo);
        var outputPath = "filetree.md";
        await File.WriteAllTextAsync(outputPath, format);
        _output.WriteLine($"File tree written to {outputPath}");
    }


    [Fact]
    public async Task TestBuild()
    {
        // 日志
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging((builder => builder.AddConsole()));
        await using (var buildServiceProvider = serviceCollection.BuildServiceProvider())
        {
            var iLogger = buildServiceProvider.GetService<ILogger<RoslynProjectAnalyzer>>()!;
            // 缓存管理
            // var cacheManager = new InfoCacheManager();
            // 分析
            using var analyzer = new RoslynProjectAnalyzer(iLogger, _analyzerConfig);

            var stopwatch = new Stopwatch();
            stopwatch.Start();
            var summary = await analyzer.AnalyzeSolutionAsync(solutionPath);
            _output.WriteLine($"First analysis completed in {stopwatch.ElapsedMilliseconds}ms");
            stopwatch.Restart();
            /*var summary = await cacheManager.GetOrGenerateAsync(
                solutionPath,
                async () => await analyzer.AnalyzeSolutionAsync(solutionPath)
            );*/
            stopwatch.Stop();
            _output.WriteLine($"Second Analysis completed in {stopwatch.ElapsedMilliseconds}ms");
            // 格式化输出
            var formatterOptions = new FormatterOptions
            {
                IncludeMembers = false,
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