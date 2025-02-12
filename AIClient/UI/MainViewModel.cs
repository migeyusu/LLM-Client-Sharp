using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using AutoMapper;
using MaterialDesignThemes.Wpf;
using Microsoft.Xaml.Behaviors.Core;

namespace LLMClient.UI;

public class MainViewModel : BaseViewModel
{
    private readonly IMapper _mapper;
    public IEndpointService ConfigureViewModel { get; set; }

    public bool IsInitializing
    {
        get => _isInitializing;
        set
        {
            if (value == _isInitializing) return;
            _isInitializing = value;
            OnPropertyChanged();
        }
    }

    public ICommand LoadCommand => new ActionCommand((o =>
    {
        try
        {
            Initialize();
        }
        catch (Exception e)
        {
            MessageBox.Show(e.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }));

    public ICommand SaveCommand => new ActionCommand((async o =>
    {
        try
        {
            IsInitializing = true;
            await SaveToLocal();
        }
        catch (Exception e)
        {
            MessageBox.Show(e.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsInitializing = false;
        }
    }));

    public ICommand QuitCommand => new ActionCommand(async o =>
    {
        try
        {
            IsInitializing = true;
            await SaveToLocal();
        }
        finally
        {
            await Task.Run(() => { Application.Current.Dispatcher.Invoke(() => { ((Window)o).Close(); }); });
        }
    });

    private bool _isDarkTheme;

    public bool IsDarkTheme
    {
        get => _isDarkTheme;
        set
        {
            if (_isDarkTheme == value)
            {
                return;
            }

            _isDarkTheme = value;
            OnPropertyChanged();
            ModifyTheme(theme => theme.SetBaseTheme(value ? BaseTheme.Dark : BaseTheme.Light));
            UITheme.IsDarkMode = value;
        }
    }


    public ICommand SelectModelCommand => new ActionCommand((async o =>
    {
        var selectionViewModel = new ModelSelectionViewModel()
            { AvailableEndpoints = ConfigureViewModel.AvailableEndpoints };
        if (await DialogHost.Show(selectionViewModel) is true)
        {
            if (selectionViewModel.SelectedModelName == null)
            {
                return;
            }

            if (selectionViewModel.SelectedEndpoint == null)
            {
                return;
            }

            var model = selectionViewModel.SelectedEndpoint.GetModel(selectionViewModel.SelectedModelName);
            if (model == null)
            {
                MessageBox.Show("No model created!");
                return;
            }

            var dialogViewModel = new DialogViewModel()
            {
                Topic = selectionViewModel.DialogName,
                Model = model,
                Endpoint = selectionViewModel.SelectedEndpoint
            };
            PreDialog = dialogViewModel;
            this.DialogViewModels.Add(dialogViewModel);
        }
    }));

    public ObservableCollection<DialogViewModel> DialogViewModels { get; set; } =
        new ObservableCollection<DialogViewModel>();

    public DialogViewModel? PreDialog
    {
        get => _preDialog;
        set
        {
            if (Equals(value, _preDialog)) return;
            _preDialog = value;
            OnPropertyChanged();
        }
    }

    private readonly DispatcherTimer _timer;

    private async void UpdateCallback(object? state, EventArgs eventArgs)
    {
        try
        {
            await SaveToLocal();
        }
        catch (Exception e)
        {
            Trace.Write(e);
        }
    }

    private DialogViewModel? _preDialog;
    private bool _isInitializing;

    public MainViewModel(IEndpointService configureViewModel, IMapper mapper)
    {
        _mapper = mapper;
        ConfigureViewModel = configureViewModel;
        _timer = new DispatcherTimer(DispatcherPriority.ApplicationIdle)
        {
            Interval = TimeSpan.FromMinutes(5)
        };
        _timer.Tick += UpdateCallback;
        _timer.Start();
        var paletteHelper = new PaletteHelper();
        Theme theme = paletteHelper.GetTheme();
        IsDarkTheme = theme.GetBaseTheme() == BaseTheme.Dark;
        /*if (theme is Theme internalTheme)
        {
            _isColorAdjusted = internalTheme.ColorAdjustment is not null;

            var colorAdjustment = internalTheme.ColorAdjustment ?? new ColorAdjustment();
            _desiredContrastRatio = colorAdjustment.DesiredContrastRatio;
            _contrastValue = colorAdjustment.Contrast;
            _colorSelectionValue = colorAdjustment.Colors;
        }*/

        if (paletteHelper.GetThemeManager() is { } themeManager)
        {
            themeManager.ThemeChanged += (_, e) => { IsDarkTheme = e.NewTheme?.GetBaseTheme() == BaseTheme.Dark; };
        }
    }

    ~MainViewModel()
    {
        _timer.Stop();
    }

    private const string DialogSaveFolder = "Dialogs";

    public async Task LoadFromLocal(string dirPath = DialogSaveFolder)
    {
        var path = Path.GetFullPath(dirPath);
        var directoryInfo = new DirectoryInfo(path);
        if (!directoryInfo.Exists)
        {
            return;
        }

        foreach (var fileInfo in directoryInfo.GetFiles())
        {
            await using (var openRead = fileInfo.OpenRead())
            {
                var dialogModel = await JsonSerializer.DeserializeAsync<DialogModel>(openRead);
                if (dialogModel != null)
                {
                    var viewModel = _mapper.Map<DialogModel, DialogViewModel>(dialogModel);
                    DialogViewModels.Add(viewModel);
                }
            }
        }
    }

    private IDictionary<string, FileInfo> LocalDialogFiles(string folder = DialogSaveFolder)
    {
        var dirPath = Path.GetFullPath(folder);
        var directoryInfo = new DirectoryInfo(dirPath);
        if (!directoryInfo.Exists)
            directoryInfo.Create();
        return directoryInfo.GetFiles()
            .ToDictionary((f) => Path.GetFileNameWithoutExtension(f.Name), (f) => f);
    }

    public async Task SaveToLocal(string folder = DialogSaveFolder)
    {
        var dirPath = Path.GetFullPath(folder);
        var fileInfos = LocalDialogFiles();
        foreach (var dialogViewModel in DialogViewModels)
        {
            var dialogModel = _mapper.Map<DialogViewModel, DialogModel>(dialogViewModel);
            var dialogId = dialogModel.DialogId;
            if (fileInfos.TryGetValue(dialogId.ToString(), out var fileInfo))
            {
                fileInfo.Delete();
            }
            else
            {
                fileInfo = new FileInfo(Path.Combine(dirPath, $"{dialogId}.json"));
            }

            using (var fileStream = fileInfo.OpenWrite())
            {
                await JsonSerializer.SerializeAsync(fileStream, dialogModel);
            }
        }
    }

    public void DeleteDialog(DialogViewModel dialogViewModel)
    {
        this.DialogViewModels.Remove(dialogViewModel);
        this.PreDialog = this.DialogViewModels.FirstOrDefault();
        var fileInfos = LocalDialogFiles();
        if (fileInfos.TryGetValue(dialogViewModel.DialogId.ToString(), out var fileInfo))
        {
            fileInfo.Delete();
        }
    }


    public async void Initialize()
    {
        IsInitializing = true;
        await LoadFromLocal(DialogSaveFolder);
        if (DialogViewModels.Any())
        {
            this.PreDialog = DialogViewModels.First();
        }

        IsInitializing = false;
    }

    public ProgressViewModel LoadingProgress { get; } = new ProgressViewModel("Loading...");

    private static void ModifyTheme(Action<Theme> modificationAction)
    {
        var paletteHelper = new PaletteHelper();
        Theme theme = paletteHelper.GetTheme();
        modificationAction?.Invoke(theme);
        paletteHelper.SetTheme(theme);
    }
}