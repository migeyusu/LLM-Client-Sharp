using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Windows.UI.ViewManagement;
using AutoMapper;
using LLMClient.Abstraction;
using LLMClient.Component;
using LLMClient.Component.Render;
using LLMClient.Component.Utility;
using LLMClient.Component.ViewModel;
using LLMClient.Component.ViewModel.Base;
using LLMClient.Configuration;
using LLMClient.Data;
using LLMClient.Dialog;
using LLMClient.Endpoints;
using LLMClient.Project;
using LLMClient.Test;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using Microsoft.Xaml.Behaviors.Core;
using PInvoke;
using TextMateSharp.Grammars;

namespace LLMClient;

public class MainWindowViewModel : BaseViewModel, IDisposable
{
    public SnackbarMessageQueue MessageQueue { get; set; } = new();

    public bool IsLeftDrawerOpen
    {
        get => _isLeftDrawerOpen;
        set
        {
            if (value == _isLeftDrawerOpen) return;
            _isLeftDrawerOpen = value;
            OnPropertyChanged();
        }
    }

    public GlobalOptions GlobalOptions { get; }

    public IEndpointService EndpointsViewModel { get; }

    public IPromptsResource PromptsResource { get; }

    public bool IsInitialized { get; private set; }

    private bool _isProcessing;

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
            OnPropertyChangedAsync();
            ModifyTheme(theme => theme.SetBaseTheme(value ? BaseTheme.Dark : BaseTheme.Light));
            UITheme.IsDarkMode = value;
            this.ThemeName = value ? ThemeName.DarkPlus : ThemeName.LightPlus;
        }
    }

    private ThemeName _themeName = ThemeName.LightPlus;

    public ThemeName ThemeName
    {
        get => _themeName;
        set
        {
            if (value == _themeName) return;
            _themeName = value;
            OnPropertyChanged();
            TextMateCodeRenderer.UpdateResource(value);
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

    #region dialog

    public ICommand ImportDialogCommand => new ActionCommand((async void (o) =>
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
            IAsyncEnumerable<FileBasedSessionBase> sessions;
            switch (type)
            {
                case "dialog":
                    sessions = FileBasedSessionBase.ImportFiles<DialogFileViewModel>(DialogFileViewModel.SaveFolderPath,
                        fileInfos, _mapper);
                    break;
                case "project":
                    sessions = FileBasedSessionBase.ImportFiles<ProjectViewModel>(ProjectViewModel.SaveFolderPath,
                        fileInfos, _mapper);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            var sessionList = new List<FileBasedSessionBase>();
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

    public CreateSessionViewModel CreateSession
    {
        get { return _createSessionLazy.Value; }
    }

    private readonly Lazy<CreateSessionViewModel> _createSessionLazy;

    public IMapper Mapper => _mapper;

    private readonly IMapper _mapper;
    private readonly IViewModelFactory _viewModelFactory;

    public DialogFileViewModel AddNewDialog(ILLMChatClient client, string dialogName = "新建会话")
    {
        var dialogSession = _viewModelFactory.CreateViewModel<DialogFileViewModel>(dialogName, client);
        AddSession(dialogSession);
        return dialogSession;
    }

    #endregion

    public ObservableCollection<FileBasedSessionBase> SessionViewModels { get; set; } = new();

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


    private string _loadingMessage = "Loading...";
    private bool _isLeftDrawerOpen = true;

    private readonly UISettings _uiSettings;

    public MainWindowViewModel(IEndpointService configureViewModel, IPromptsResource promptsResource,
        IMcpServiceCollection mcpServiceCollection, IRagSourceCollection ragSourceCollection, IMapper mapper,
        GlobalOptions globalOptions, IViewModelFactory viewModelFactory)
    {
        MessageEventBus.MessageReceived += s => MessageQueue.Enqueue(s);
        PromptsResource = promptsResource;
        McpServiceCollection = mcpServiceCollection;
        RagSourceCollection = ragSourceCollection;
        _mapper = mapper;
        _viewModelFactory = viewModelFactory;
        GlobalOptions = globalOptions;
        EndpointsViewModel = configureViewModel;
        _uiSettings = new UISettings();
        IsDarkTheme = !IsColorLight(_uiSettings.GetColorValue(UIColorType.Background));
        _createSessionLazy =
            new Lazy<CreateSessionViewModel>(() => viewModelFactory.CreateViewModel<CreateSessionViewModel>());
        /*SystemEvents.UserPreferenceChanged += (_, e) =>
        {
            if (e.Category == UserPreferenceCategory.Color)
            {

            }
        };*/
        /*if (theme is Theme internalTheme)
        {
            _isColorAdjusted = internalTheme.ColorAdjustment is not null;

            var colorAdjustment = internalTheme.ColorAdjustment ?? new ColorAdjustment();
            _desiredContrastRatio = colorAdjustment.DesiredContrastRatio;
            _contrastValue = colorAdjustment.Contrast;
            _colorSelectionValue = colorAdjustment.Colors;
        }*/
        var paletteHelper = new PaletteHelper();
        if (paletteHelper.GetThemeManager() is { } themeManager)
        {
            themeManager.ThemeChanged += (_, e) => { IsDarkTheme = e.NewTheme.GetBaseTheme() == BaseTheme.Dark; };
        }

        _uiSettings.ColorValuesChanged += UiSettingsOnColorValuesChanged;
    }

    private void UiSettingsOnColorValuesChanged(UISettings sender, object args)
    {
        var colorValue = sender.GetColorValue(UIColorType.Background);
        IsDarkTheme = !IsColorLight(colorValue);
    }

    bool IsColorLight(Windows.UI.Color clr)
    {
        return (((5 * clr.G) + (2 * clr.R) + clr.B) > (8 * 128));
    }

    #region item management

    public void AddSession(FileBasedSessionBase projectViewModel)
    {
        ((INotifyPropertyChanged)projectViewModel).PropertyChanged += SessionOnPropertyChanged;
        this.SessionViewModels.Insert(0, projectViewModel);
        PreSession = projectViewModel;
    }

    private async Task InitialSessionsFromLocal()
    {
        var sessions = new List<FileBasedSessionBase>();
        sessions.AddRange(
            await FileBasedSessionBase.LoadFromLocal<DialogFileViewModel>(_mapper,
                DialogFileViewModel.SaveFolderPath));
        sessions.AddRange(
            await FileBasedSessionBase.LoadFromLocal<ProjectViewModel>(_mapper, ProjectViewModel.SaveFolderPath));
        foreach (var llmSession in sessions.OrderByDescending((session => session.EditTime)))
        {
            ((INotifyPropertyChanged)llmSession).PropertyChanged += SessionOnPropertyChanged;
            this.SessionViewModels.Add(llmSession);
        }
    }

    private void InsertSessions(IEnumerable<FileBasedSessionBase> sessions)
    {
        foreach (var session in sessions.OrderByDescending(model => model.EditTime))
        {
            ((INotifyPropertyChanged)session).PropertyChanged += SessionOnPropertyChanged;
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

    private void SessionOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        var session = ((FileBasedSessionBase?)sender)!;
        switch (e.PropertyName)
        {
            case nameof(ILLMSession.EditTime):
                var indexOf = this.SessionViewModels.IndexOf(session);
                if (indexOf != 0)
                {
                    this.SessionViewModels.Move(indexOf, 0);
                }

                break;
            case nameof(ILLMSession.IsBusy):
                OnPropertyChanged(nameof(IsBusy));
                if (!IsBusy)
                {
                    var mainWindow = Application.Current.MainWindow;
                    if (mainWindow == null)
                    {
                        return;
                    }

                    if (mainWindow.IsFocused || mainWindow.IsActive)
                    {
                        return;
                    }

                    var wih = new WindowInteropHelper(mainWindow);
                    var hwnd = wih.Handle;
                    // 创建并填充FLASHWINFO结构体
                    var finfo = new User32.FLASHWINFO();
                    finfo.cbSize = Convert.ToInt32(Marshal.SizeOf(finfo));
                    finfo.hwnd = hwnd;
                    // FLASHW_ALL: 闪烁标题栏和任务栏图标
                    // FLASHW_TIMERNOFG: 持续闪烁，直到窗口被用户点击激活，然后自动停止
                    finfo.dwFlags = User32.FlashWindowFlags.FLASHW_TIMERNOFG;
                    finfo.uCount = int.MaxValue; // 闪烁次数，对于持续闪烁设为最大值
                    finfo.dwTimeout = 0; // 使用系统默认的光标闪烁频率
                    User32.FlashWindowEx(ref finfo);
                }

                break;
        }
    }

    private bool ShouldSaveSessions()
    {
        return SessionViewModels.Any(session => session.IsDataChanged);
    }

    private async Task SaveSessionsToLocal()
    {
        foreach (var sessionViewModel in SessionViewModels.Where((session => session.IsDataChanged)))
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
    }

    public void DeleteSession(FileBasedSessionBase session)
    {
        ((INotifyPropertyChanged)session).PropertyChanged -= SessionOnPropertyChanged;
        this.SessionViewModels.Remove(session);
        this.PreSession = this.SessionViewModels.FirstOrDefault();
        session.Delete();
    }

    public async Task SaveDataAsync()
    {
        if (this.IsBusy || this.IsProcessing)
        {
            return;
        }

        try
        {
            await this.EndpointsViewModel.SaveActivities();
            await HttpContentCache.Instance.PersistIndexAsync();
        }
        catch (Exception e)
        {
            Trace.TraceError(e.Message, "Error");
        }

        if (this.ShouldSaveSessions())
        {
            this.LoadingMessage = "Auto Saving data...";
            this.IsProcessing = true;
            try
            {
                await this.SaveSessionsToLocal();
            }
            catch (Exception e)
            {
                Trace.TraceError(e.Message);
            }
            finally
            {
                IsProcessing = false;
            }
        }
    }

    #endregion

    // ReSharper disable once MemberCanBePrivate.Global
    public async void Initialize()
    {
        try
        {
            if (IsInitialized)
            {
                return;
            }

            IsProcessing = true;
            await TextMateCodeRenderer.InitializeAsync();
            await McpServiceCollection.LoadAsync();
            await RagSourceCollection.LoadAsync();
            await EndpointsViewModel.Initialize();
            await PromptsResource.Initialize();
            await InitialSessionsFromLocal();
            if (SessionViewModels.Any())
            {
                PreSession = SessionViewModels.First();
            }

            TextMateCodeRenderer.UpdateResource(_themeName);
            IsInitialized = true;
            // SemanticKernelStore.Test();
            //test point
            //await new TempTest().TestMethod();
        }
        catch (Exception e)
        {
            MessageQueue.Enqueue("Init failed: " + e.Message);
        }
        finally
        {
            IsProcessing = false;
        }
    }

    private static void ModifyTheme(Action<Theme> modificationAction)
    {
        var paletteHelper = new PaletteHelper();
        var theme = paletteHelper.GetTheme();
        modificationAction.Invoke(theme);
        paletteHelper.SetTheme(theme);
    }

    public void Dispose()
    {
        MessageQueue.Dispose();
    }
}