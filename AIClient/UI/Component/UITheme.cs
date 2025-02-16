using LLMClient.Render;

namespace LLMClient.UI.Component;

public static class UITheme
{
    private static bool _isDarkMode = false;

    private static TextMateThemeColors _themeName =
        TextMateCodeRenderer.GetTheme(TextMateSharp.Grammars.ThemeName.Light);

    public static bool IsDarkMode
    {
        get => _isDarkMode;
        set
        {
            if (value == _isDarkMode)
            {
                return;
            }

            _isDarkMode = value;
            OnModeChanged(value);
        }
    }

    public static TextMateThemeColors ThemeName
    {
        get => _themeName;
        set
        {
            if (value == _themeName)
            {
                return;
            }

            _themeName = value;
        }
    }

    public static event Action<bool>? ModeChanged;
    

    private static void OnModeChanged(bool obj)
    {
        ModeChanged?.Invoke(obj);
    }
}