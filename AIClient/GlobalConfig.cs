using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using LLMClient.Render;
using Microsoft.Xaml.Behaviors.Core;
using TextMateSharp.Grammars;

namespace LLMClient;

public class GlobalConfig : BaseViewModel
{
    private const string ThemeColorResourceKey = "CodeBlock.TextMateSharp.Theme";

    private ThemeName _themeName = ThemeName.Light;

    public ThemeName ThemeName
    {
        get => _themeName;
        set
        {
            if (value == _themeName) return;
            _themeName = value;
            OnPropertyChanged();
            UITheme.ThemeName = TextMateCodeRenderer.GetTheme(value);
            // UpdateResource(value);
        }
    }

    public static void UpdateResource(ThemeName themeName)
    {
        var application = Application.Current;
        if (application == null)
        {
            return;
        }

        var sourceDictionary = application.Resources;
        sourceDictionary[ThemeColorResourceKey] = TextMateCodeRenderer.GetTheme(themeName);
    }

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