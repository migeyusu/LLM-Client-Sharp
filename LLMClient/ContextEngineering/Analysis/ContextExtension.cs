using System.Diagnostics;
using Microsoft.Build.Locator;

namespace LLMClient.ContextEngineering.Analysis;

public static class AnalyzerExtension
{
    public static void RegisterMsBuild()
    {
        if (MSBuildLocator.IsRegistered) return;

        // 方式 A：自动挑最新
        var instance = MSBuildLocator.QueryVisualStudioInstances()
            .OrderByDescending(i => i.Version)
            .FirstOrDefault();

        if (instance != null)
        {
            MSBuildLocator.RegisterInstance(instance);
            Trace.WriteLine($"MSBuild 注册成功，来源：{instance.MSBuildPath}");
        }
        else
        {
            // 没装 VS，仅有 .NET SDK 时也可以：
            MSBuildLocator.RegisterDefaults();
            Trace.WriteLine("MSBuild 注册成功，使用 .NET SDK 内置 MSBuild。");
        }
    }
}