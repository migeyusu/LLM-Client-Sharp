using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using AutoMapper;
using LLMClient.Abstraction;
using LLMClient.Data;
using LLMClient.Endpoints;
using LLMClient.Render;
using LLMClient.UI.Component;
using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
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
            await SaveDialogsToLocal();
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

    public ICommand ImportDialogCommand => new ActionCommand((async o =>
    {
        var openFileDialog = new OpenFileDialog()
        {
            AddExtension = true,
            Filter = "json files (*.json)|*.json",
            CheckFileExists = true,
            Multiselect = true
        };
        if (openFileDialog.ShowDialog() != true)
        {
            return;
        }

        var fileInfos = openFileDialog.FileNames.Select((s => new FileInfo(s)));
        var path = Path.GetFullPath(DialogSaveFolder);
        var directoryInfo = new DirectoryInfo(path);
        if (!directoryInfo.Exists)
        {
            directoryInfo.Create();
        }

        try
        {
            var list = new List<FileInfo>();
            foreach (var fileInfo in fileInfos)
            {
                if (fileInfo.DirectoryName?.Equals(directoryInfo.FullName, StringComparison.OrdinalIgnoreCase) == false)
                {
                    var newFilePath = Path.Combine(directoryInfo.FullName, fileInfo.Name);
                    if (File.Exists(newFilePath))
                    {
                        MessageQueue.Enqueue($"会话文件 {fileInfo.Name} 已存在，未进行复制。");
                    }
                    else
                    {
                        File.Copy(fileInfo.FullName, newFilePath, true);
                        list.Add(new FileInfo(newFilePath));
                    }
                }
            }

            await LoadDialogs(list);
        }
        catch (Exception e)
        {
            MessageBox.Show("导入出现问题" + e.Message);
        }
    }));

    public ICommand NewDialogCommand => new ActionCommand((async o =>
    {
        var selectionViewModel = new DialogCreationViewModel(ConfigureViewModel.AvailableEndpoints);
        if (await DialogHost.Show(selectionViewModel) is true)
        {
            var model = selectionViewModel.GetClient();
            if (model == null)
            {
                MessageBox.Show("No model created!");
                return;
            }

            var dialogViewModel =
                new DialogViewModel(selectionViewModel.DialogName, model)
                    { IsDataChanged = true };
            dialogViewModel.PropertyChanged += DialogOnEditTimeChanged;
            PreDialog = dialogViewModel;
            this.DialogViewModels.Insert(0, dialogViewModel);
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

    public const string DialogSaveFolder = "Dialogs";

    public async Task LoadDialogs(IEnumerable<FileInfo> fileInfos)
    {
        var dialogViewModels = new List<DialogViewModel>();
        foreach (var fileInfo in fileInfos)
        {
            try
            {
                await using (var openRead = fileInfo.OpenRead())
                {
                    var dialogModel = await JsonSerializer.DeserializeAsync<DialogPersistModel>(openRead);
                    if (dialogModel == null)
                    {
                        MessageQueue.Enqueue($"加载会话{fileInfo.FullName}失败：文件内容为空");
                        continue;
                    }

                    if (dialogModel.Version != DialogPersistModel.DialogPersistVersion)
                    {
                        MessageQueue.Enqueue($"加载会话{fileInfo.FullName}失败：版本不匹配");
                        continue;
                    }

                    var viewModel = _mapper.Map<DialogPersistModel, DialogViewModel>(dialogModel);
                    viewModel.FileName = Path.GetFileNameWithoutExtension(fileInfo.Name);
                    dialogViewModels.Add(viewModel);
                    viewModel.IsDataChanged = false;
                }
            }
            catch (Exception e)
            {
                MessageQueue.Enqueue($"加载会话{fileInfo.FullName}失败：{e.Message}");
            }
        }

        foreach (var dialogViewModel in dialogViewModels.OrderByDescending(model => model.EditTime))
        {
            dialogViewModel.PropertyChanged += DialogOnEditTimeChanged;
            this.DialogViewModels.Add(dialogViewModel);
        }
    }

    public async Task InitialDialogsFromLocal(string dirPath = DialogSaveFolder)
    {
        var path = Path.GetFullPath(dirPath);
        var directoryInfo = new DirectoryInfo(path);
        if (!directoryInfo.Exists)
        {
            return;
        }

        await LoadDialogs(directoryInfo.GetFiles());
    }

    private void DialogOnEditTimeChanged(object? sender, PropertyChangedEventArgs e)
    {
        var dialogViewModel = ((DialogViewModel?)sender)!;
        switch (e.PropertyName)
        {
            case nameof(DialogViewModel.EditTime):
                var indexOf = this.DialogViewModels.IndexOf(dialogViewModel);
                if (indexOf != 0)
                {
                    this.DialogViewModels.Move(indexOf, 0);
                }

                break;
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

    public async Task SaveDialogsToLocal(string folder = DialogSaveFolder)
    {
        var dirPath = Path.GetFullPath(folder);
        var fileInfos = LocalDialogFiles();
        foreach (var dialogViewModel in DialogViewModels)
        {
            if (dialogViewModel.IsDataChanged == false)
            {
                continue;
            }

            var dialogModel = _mapper.Map<DialogViewModel, DialogPersistModel>(dialogViewModel);
            if (fileInfos.TryGetValue(dialogViewModel.FileName, out var fileInfo))
            {
                fileInfo.Delete();
            }
            else
            {
                fileInfo = new FileInfo(Path.Combine(dirPath, $"{Guid.NewGuid()}.json"));
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
        dialogViewModel.PropertyChanged -= DialogOnEditTimeChanged;
        this.DialogViewModels.Remove(dialogViewModel);
        this.PreDialog = this.DialogViewModels.FirstOrDefault();
        var fileInfos = LocalDialogFiles();
        if (string.IsNullOrEmpty(dialogViewModel.FileName))
        {
            return;
        }

        if (fileInfos.TryGetValue(dialogViewModel.FileName, out var fileInfo))
        {
            fileInfo.Delete();
        }
    }

    public async void Initialize()
    {
        IsProcessing = true;
        await ConfigureViewModel.Initialize();
        await InitialDialogsFromLocal(DialogSaveFolder);
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