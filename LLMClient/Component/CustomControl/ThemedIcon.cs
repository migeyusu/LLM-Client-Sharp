using System.Windows.Media;
using System.Windows.Media.Imaging;
using LLMClient.Component.Utility;
using LLMClient.Component.ViewModel.Base;
using LLMClient.Data;
using MaterialDesignThemes.Wpf;

namespace LLMClient.Component.CustomControl;

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

    public ImageSource LightModeSource { get; protected set; }

    public ImageSource? DarkModeSource { get; protected set; }

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

    public static LocalThemedIcon FromPackIcon(PackIconKind kind)
    {
        var imageSource = kind.ToImageSource();
        var darkSource = kind.ToImageSource(foreground: Brushes.White, background: Brushes.Black);
        return new LocalThemedIcon(imageSource, darkSource);
    }
}

public class AsyncThemedIcon : ThemedIcon
{
    public AsyncThemedIcon(Func<Task<ImageSource>> lightModeSourceTask,
        Func<Task<ImageSource>>? darkModeSourceTask = null)
        : base(EmptyIcon, darkModeSourceTask != null, EmptyIcon)
    {
        UpdateSource(lightModeSourceTask, darkModeSourceTask);
    }

    private async void UpdateSource(Func<Task<ImageSource>> lightModeSourceTask,
        Func<Task<ImageSource>>? darkModeSourceTask)
    {
        if (LightModeSource.Equals(EmptyIcon.CurrentSource))
        {
            this.LightModeSource = await lightModeSourceTask();
        }

        if (darkModeSourceTask != null)
        {
            this.DarkModeSource = await darkModeSourceTask();
        }

        OnPropertyChangedAsync(nameof(CurrentSource));
    }

    public static AsyncThemedIcon FromUri(Uri lightModeUri, Uri? darkModeUri = null, ImageSource? emptyIcon = null)
    {
        emptyIcon ??= EmptyIcon.CurrentSource;
        return new AsyncThemedIcon(
            async () => await lightModeUri.GetImageSourceAsync() ?? emptyIcon,
            darkModeUri != null ? async () => await darkModeUri.GetImageSourceAsync() ?? emptyIcon : null);
    }
}

public class UriThemedIcon : ThemedIcon
{
    private readonly ImageSource _lightModeFallbackValue;
    private readonly ImageSource? _darkModeFallbackValue;
    private Uri? _lightModeUri;
    private Uri? _darkModeUri;

    public Uri? LightModeUri
    {
        get => _lightModeUri;
        set
        {
            if (_lightModeUri != null && _lightModeUri.Equals(value))
            {
                return;
            }

            _lightModeUri = value;
            UpdateSource(value, DarkModeUri);
        }
    }

    public Uri? DarkModeUri
    {
        get => _darkModeUri;
        set
        {
            if (_darkModeUri != null && _darkModeUri.Equals(value))
            {
                return;
            }

            _darkModeUri = value;
            UpdateSource(LightModeUri, value);
        }
    }

    public UriThemedIcon(Uri? lightModeUri, ImageSource lightModeFallbackValue, Uri? darkModeUri = null,
        ImageSource? darkModeFallbackValue = null)
        : base(lightModeFallbackValue, darkModeUri != null, EmptyIcon)
    {
        _lightModeFallbackValue = lightModeFallbackValue;
        _darkModeFallbackValue = darkModeFallbackValue;
        LightModeUri = lightModeUri;
        UpdateSource(lightModeUri, darkModeUri);
    }

    private async void UpdateSource(Uri? lightModeUri, Uri? darkModeUri)
    {
        if (lightModeUri != null)
        {
            this.LightModeSource = await lightModeUri.GetImageSourceAsync() ?? _lightModeFallbackValue;
        }

        if (darkModeUri != null)
        {
            this.DarkModeSource = await darkModeUri.GetImageSourceAsync() ?? _darkModeFallbackValue;
        }

        OnPropertyChangedAsync(nameof(CurrentSource));
    }
}