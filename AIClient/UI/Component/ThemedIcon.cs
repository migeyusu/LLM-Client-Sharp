using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LLMClient.UI.Component;

public abstract class ThemedIcon : BaseViewModel
{
    private static readonly Lazy<ThemedIcon> EmptyIconLazy = new Lazy<ThemedIcon>((() =>
    {
        var empty = new BitmapImage();
        empty.Freeze();
        return new LocalThemedIcon(empty);
    }));

    public static ThemedIcon EmptyIcon
    {
        get { return EmptyIconLazy.Value; }
    }

    private bool _isDarkModeSupported;

    public bool IsDarkModeSupported
    {
        get => _isDarkModeSupported;
        set
        {
            if (_isDarkModeSupported != value)
            {
                _isDarkModeSupported = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentSource));
            }
        }
    }

    // 当前应该显示的图标资源
    public ImageSource CurrentSource => GetCurrentSource();

    protected ImageSource LightModeSource;

    protected ImageSource? DarkModeSource;

    public ThemedIcon(ImageSource lightModeSource, bool isDarkModeSupported, ImageSource? darkModeSource)
    {
        LightModeSource = lightModeSource;
        _isDarkModeSupported = isDarkModeSupported;
        DarkModeSource = darkModeSource;
        // 订阅主题变更事件
        UITheme.ModeChanged += (_) => OnPropertyChanged(nameof(CurrentSource));
    }

    protected virtual ImageSource GetCurrentSource()
    {
        if (!IsDarkModeSupported || DarkModeSource == null)
            return LightModeSource;
        return UITheme.IsDarkMode ? DarkModeSource : LightModeSource;
    }

    // 允许隐式转换为ImageSource，便于在某些场景直接使用
    public static implicit operator ImageSource(ThemedIcon icon) => icon.CurrentSource;

    public static explicit operator ThemedIcon(ImageSource imageSource) => new LocalThemedIcon(imageSource);
}

public class LocalThemedIcon : ThemedIcon
{
    public LocalThemedIcon(ImageSource lightModeSource, ImageSource? darkModeSource = null) :
        base(lightModeSource, darkModeSource != null, darkModeSource)
    {
    }
}

public class AsyncThemedIcon : ThemedIcon
{
    public AsyncThemedIcon(Task<ImageSource> lightModeSourceTask, Task<ImageSource>? darkModeSourceTask)
        : base(lightModeSourceTask.IsCompleted ? lightModeSourceTask.Result : EmptyIcon,
            darkModeSourceTask != null,
            darkModeSourceTask?.IsCompleted == true ? darkModeSourceTask.Result : EmptyIcon)
    {
        UpdateSource(lightModeSourceTask, darkModeSourceTask);
    }

    private async void UpdateSource(Task<ImageSource> lightModeSourceTask, Task<ImageSource>? darkModeSourceTask)
    {
        if (LightModeSource.Equals(EmptyIcon.CurrentSource))
        {
            this.LightModeSource = await lightModeSourceTask;
        }

        if (darkModeSourceTask != null)
        {
            this.DarkModeSource = await darkModeSourceTask;
        }

        OnPropertyChangedAsync(nameof(CurrentSource));
    }
}