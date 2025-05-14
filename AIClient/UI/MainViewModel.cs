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

    public ICommand BackupCommand => new ActionCommand((async o =>
    {
        if (PreDialog == null)
        {
            return;
        }

        var saveFileDialog = new SaveFileDialog()
        {
            AddExtension = true, DefaultExt = ".json", CheckPathExists = true,
            Filter = "json files (*.json)|*.json"
        };
        var dialogModel = _mapper.Map<DialogViewModel, DialogPersistanceModel>(PreDialog);
        if (saveFileDialog.ShowDialog() == true)
        {
            var fileName = saveFileDialog.FileName;
            var fileInfo = new FileInfo(fileName);
            await using (var fileStream = fileInfo.OpenWrite())
            {
                await JsonSerializer.SerializeAsync(fileStream, dialogModel);
            }
        }
    }));

    public ICommand ExportCommand => new ActionCommand((async o =>
    {
        if (PreDialog == null)
        {
            return;
        }

        var saveFileDialog = new SaveFileDialog()
        {
            AddExtension = true,
            DefaultExt = ".md", CheckPathExists = true,
            Filter = "markdown files (*.md)|*.md"
        };
        if (saveFileDialog.ShowDialog() != true)
        {
            return;
        }

        var stringBuilder = new StringBuilder(8192);
        stringBuilder.AppendLine($"# {PreDialog.Topic}");
        if (PreDialog.Model != null)
        {
            stringBuilder.AppendLine($"### {PreDialog.Model.Name}");
        }

        foreach (var viewItem in PreDialog.DialogItems)
        {
            if (viewItem is ResponseViewItem responseViewItem && !responseViewItem.IsInterrupt)
            {
                stringBuilder.AppendLine("## **Assistant:**");
                stringBuilder.Append(responseViewItem.Raw);
                stringBuilder.AppendLine();
                stringBuilder.AppendLine();
                stringBuilder.AppendLine("***");
                stringBuilder.AppendLine();
            }
            else if (viewItem is RequestViewItem reqViewItem && reqViewItem.IsAvailableInContext)
            {
                stringBuilder.AppendLine("## **User:**");
                stringBuilder.Append(reqViewItem.MessageContent);
                stringBuilder.AppendLine();
                stringBuilder.AppendLine();
                stringBuilder.AppendLine("***");
                stringBuilder.AppendLine();
            }
        }

        var fileName = saveFileDialog.FileName;
        File.WriteAllText(fileName, stringBuilder.ToString());
    }));

    public ICommand ChangeModelCommand => new ActionCommand((async o =>
    {
        if (PreDialog == null)
        {
            return;
        }

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

            PreDialog.Model = model;
        }
    }));

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
                new DialogViewModel(selectionViewModel.DialogName, model) { IsDataChanged = true };
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

    public async Task LoadDialogsFromLocal(string dirPath = DialogSaveFolder)
    {
        var path = Path.GetFullPath(dirPath);
        var directoryInfo = new DirectoryInfo(path);
        if (!directoryInfo.Exists)
        {
            return;
        }

        var dialogViewModels = new List<DialogViewModel>();
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
                        viewModel.FileName = Path.GetFileNameWithoutExtension(fileInfo.Name);
                        dialogViewModels.Add(viewModel);
                        viewModel.IsDataChanged = false;
                    }
                }
            }
            catch (Exception e)
            {
                Trace.WriteLine($"加载会话{fileInfo.FullName}失败：{e.Message}");
            }
        }

        foreach (var dialogViewModel in dialogViewModels.OrderByDescending(model => model.EditTime))
        {
            dialogViewModel.PropertyChanged += DialogOnEditTimeChanged;
            this.DialogViewModels.Add(dialogViewModel);
        }
    }

    private void DialogOnEditTimeChanged(object? sender, PropertyChangedEventArgs e)
    {
        var dialogViewModel = ((DialogViewModel)sender)!;
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
        await LoadDialogsFromLocal(DialogSaveFolder);
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