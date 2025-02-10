using System.IO;
using System.Text.Json;
using System.Windows.Input;
using Microsoft.Xaml.Behaviors.Core;
using TextMateSharp.Grammars;

namespace LLMClient;

public class GlobalConfig : BaseViewModel
{
    private ThemeName _themeName = ThemeName.Light;

    public ThemeName ThemeName
    {
        get => _themeName;
        set
        {
            if (value == _themeName) return;
            _themeName = value;
            OnPropertyChanged();
        }
    }

    private const string DEFAULT_GLOBAL_CONFIG_FILE = "globalconfig.json";

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