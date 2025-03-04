using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Input;
using LLMClient.UI;
using Microsoft.Xaml.Behaviors.Core;

namespace LLMClient;

public class GlobalConfig : BaseViewModel
{
    private const string DEFAULT_GLOBAL_CONFIG_FILE = "globalconfig.json";

    [JsonIgnore]
    public ICommand SaveCommand => new ActionCommand(async (param) =>
    {
        var fileInfo = new FileInfo(DEFAULT_GLOBAL_CONFIG_FILE);
        fileInfo.Directory?.Create();
        using (var fileStream = fileInfo.Open(FileMode.OpenOrCreate, FileAccess.Write))
        {
            await JsonSerializer.SerializeAsync(fileStream, this);
        }
    });

    public static GlobalConfig LoadOrCreate()
    {
        var fileInfo = new FileInfo(DEFAULT_GLOBAL_CONFIG_FILE);
        if (fileInfo.Exists)
        {
            try
            {
                using (var fileStream = fileInfo.OpenRead())
                {
                    var config = JsonSerializer.Deserialize<GlobalConfig>(fileStream);
                    if (config != null)
                    {
                        return config;
                    }
                }
            }
            catch (Exception e)
            {
                Trace.TraceError(e.Message);
            }
        }

        return new GlobalConfig();
    }
}