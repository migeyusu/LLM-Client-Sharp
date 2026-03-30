using System.Diagnostics;
using System.Text.Json;

namespace LLMClient.Test;

public class ProcessStartInfoEnvironmentTests
{
    private sealed record ProbeResult(string? cwd, string? script, string[] argv, Dictionary<string, string?> env);

    [Fact]
    public async Task ProcessStartInfo_DefaultLaunch_InheritsParentEnvironmentVariables()
    {
        var sentinelName = $"LLMCLIENT_PARENT_{Guid.NewGuid():N}";
        var sentinelValue = $"parent-{Guid.NewGuid():N}";
        Environment.SetEnvironmentVariable(sentinelName, sentinelValue);

        try
        {
            var result = await RunPythonProbeAsync(startInfo =>
            {
                startInfo.UseShellExecute = false;
            }, sentinelName);

            Assert.True(result.env.ContainsKey(sentinelName));
            Assert.Equal(sentinelValue, result.env[sentinelName]);
        }
        finally
        {
            Environment.SetEnvironmentVariable(sentinelName, null);
        }
    }

    [Fact]
    public async Task ProcessStartInfo_ExplicitEnvironment_PreservesParentAndAppliesOverrides()
    {
        var inheritedName = $"LLMCLIENT_INHERITED_{Guid.NewGuid():N}";
        var inheritedValue = $"parent-{Guid.NewGuid():N}";
        var overriddenName = $"LLMCLIENT_OVERRIDE_{Guid.NewGuid():N}";
        var parentOverrideValue = $"parent-{Guid.NewGuid():N}";
        var childOverrideValue = $"child-{Guid.NewGuid():N}";
        var addedName = $"LLMCLIENT_ADDED_{Guid.NewGuid():N}";
        var addedValue = $"added-{Guid.NewGuid():N}";

        Environment.SetEnvironmentVariable(inheritedName, inheritedValue);
        Environment.SetEnvironmentVariable(overriddenName, parentOverrideValue);

        try
        {
            var result = await RunPythonProbeAsync(startInfo =>
            {
                startInfo.UseShellExecute = false;
                startInfo.Environment[overriddenName] = childOverrideValue;
                startInfo.Environment[addedName] = addedValue;
            }, inheritedName, overriddenName, addedName);

            Assert.Equal(inheritedValue, result.env[inheritedName]);
            Assert.Equal(childOverrideValue, result.env[overriddenName]);
            Assert.Equal(addedValue, result.env[addedName]);
        }
        finally
        {
            Environment.SetEnvironmentVariable(inheritedName, null);
            Environment.SetEnvironmentVariable(overriddenName, null);
        }
    }

    [Fact]
    public async Task ProcessStartInfo_WorkingDirectory_ChangesCwdButNotScriptPath()
    {
        var workingDirectory = Directory.CreateTempSubdirectory();
        try
        {
            var result = await RunPythonProbeAsync(startInfo =>
            {
                startInfo.UseShellExecute = false;
                startInfo.WorkingDirectory = workingDirectory.FullName;
            }, "PATH");

            Assert.Equal(workingDirectory.FullName, result.cwd);
            Assert.NotNull(result.script);
            Assert.NotEqual(workingDirectory.FullName, Path.GetDirectoryName(result.script));
        }
        finally
        {
            workingDirectory.Delete(true);
        }
    }

    [Fact]
    public async Task ProcessStartInfo_EnvironmentCollection_IsPrepopulatedFromParentProcess()
    {
        var sentinelName = $"LLMCLIENT_PREPOPULATED_{Guid.NewGuid():N}";
        var sentinelValue = $"prepopulated-{Guid.NewGuid():N}";
        Environment.SetEnvironmentVariable(sentinelName, sentinelValue);

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "python",
                UseShellExecute = false,
            };

            Assert.True(startInfo.Environment.ContainsKey(sentinelName));
            Assert.Equal(sentinelValue, startInfo.Environment[sentinelName]);
            Assert.True(startInfo.Environment.ContainsKey("PATH"));
        }
        finally
        {
            Environment.SetEnvironmentVariable(sentinelName, null);
        }
    }

    private static async Task<ProbeResult> RunPythonProbeAsync(Action<ProcessStartInfo> configure, params string[] variableNames)
    {
        var probeDirectory = Directory.CreateTempSubdirectory();
        try
        {
            var scriptPath = Path.Combine(probeDirectory.FullName, "env_probe.py");
            await File.WriteAllTextAsync(scriptPath, BuildProbeScript(variableNames));

            var startInfo = new ProcessStartInfo
            {
                FileName = "python",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            startInfo.ArgumentList.Add(scriptPath);
            configure(startInfo);

            using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start python process.");
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            var stdout = await stdoutTask;
            var stderr = await stderrTask;
            Assert.True(process.ExitCode == 0, $"python exited with {process.ExitCode}\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");

            var result = JsonSerializer.Deserialize<ProbeResult>(stdout);
            Assert.NotNull(result);
            return result!;
        }
        finally
        {
            probeDirectory.Delete(true);
        }
    }

    private static string BuildProbeScript(IEnumerable<string> variableNames)
    {
        var variableList = string.Join(", ", variableNames.Select(name => JsonSerializer.Serialize(name)));
        return $$"""
import json
import os
import sys

variable_names = [{{variableList}}]
payload = {
    "cwd": os.getcwd(),
    "script": os.path.abspath(__file__),
    "argv": sys.argv,
    "env": {name: os.environ.get(name) for name in variable_names}
}
print(json.dumps(payload, ensure_ascii=False))
""";
    }
}
