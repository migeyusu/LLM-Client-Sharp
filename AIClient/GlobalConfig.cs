using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using LLMClient.Render;
using LLMClient.UI;
using LLMClient.UI.Component;
using Microsoft.Xaml.Behaviors.Core;
using TextMateSharp.Grammars;

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
            using (var fileStream = fileInfo.OpenRead())
            {
                var config = JsonSerializer.Deserialize<GlobalConfig>(fileStream);
                if (config != null)
                {
                    return config;
                }
            }
        }

        return new GlobalConfig();
    }
}