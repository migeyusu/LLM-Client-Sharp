using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows.Input;
using AutoMapper;
using LLMClient.Abstraction;
using LLMClient.Component.Converters;
using LLMClient.Component.ViewModel;
using LLMClient.Configuration;
using LLMClient.ContextEngineering.Analysis;
using LLMClient.ContextEngineering.PromptGeneration;
using LLMClient.ContextEngineering.Tools;
using LLMClient.Data;
using LLMClient.Dialog;
using LLMClient.Dialog.Models;
using LLMClient.Endpoints;
using LLMClient.ToolCall;
using LLMClient.ToolCall.DefaultPlugins;
using MaterialDesignThemes.Wpf;
using Microsoft.Xaml.Behaviors.Core;

namespace LLMClient.Project;

[TypeConverter(typeof(EnumDescriptionTypeConverter))]
public enum ProjectType : int
{
    [Description("代码")] Default,
    [Description("C#")] CSharp,
    [Description("C++")] Cpp
}

public abstract class ProjectViewModel : FileBasedSessionBase, ILLMSessionLoader<ProjectViewModel>, IPromptableSession
{
    public const string SaveDir = "Projects";

    private readonly IMapper _mapper;
    private readonly IViewModelFactory _factory;

    private bool _isDataChanged = true;

    public override bool IsDataChanged
    {
        get { return Session.Any(session => session.IsDataChanged) || Requester.IsDataChanged || _isDataChanged; }
        set
        {
            _isDataChanged = value;
            if (!value)
            {
                //用于重置子项的变更状态
                foreach (var session in this.Session)
                {
                    session.IsDataChanged = value;
                }

                Requester.IsDataChanged = value;
            }
        }
    }

    public override bool IsBusy
    {
        get { return Session.Any(session => session.IsBusy); }
    }

    private static readonly Lazy<string> SaveFolderPathLazy = new(() => Path.GetFullPath(SaveDir));

    public static string SaveFolderPath => SaveFolderPathLazy.Value;

    protected override string DefaultSaveFolderPath
    {
        get { return SaveFolderPathLazy.Value; }
    }

    public RequesterViewModel Requester { get; }

    #region file

    public static async Task<ProjectViewModel?> LoadFromStream(Stream fileStream, IMapper mapper)
    {
        try
        {
            var root = await JsonNode.ParseAsync(fileStream);
            if (root == null)
            {
                return null;
            }

            var version = root[nameof(ProjectPersistModel.Version)]?.GetValue<int?>();
            if (version != ProjectPersistModel.CurrentVersion)
            {
                throw new Exception($"Project version mismatch: {version} != {ProjectPersistModel.CurrentVersion}");
            }

            var typeInt = root["ProjectOptions"]?[nameof(ProjectOptionsPersistModel.Type)]?.GetValue<int>() ?? 0;
            var type = Enum.IsDefined(typeof(ProjectType), typeInt) ? (ProjectType)typeInt : ProjectType.Default;
            var (poType, viewmodelType) = ResolveTypePair(type);
            var persistModel = (ProjectPersistModel?)root.Deserialize(poType, SerializerOption);
            if (persistModel == null)
            {
                throw new Exception($"Project type mismatch: {type} != {poType}");
            }

            var viewModel = mapper.Map<ProjectPersistModel, ProjectViewModel>(persistModel, (_ => { }));
            if (viewModel.GetType() != viewmodelType)
            {
                throw new Exception($"Project mapping failed: {type} != {viewmodelType}");
            }

            return viewModel;
        }
        catch (Exception e)
        {
            Trace.TraceError("Failed to load project: " + e.Message);
            return null;
        }
    }

    private static Tuple<Type, Type> ResolveTypePair(ProjectType projectType)
    {
        switch (projectType)
        {
            case ProjectType.Default:
                return new Tuple<Type, Type>(typeof(ProjectPersistModel), typeof(ProjectViewModel));
            case ProjectType.CSharp:
                return new Tuple<Type, Type>(typeof(CSharpProjectPersistModel), typeof(CSharpProjectViewModel));
            case ProjectType.Cpp:
                return new Tuple<Type, Type>(typeof(CppProjectPersistModel), typeof(CppProjectViewModel));
            default:
                throw new ArgumentOutOfRangeException($"Unknown project type: {projectType.ToString()}");
        }
    }


    protected override async Task SaveToStream(Stream stream)
    {
        var (po, vmo) = ResolveTypePair(this.Option.Type);
        var dialogModel =
            _mapper.Map<ProjectViewModel, ProjectPersistModel>(this, _ => { });
        await JsonSerializer.SerializeAsync(stream, dialogModel, po, SerializerOption);
    }

    public override object Clone()
    {
        using (var memoryStream = new MemoryStream())
        {
            this.SaveToStream(memoryStream).Wait();
            memoryStream.Seek(0, SeekOrigin.Begin);
            var cloneProject = LoadFromStream(memoryStream, _mapper).Result!;
            cloneProject.IsDataChanged = true;
            return cloneProject;
        }
    }

    public override ILLMSession CloneHeader()
    {
        var projectOption = (ProjectOption)this.Option.Clone();
        var client = this.Requester.DefaultClient.CloneClient();
        switch (projectOption.Type)
        {
            case ProjectType.CSharp:
                return _factory.CreateViewModel<CSharpProjectViewModel>(projectOption, client);
            case ProjectType.Default:
                return _factory.CreateViewModel<GeneralProjectViewModel>(projectOption, client);
            case ProjectType.Cpp:
                return _factory.CreateViewModel<CppProjectViewModel>(projectOption, client);
            default:
                throw new NotSupportedException();
        }
    }

    #endregion

    #region info

    public ProjectOption Option { get; }

    public string? UserSystemPrompt
    {
        get => _userSystemPrompt;
        set
        {
            if (value == _userSystemPrompt) return;
            _userSystemPrompt = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Context));
        }
    }

    private ObservableCollection<PromptEntry> _extendedSystemPrompts = [];

    public ObservableCollection<PromptEntry> ExtendedSystemPrompts
    {
        get => _extendedSystemPrompts;
        set
        {
            if (Equals(value, _extendedSystemPrompts)) return;
            _extendedSystemPrompts.CollectionChanged -= ExtendedSystemPromptsOnCollectionChanged;
            _extendedSystemPrompts = value;
            value.CollectionChanged += ExtendedSystemPromptsOnCollectionChanged;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Context));
        }
    }

    private readonly StringBuilder _systemPromptBuilder = new StringBuilder(1024);

    /// <summary>
    /// 项目级别的上下文，在Item间共享
    /// </summary>
    public virtual string? Context
    {
        get
        {
            _systemPromptBuilder.Clear();
            foreach (var promptEntry in ExtendedSystemPrompts)
            {
                _systemPromptBuilder.AppendLine(promptEntry.Prompt);
            }

            if (!string.IsNullOrEmpty(UserSystemPrompt))
            {
                _systemPromptBuilder.AppendLine(UserSystemPrompt);
            }

            _systemPromptBuilder.AppendLine("# 项目背景");
            _systemPromptBuilder.AppendFormat("这是一个名为{0}的{1}项目，项目代码位于文件夹{2}。", Option.Name,
                Option.Type.GetEnumDescription(), Option.RootPath);
            _systemPromptBuilder.AppendLine();
            _systemPromptBuilder.AppendLine(Option.Description);
            if (this.ProjectContextPrompt is { IncludeContext: true })
            {
                //todo: 当前只支持项目上下文
                _systemPromptBuilder.AppendLine("# 项目上下文：");
                _systemPromptBuilder.AppendLine(ProjectContextPrompt.TotalContext);
            }

            var contextSessions = this.Session
                .Where(model => model.EnableInContext && string.IsNullOrEmpty(model.Summary))
                .ToArray();
            if (contextSessions.Length != 0)
            {
                _systemPromptBuilder.AppendLine("以下是曾经完成的任务信息：");
                foreach (var contextSession in contextSessions)
                {
                    _systemPromptBuilder.Append("#");
                    _systemPromptBuilder.AppendLine(contextSession.Name);
                    _systemPromptBuilder.AppendLine(contextSession.Summary);
                }
            }

            return _systemPromptBuilder.ToString();
        }
    }

    /// <summary>
    /// 默认不提供项目上下文，派生类可重写以提供特定上下文
    /// </summary>
    public virtual ContextPromptViewModel? ProjectContextPrompt { get; }

    private long _tokensConsumption;

    public long TokensConsumption
    {
        get => _tokensConsumption;
        set
        {
            if (value == _tokensConsumption) return;
            _tokensConsumption = value;
            OnPropertyChanged();
        }
    }

    private double _totalPrice;

    public double TotalPrice
    {
        get => _totalPrice;
        set
        {
            if (value.Equals(_totalPrice)) return;
            _totalPrice = value;
            OnPropertyChanged();
        }
    }

    #endregion

    #region sessions

    public ICommand NewSessionCommand => new ActionCommand(_ =>
    {
        AddSession(new ProjectSessionViewModel(this, _mapper) { Name = "Empty Dialog" });
    });

    public void AddSession(ProjectSessionViewModel session)
    {
        session.PropertyChanged += OnSessionPropertyChanged;
        this.Session.Add(session);
    }

    public void RemoveSession(ProjectSessionViewModel session)
    {
        session.PropertyChanged -= OnSessionPropertyChanged;
        this.Session.Remove(session);
    }

    private void OnSessionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        var propertyName = e.PropertyName;
        switch (propertyName)
        {
            case nameof(ProjectSessionViewModel.EnableInContext):
                OnPropertyChanged(nameof(Context));
                break;
            case nameof(DialogSessionViewModel.IsBusy):
                OnPropertyChanged(nameof(IsBusy));
                break;
        }
    }

    public ObservableCollection<ProjectSessionViewModel> Session { get; set; }

    private ProjectSessionViewModel? _selectedSession;

    public ProjectSessionViewModel? SelectedSession
    {
        get => _selectedSession;
        set
        {
            if (Equals(value, _selectedSession)) return;
            _selectedSession = value;
            OnPropertyChanged();
            Requester.FunctionGroupSource = value;
            Requester.FunctionTreeSelector.Reset();
        }
    }

    #endregion

    private readonly string[] _notTrackingProperties =
    [
        nameof(EditTime),
        nameof(SelectedSession)
    ];

    private string? _userSystemPrompt;

    public ProjectViewModel(ProjectOption projectOption, ILLMChatClient modelClient, IMapper mapper,
        GlobalOptions options, IViewModelFactory factory, IEnumerable<ProjectSessionViewModel>? sessions = null)
    {
        this._mapper = mapper;
        _factory = factory;
        this.Option = projectOption;
        projectOption.PropertyChanged += ProjectOptionOnPropertyChanged;
        Requester = factory.CreateViewModel<RequesterViewModel>(modelClient,
            (Func<ILLMChatClient, IRequestItem, IRequestItem?, CancellationToken, Task<CompletedResult>>)GetResponse);
        var functionTreeSelector = Requester.FunctionTreeSelector;
        functionTreeSelector.ConnectDefault()
            .ConnectSource(new ProxyFunctionGroupSource(() => this.SelectedSession?.SelectedFunctionGroups));
        functionTreeSelector.AfterSelect += FunctionTreeSelectorOnAfterSelect;
        Requester.RequestCompleted += response =>
        {
            this.TokensConsumption += response.Tokens;
            this.TotalPrice += (response.Price ?? 0);
        };
        this.Session = [];
        if (sessions != null)
        {
            foreach (var session in sessions)
            {
                this.AddSession(session);
            }
        }

        this.PropertyChanged += (_, e) =>
        {
            var propertyName = e.PropertyName;
            if (_notTrackingProperties.Contains(propertyName))
            {
                return;
            }

            this.EditTime = DateTime.Now;
            IsDataChanged = true;
        };
        Session.CollectionChanged += OnCollectionChanged;
        _extendedSystemPrompts.CollectionChanged += ExtendedSystemPromptsOnCollectionChanged;
    }

    private void ProjectOptionOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(Option.Description):
                OnPropertyChanged(nameof(Context));
                OnPropertyChanged(nameof(UserSystemPrompt));
                break;
            case nameof(Option.RootPath):
                OnPropertyChanged(nameof(Context));
                break;
        }

        this.IsDataChanged = true;
    }

    private void ExtendedSystemPromptsOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        this.IsDataChanged = true;
        OnPropertyChanged(nameof(Context));
    }

    private void FunctionTreeSelectorOnAfterSelect()
    {
        if (this.SelectedSession == null)
        {
            return;
        }

        this.SelectedSession.SelectedFunctionGroups =
            this.Requester.FunctionTreeSelector.FunctionGroups.Where(tree => tree.IsSelected != false)
                .Select((tree => (CheckableFunctionGroupTree)tree.Clone())).ToArray();
        PopupBox.ClosePopupCommand.Execute(null, null);
    }

    protected virtual async Task<CompletedResult> GetResponse(ILLMChatClient arg1, IRequestItem arg2,
        IRequestItem? insertViewItem = null,
        CancellationToken token = default)
    {
        if (SelectedSession == null)
        {
            throw new NotSupportedException("未选择任务");
        }

        if (!this.Option.Check())
        {
            throw new InvalidOperationException("当前项目配置不合法");
        }

        this.ReadyForRequest();
        if (this.ProjectContextPrompt is { IncludeContext: true })
            await this.ProjectContextPrompt.BuildAsync();
        return await SelectedSession.NewResponse(arg1, arg2, insertViewItem, token);
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        this.IsDataChanged = true;
    }

    public void ReadyForRequest()
    {
        var functionGroups = this.SelectedSession?.SelectedFunctionGroups;
        if (functionGroups != null)
        {
            foreach (var aiFunctionGroup in functionGroups)
            {
                if (aiFunctionGroup.Data is FileSystemPlugin fileSystemPlugin)
                {
                    fileSystemPlugin.BypassPaths = Option.AllowedFolderPaths;
                }
            }
        }
    }
}