using System.Windows;

namespace LLMClient;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public static bool IsCustomMessageBoxReady { get; private set; }

    public App()
    {
        Startup += OnStartup;
        Exit += OnExit;
        Activated += OnActivated;
        DispatcherUnhandledException += (_, _) => { IsCustomMessageBoxReady = false; };
    }

    private void OnStartup(object sender, StartupEventArgs e)
    {
        IsCustomMessageBoxReady = Resources.MergedDictionaries.Count > 0;
    }

    private void OnActivated(object? sender, EventArgs e)
    {
        IsCustomMessageBoxReady = Resources.MergedDictionaries.Count > 0 &&
                                  MainWindow is { IsLoaded: true, IsVisible: true };
    }

    private void OnExit(object sender, ExitEventArgs e)
    {
        IsCustomMessageBoxReady = false;
    }
}