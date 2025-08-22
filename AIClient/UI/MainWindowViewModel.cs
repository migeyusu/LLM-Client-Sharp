using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using LLMClient.Abstraction;
using LLMClient.Data;
using LLMClient.Dialog;
using LLMClient.Endpoints;
using LLMClient.Project;
using LLMClient.UI.Component;
using LLMClient.UI.Render;
using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
using Microsoft.Xaml.Behaviors.Core;
using TextMateSharp.Grammars;

namespace LLMClient.UI;

public class MainWindowViewModel : BaseViewModel
{
    public SnackbarMessageQueue MessageQueue { get; set; } = new();

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

    public bool IsBusy => SessionViewModels.Any(session => session.IsBusy);

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

    public string LoadingMessage
    {
        get => _loadingMessage;
        set
        {
            if (value == _loadingMessage) return;
            _loadingMessage = value;
            OnPropertyChanged();
        }
    }

    public IMcpServiceCollection McpServiceCollection { get; }

    public IRagSourceCollection RagSourceCollection { get; }

    #region project

    public ICommand NewProjectCommand => new ActionCommand((async void (_) =>
    {
        try
        {
            var selectionViewModel =
                new ProjectConfigViewModel(this.EndpointsViewModel, new ProjectViewModel(NullLlmModelClient.Instance));
            if (await DialogHost.Show(selectionViewModel) is true)
            {
                AddSession(selectionViewModel.Project);
            }
        }
        catch (Exception e)
        {
            MessageBox.Show(e.Message);
        }
    }));

    #endregion

    #region dialog

    public ICommand ImportCommand => new ActionCommand((async o =>
    {
        try
        {
            var type = o.ToString();
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
            IAsyncEnumerable<ILLMSession> sessions;
            switch (type)
            {
                case "dialog":
                    sessions = DialogFileViewModel.ImportFiles(fileInfos);
                    break;
                case "project":
                    sessions = ProjectViewModel.ImportFiles(fileInfos);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            var sessionList = new List<ILLMSession>();
            await foreach (var llmSession in sessions)
            {
                sessionList.Add(llmSession);
            }

            InsertSessions(sessionList);
        }
        catch (Exception e)
        {
            MessageBox.Show(e.Message);
        }
    }));

    public ICommand NewDialogCommand => new ActionCommand((async _ =>
    {
        try
        {
            var selectionViewModel = new ModelSelectionPopupViewModel(EndpointsViewModel);
            if (await DialogHost.Show(selectionViewModel) is true)
            {
                var model = selectionViewModel.GetClient();
                if (model == null)
                {
                    MessageBox.Show("No model created!");
                    return;
                }

                AddNewDialog(model);
            }
        }
        catch (Exception e)
        {
            MessageBox.Show(e.Message);
        }
    }));

    public DialogFileViewModel AddNewDialog(ILLMChatClient client, string dialogName = "新建会话")
    {
        var dialogSession = new DialogFileViewModel(dialogName, client);
        AddSession(dialogSession);
        return dialogSession;
    }

    public void ForkPreDialog(IDialogItem item)
    {
        var preSession = PreSession;
        if (preSession is not DialogFileViewModel preDialog)
        {
            return;
        }

        var indexOf = this.SessionViewModels.IndexOf(preDialog);
        if (indexOf < 0)
        {
            return;
        }

        var dialogSession = preDialog.Fork(item);
        AddSession(dialogSession);
    }

    public ObservableCollection<ILLMSession> SessionViewModels { get; set; } =
        new ObservableCollection<ILLMSession>();

    private ILLMSession? _preSession;

    public ILLMSession? PreSession
    {
        get => _preSession;
        set
        {
            if (Equals(value, _preSession)) return;
            _preSession = value;
            OnPropertyChanged();
        }
    }

    #endregion

    public ICommand LoadCommand => new ActionCommand((_ =>
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

    public ICommand SaveCommand => new ActionCommand((async _ =>
    {
        try
        {
            IsProcessing = true;
            await SaveSessionsToLocal();
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

    private bool _isProcessing;
    private string _loadingMessage = "Loading...";

    public MainWindowViewModel(IEndpointService configureViewModel, IPromptsResource promptsResource,
        IMcpServiceCollection mcpServiceCollection, IRagSourceCollection ragSourceCollection)
    {
        MessageEventBus.MessageReceived += s => this.MessageQueue.Enqueue(s);
        PromptsResource = promptsResource;
        McpServiceCollection = mcpServiceCollection;
        RagSourceCollection = ragSourceCollection;
        EndpointsViewModel = configureViewModel;
        var paletteHelper = new PaletteHelper();
        var theme = paletteHelper.GetTheme();
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
            themeManager.ThemeChanged += (_, e) => { IsDarkTheme = e.NewTheme.GetBaseTheme() == BaseTheme.Dark; };
        }
    }

    #region item management

    public void AddSession(ILLMSession projectViewModel)
    {
        ((INotifyPropertyChanged)projectViewModel).PropertyChanged += SessionOnEditTimeChanged;
        this.SessionViewModels.Insert(0, projectViewModel);
        PreSession = projectViewModel;
    }

    private async Task InitialSessionsFromLocal()
    {
        var sessions = new List<ILLMSession>();
        await foreach (var llmSession in DialogFileViewModel.LoadFromLocal())
        {
            sessions.Add(llmSession);
        }

        await foreach (var projectViewModel in ProjectViewModel.LoadFromLocal())
        {
            sessions.Add(projectViewModel);
        }

        foreach (var llmSession in sessions.OrderByDescending((session => session.EditTime)))
        {
            this.SessionViewModels.Add(llmSession);
        }
    }

    private void InsertSessions(IEnumerable<ILLMSession> sessions)
    {
        foreach (var session in sessions.OrderByDescending(model => model.EditTime))
        {
            ((INotifyPropertyChanged)session).PropertyChanged += SessionOnEditTimeChanged;
            //默认将最新的会话放在最前面，也就是从大到小排序
            var sessionEditTime = session.EditTime;
            var firstOrDefault =
                this.SessionViewModels.FirstOrDefault((llmSession => llmSession.EditTime < sessionEditTime));
            if (firstOrDefault is not null)
            {
                var indexOf = this.SessionViewModels.IndexOf(firstOrDefault);
                this.SessionViewModels.Insert(indexOf, session);
            }
            else
            {
                //如果没有比当前会话更小的，则直接添加到列表末尾
                this.SessionViewModels.Add(session);
            }
        }
    }

    private void SessionOnEditTimeChanged(object? sender, PropertyChangedEventArgs e)
    {
        var session = ((ILLMSession?)sender)!;
        switch (e.PropertyName)
        {
            case nameof(ILLMSession.EditTime):
                var indexOf = this.SessionViewModels.IndexOf(session);
                if (indexOf != 0)
                {
                    this.SessionViewModels.Move(indexOf, 0);
                }

                break;
        }
    }

    public async Task SaveSessionsToLocal()
    {
        foreach (var sessionViewModel in SessionViewModels)
        {
            if (sessionViewModel is DialogFileViewModel dialogViewModel)
            {
                await dialogViewModel.SaveToLocal();
            }
            else if (sessionViewModel is ProjectViewModel projectViewModel)
            {
                await projectViewModel.SaveToLocal();
            }
        }

        MessageQueue.Enqueue("会话已保存到本地");
    }

    public void DeleteSession(ILLMSession session)
    {
        ((INotifyPropertyChanged)session).PropertyChanged -= SessionOnEditTimeChanged;
        this.SessionViewModels.Remove(session);
        this.PreSession = this.SessionViewModels.FirstOrDefault();
        session.Delete();
    }

    public async Task SaveSessions()
    {
        this.LoadingMessage = "Saving sessions...";
        this.IsProcessing = true;
        await this.SaveSessionsToLocal();
        await HttpContentCache.Instance.PersistIndexAsync();
    }

    #endregion

    public async void Initialize()
    {
        IsProcessing = true;
        await McpServiceCollection.LoadAsync();
        await RagSourceCollection.LoadAsync();
        await EndpointsViewModel.Initialize();
        await InitialSessionsFromLocal();
        if (SessionViewModels.Any())
        {
            this.PreSession = SessionViewModels.First();
        }

        UpdateResource(_themeName);
        await this.PromptsResource.Initialize();
        IsProcessing = false;
        IsInitialized = true;
        // SemanticKernelStore.Test();
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

    private static void ModifyTheme(Action<Theme> modificationAction)
    {
        var paletteHelper = new PaletteHelper();
        Theme theme = paletteHelper.GetTheme();
        modificationAction.Invoke(theme);
        paletteHelper.SetTheme(theme);
    }
}