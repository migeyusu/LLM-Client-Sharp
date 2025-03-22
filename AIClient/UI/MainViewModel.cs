using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using AutoMapper;
using LLMClient.Render;
using LLMClient.UI.Component;
using MaterialDesignThemes.Wpf;
using Microsoft.Xaml.Behaviors.Core;
using TextMateSharp.Grammars;

namespace LLMClient.UI;

public class MainViewModel : BaseViewModel
{
    private readonly IMapper _mapper;

    public SnackbarMessageQueue MessageQueue { get; set; } = new SnackbarMessageQueue();

    public IEndpointService ConfigureViewModel { get; }

    public IPromptsResource PromptsResource { get; }

    public bool IsInitialized { get; private set; }

    public bool IsProcessing
    {
        get => _isProcessing;
        set
        {
            if (value == _isProcessing) return;
            _isProcessing = value;
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
            IsProcessing = true;
            await SaveToLocal();
        }
        catch (Exception e)
        {
            MessageBox.Show(e.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsProcessing = false;
        }
    }));

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
            this.ThemeName = value ? ThemeName.DarkPlus : ThemeName.LightPlus;
        }
    }

    private const string ThemeColorResourceKey = "CodeBlock.TextMateSharp.Theme";

    private ThemeName _themeName = ThemeName.LightPlus;

    public ThemeName ThemeName
    {
        get => _themeName;
        set
        {
            if (value == _themeName) return;
            _themeName = value;
            OnPropertyChanged();
            UpdateResource(value);
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

            var model = selectionViewModel.SelectedEndpoint.NewClient(selectionViewModel.SelectedModelName);
            if (model == null)
            {
                MessageBox.Show("No model created!");
                return;
            }

            var dialogViewModel =
                new DialogViewModel(selectionViewModel.DialogName, model);
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

    private DialogViewModel? _preDialog;

    private bool _isProcessing;

    public MainViewModel(IEndpointService configureViewModel, IMapper mapper, IPromptsResource promptsResource)
    {
        MessageEventBus.MessageReceived += s => this.MessageQueue.Enqueue(s);
        _mapper = mapper;
        PromptsResource = promptsResource;
        ConfigureViewModel = configureViewModel;
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
            try
            {
                await using (var openRead = fileInfo.OpenRead())
                {
                    var dialogModel = await JsonSerializer.DeserializeAsync<DialogPersistanceModel>(openRead);
                    if (dialogModel != null)
                    {
                        var viewModel = _mapper.Map<DialogPersistanceModel, DialogViewModel>(dialogModel);
                        DialogViewModels.Add(viewModel);
                        viewModel.IsDataChanged = false;
                    }
                }
            }
            catch (Exception e)
            {
                Trace.WriteLine($"加载会话{fileInfo.FullName}失败：{e.Message}");
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
            if (dialogViewModel.IsDataChanged == false)
            {
                continue;
            }

            var dialogModel = _mapper.Map<DialogViewModel, DialogPersistanceModel>(dialogViewModel);
            var dialogId = dialogModel.DialogId;
            if (fileInfos.TryGetValue(dialogId.ToString(), out var fileInfo))
            {
                fileInfo.Delete();
            }
            else
            {
                fileInfo = new FileInfo(Path.Combine(dirPath, $"{dialogId}.json"));
            }

            await using (var fileStream = fileInfo.OpenWrite())
            {
                await JsonSerializer.SerializeAsync(fileStream, dialogModel);
            }

            dialogViewModel.IsDataChanged = false;
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
        IsProcessing = true;
        await LoadFromLocal(DialogSaveFolder);
        if (DialogViewModels.Any())
        {
            this.PreDialog = DialogViewModels.First();
        }

        UpdateResource(_themeName);
        await this.PromptsResource.Initialize();
        IsProcessing = false;
        IsInitialized = true;
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