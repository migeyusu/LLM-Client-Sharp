namespace LLMClient;

public static class UITheme
{
    private static bool _isDarkMode = false;

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

    public static event Action<bool>? ModeChanged;

    private static void OnModeChanged(bool obj)
    {
        ModeChanged?.Invoke(obj);
    }
}