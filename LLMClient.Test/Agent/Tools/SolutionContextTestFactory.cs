using AutoMapper;
using LLMClient.ContextEngineering.Analysis;
using Microsoft.Extensions.Logging.Abstractions;

namespace LLMClient.Test.Agent.Tools;

internal static class SolutionContextTestFactory
{
    public static SolutionContext CreateEmpty()
    {
        var mapper = new MapperConfiguration(
            cfg => cfg.AddProfile<RoslynMappingProfile>(),
            NullLoggerFactory.Instance).CreateMapper();
        var analyzer = new RoslynProjectAnalyzer(logger: null, mapper: mapper);

        return new SolutionContext(analyzer);
    }

    public static SolutionContext CreateLoaded(SolutionInfo solutionInfo)
    {
        var context = CreateEmpty();
        context.SetForTesting(solutionInfo);
        return context;
    }
}

