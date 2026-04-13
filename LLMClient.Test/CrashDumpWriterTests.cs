using LLMClient.Log;

namespace LLMClient.Test;

public class CrashDumpWriterTests
{
    [Fact]
    public void TryWriteCurrentProcessDump_ShouldCreateDumpFile()
    {
        var logRoot = Path.Combine(Path.GetTempPath(), "LLMClient.CrashDumpWriterTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(logRoot);

        try
        {
            var succeeded = CrashDumpWriter.TryWriteCurrentProcessDump(logRoot, out var dumpPath, out var error);

            Assert.True(succeeded, error?.ToString());
            Assert.NotNull(dumpPath);
            Assert.True(File.Exists(dumpPath));
            Assert.True(new FileInfo(dumpPath).Length > 0);
        }
        finally
        {
            if (Directory.Exists(logRoot))
            {
                Directory.Delete(logRoot, true);
            }
        }
    }
}

