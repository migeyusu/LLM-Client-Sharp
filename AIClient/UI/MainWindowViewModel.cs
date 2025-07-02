using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using AutoMapper;
using LLMClient.Abstraction;
using LLMClient.Data;
using LLMClient.Render;
using LLMClient.UI.Component;
using LLMClient.UI.Dialog;
using LLMClient.UI.MCP;
using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
using Microsoft.Xaml.Behaviors.Core;
using TextMateSharp.Grammars;

namespace LLMClient.UI;

public class MainWindowViewModel : BaseViewModel
{
    private readonly IMapper _mapper;

    public SnackbarMessageQueue MessageQueue { get; set; } = new SnackbarMessageQueue();

    public IEndpointService EndpointsViewModel { get; }

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

    public McpServiceCollection McpServiceCollection { get; } = new McpServiceCollection();

    #region dialog

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
        var selectionViewModel = new DialogCreationViewModel(EndpointsViewModel);
        if (await DialogHost.Show(selectionViewModel) is true)
        {
            var model = selectionViewModel.GetClient();
            if (model == null)
            {
                MessageBox.Show("No model created!");
                return;
            }

            AddNewDialog(model, selectionViewModel.DialogName);
        }
    }));

    public DialogViewModel AddNewDialog(ILLMClient client, string dialogName = "新建会话")
    {
        var dialogViewModel = new DialogViewModel(dialogName, client)
            { IsDataChanged = true };
        dialogViewModel.PropertyChanged += DialogOnEditTimeChanged;
        this.DialogViewModels.Insert(0, dialogViewModel);
        PreDialog = dialogViewModel;
        return dialogViewModel;
    }

    public async void ForkPreDialog(IDialogItem viewModel)
    {
        var preDialog = PreDialog;
        if (preDialog == null)
        {
            return;
        }

        var of = preDialog.DialogItems.IndexOf(viewModel);
        var dialogModelClone = _mapper.Map<DialogViewModel, DialogPersistModel>(preDialog);
        dialogModelClone.DialogItems = dialogModelClone.DialogItems?.Take(of + 1).ToArray();
        var dirPath = Path.GetFullPath(DialogSaveFolder);
        var fileInfo = new FileInfo(Path.Combine(dirPath, $"{Guid.NewGuid()}.json"));
        await using (var fileStream = fileInfo.OpenWrite())
        {
            await JsonSerializer.SerializeAsync(fileStream, dialogModelClone);
        }

        var dialogViewModel = _mapper.Map<DialogPersistModel, DialogViewModel>(dialogModelClone);
        dialogViewModel.FileName = Path.GetFileNameWithoutExtension(fileInfo.Name);
        dialogViewModel.PropertyChanged += DialogOnEditTimeChanged;
        dialogViewModel.IsDataChanged = false;
        var indexOf = this.DialogViewModels.IndexOf(preDialog);
        this.DialogViewModels.Insert(indexOf, dialogViewModel);
        PreDialog = dialogViewModel;
    }

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

    #endregion

    private bool _isProcessing;

    public MainWindowViewModel(IEndpointService configureViewModel, IMapper mapper, IPromptsResource promptsResource)
    {
        MessageEventBus.MessageReceived += s => this.MessageQueue.Enqueue(s);
        _mapper = mapper;
        PromptsResource = promptsResource;
        EndpointsViewModel = configureViewModel;
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

    public const string DialogSaveFolder = "Dialogs";

    public async Task LoadDialogs(IEnumerable<FileInfo> fileInfos)
    {
        var dialogViewModels = new List<DialogViewModel>();
        foreach (var fileInfo in fileInfos)
        {
            var dialogViewModel = await DialogViewModel.LoadFromFile(fileInfo);
            if (dialogViewModel != null)
            {
                dialogViewModel.PropertyChanged += DialogOnEditTimeChanged;
                dialogViewModels.Add(dialogViewModel);
            }
            else
            {
                MessageQueue.Enqueue($"加载会话{fileInfo.FullName}失败：文件内容为空或格式不正确");
            }
        }

        foreach (var dialogViewModel in dialogViewModels.OrderByDescending(model => model.EditTime))
        {
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

    public async Task SaveDialogsToLocal(string folder = DialogSaveFolder)
    {
        var dirPath = Path.GetFullPath(folder);
        foreach (var dialogViewModel in DialogViewModels)
        {
            await dialogViewModel.SaveToLocal(dirPath);
        }
    }

    public void DeleteDialog(DialogViewModel dialogViewModel, string folderPath = DialogSaveFolder)
    {
        dialogViewModel.PropertyChanged -= DialogOnEditTimeChanged;
        this.DialogViewModels.Remove(dialogViewModel);
        this.PreDialog = this.DialogViewModels.FirstOrDefault();
        var dirPath = Path.GetFullPath(folderPath);
        var assossiateFile = dialogViewModel.GetAssociateFile(dirPath);
        if (assossiateFile.Exists)
        {
            assossiateFile.Delete();
        }
    }

    public async void Initialize()
    {
        IsProcessing = true;
        await EndpointsViewModel.Initialize();
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