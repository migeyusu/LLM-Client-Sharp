using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows.Input;
using System.Xml;
using System.Xml.Serialization;
using AutoMapper;
using LLMClient.Abstraction;
using LLMClient.Component.Converters;
using LLMClient.Component.CustomControl;
using LLMClient.Component.Utility;
using LLMClient.Component.ViewModel;
using LLMClient.Configuration;
using LLMClient.ContextEngineering.PromptGeneration;
using LLMClient.Dialog;
using LLMClient.Dialog.Models;
using LLMClient.Persistence;
using LLMClient.ToolCall;
using LLMClient.ToolCall.DefaultPlugins;
using MaterialDesignThemes.Wpf;
using Microsoft.Xaml.Behaviors.Core;

namespace LLMClient.Project;

[TypeConverter(typeof(EnumDescriptionTypeConverter))]
public enum ProjectType : int
{
    [Description("C#")] CSharp = 1,
    [Description("C++")] Cpp = 2
}

public abstract class ProjectViewModel : FileBasedSessionBase,
    ILoadableSessionFile<ProjectViewModel>, IPromptableSession
{
    public const string SaveDir = "Projects";

    private readonly IMapper _mapper;
    private readonly IViewModelFactory _factory;

    public override bool IsDataChanged
    {
        get { return Session.Any(session => session.IsDataChanged) || Requester.IsDataChanged || field; }
        set
        {
            field = value;
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
    } = true;

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

    public abstract IEnumerable<Type> ProjectAgents { get; }

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

            var typeInt = root["ProjectOptions"]?[nameof(ProjectOptionsPersistModel.Type)]?.GetValue<int>() ?? 1;
            var type = Enum.IsDefined(typeof(ProjectType), typeInt) ? (ProjectType)typeInt : ProjectType.CSharp;
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
            case ProjectType.CSharp:
                return new Tuple<Type, Type>(typeof(CSharpProjectPersistModel), typeof(CSharpProjectViewModel));
            case ProjectType.Cpp:
                return new Tuple<Type, Type>(typeof(CppProjectPersistModel), typeof(CppProjectViewModel));
            default:
                return new Tuple<Type, Type>(typeof(CSharpProjectPersistModel), typeof(CSharpProjectViewModel));
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

    public override ILLMSessionFile CloneHeader()
    {
        var projectOption = (ProjectOption)this.Option.Clone();
        var client = this.Requester.DefaultClient.CloneClient();
        switch (projectOption.Type)
        {
            case ProjectType.CSharp:
                return _factory.CreateViewModel<CSharpProjectViewModel>(projectOption, client);
            case ProjectType.Cpp:
                return _factory.CreateViewModel<CppProjectViewModel>(projectOption, client);
            default:
                return _factory.CreateViewModel<CSharpProjectViewModel>(projectOption, client);
        }
    }

    #endregion

    #region info

    public ProjectOption Option { get; }

    public string? UserSystemPrompt
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
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

    private readonly StringBuilder _systemPromptBuilder = new(1024);

    private static readonly XmlSerializer _projectInfoSerializer = new(typeof(ProjectInformation));
    private static readonly XmlSerializerNamespaces _emptyNamespaces = CreateEmptyNamespaces();

    private static XmlSerializerNamespaces CreateEmptyNamespaces()
    {
        var ns = new XmlSerializerNamespaces();
        ns.Add(string.Empty, string.Empty);
        return ns;
    }

    public string ProjectInformationPrompt
    {
        get
        {
            var projectInfo = new ProjectInformation
            {
                Project = new ProjectInfo
                {
                    Name = Option.Name ?? string.Empty,
                    Type = Option.Type.GetEnumDescription(),
                    Path = Option.RootPath ?? string.Empty,
                    Description = string.IsNullOrEmpty(Option.Description) ? null : Option.Description
                }
            };

            if (Option.IncludeAgentsMd && !string.IsNullOrEmpty(Option.RootPath))
            {
                var agentsMdPath = Path.Combine(Option.RootPath, "AGENTS.md");
                if (File.Exists(agentsMdPath))
                {
                    try
                    {
                        var agentsMdContent = File.ReadAllText(agentsMdPath);
                        projectInfo.AgentsRules = new AgentsRules { Content = agentsMdContent };
                    }
                    catch (Exception e)
                    {
                        Trace.TraceError("Failed to read AGENTS.md: " + e.Message);
                    }
                }
            }

            using var ms = new MemoryStream();
            using (var writer = new StreamWriter(ms, Encoding.UTF8, leaveOpen: true))
            {
                using var xmlWriter = XmlWriter.Create(writer,
                    new XmlWriterSettings { Indent = true, IndentChars = "    ", OmitXmlDeclaration = true });
                _projectInfoSerializer.Serialize(xmlWriter, projectInfo, _emptyNamespaces);
            }

            return Encoding.UTF8.GetString(ms.ToArray());
        }
    }

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

            _systemPromptBuilder.AppendLine(ProjectInformationPrompt);

            return _systemPromptBuilder.ToString();
        }
    }

    /// <summary>
    /// 默认不提供项目上下文
    /// </summary>
    public virtual ContextPromptViewModel? ProjectContext { get; }

    public virtual IEnumerable<IAIFunctionGroup> GetInspectorFunctionGroups()
    {
        yield break;
    }

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
        var projectSessionViewModel = _factory.CreateViewModel<ProjectSessionViewModel>(this, _mapper);
        AddSession(projectSessionViewModel);
    });

    public async Task ImportFromDialogSessions(IReadOnlyList<DialogViewModel> availableDialogs)
    {
        try
        {
            var dialogSessions = availableDialogs
                .Where(d => d.DialogItems.Count > 0)
                .ToList();

            if (!dialogSessions.Any())
            {
                MessageEventBus.Publish("没有可用的对话会话");
                return;
            }

            // Show selection dialog
            var result = await DialogHost.Show(new ImportDialogSelectionViewModel(dialogSessions));
            if (result is not DialogViewModel selectedDialog)
            {
                return;
            }

            // Serialize selected dialog to persist model
            var dialogPersist = _mapper.Map<DialogViewModel, DialogSessionPersistModel>(selectedDialog, _ => { });

            // Strip per-request FunctionGroups from dialog items:
            // These reference external tools (MCP servers, plugins) that may not be resolvable
            // in the project context and are not needed for imported conversation content.
            if (dialogPersist.DialogItems != null)
            {
                foreach (var item in dialogPersist.DialogItems)
                {
                    if (item is RequestPersistItem requestPersist)
                    {
                        requestPersist.FunctionGroups = null;
                    }
                }
            }

            // Create ProjectSessionPersistModel (dialog items only, no system prompts)
            var sessionPersist = new ProjectSessionPersistModel
            {
                DialogItems = dialogPersist.DialogItems,
                RootNode = dialogPersist.RootNode,
                CurrentLeaf = dialogPersist.CurrentLeaf,
                TokensConsumption = dialogPersist.TokensConsumption,
                TotalPrice = dialogPersist.TotalPrice,
                Name = selectedDialog.Topic,
                // AllowedFunctions intentionally not copied
            };

            // Map back to ProjectSessionViewModel
            var projectSession = _mapper.Map<ProjectSessionPersistModel, ProjectSessionViewModel>(sessionPersist,
                opts => opts.Items[AutoMapModelTypeConverter.ParentProjectViewModelKey] = this);

            AddSession(projectSession);
            MessageEventBus.Publish($"已从对话「{selectedDialog.Topic}」导入");
        }
        catch (Exception e)
        {
            var message = e.InnerException != null
                ? $"{e.Message}\n{e.InnerException.Message}"
                : e.Message;
            Trace.TraceError(e.ToString());
            MessageBoxes.Error("导入失败: " + message);
        }
    }

    public void AddSession(ProjectSessionViewModel session)
    {
        session.PropertyChanged += OnSessionPropertyChanged;
        this.Session.Add(session);
        SelectedSession = session;
    }

    public void RemoveSession(ProjectSessionViewModel session)
    {
        session.PropertyChanged -= OnSessionPropertyChanged;
        this.Session.Remove(session);
        if (SelectedSession == session)
        {
            SelectedSession = this.Session.LastOrDefault();
        }
    }

    private void OnSessionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        var propertyName = e.PropertyName;
        switch (propertyName)
        {
            case nameof(DialogSessionViewModel.IsBusy):
                OnPropertyChanged(nameof(IsBusy));
                break;
        }
    }

    public ObservableCollection<ProjectSessionViewModel> Session { get; set; }

    public ProjectSessionViewModel? SelectedSession
    {
        get;
        set
        {
            if (Equals(value, field)) return;
            field = value;
            OnPropertyChanged();
            Requester.ConnectedSession = value;
        }
    }

    #endregion

    private readonly string[] _notTrackingProperties =
    [
        nameof(EditTime),
        nameof(SelectedSession)
    ];

    protected ProjectViewModel(ProjectOption projectOption, string initialPrompt, ILLMChatClient modelClient,
        IMapper mapper, GlobalOptions options, IViewModelFactory factory,
        IEnumerable<ProjectSessionViewModel>? sessions = null)
    {
        this._mapper = mapper;
        _factory = factory;
        this.Option = projectOption;
        projectOption.PropertyChanged += ProjectOptionOnPropertyChanged;
        Requester = factory.CreateViewModel<RequesterViewModel>(initialPrompt, modelClient,
            new Func<ITextDialogSession?>(() => this.SelectedSession));
        var functionTreeSelector = Requester.FunctionTreeSelector;
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
            case nameof(Option.IncludeAgentsMd):
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
                .Select(tree => (CheckableFunctionGroupTree)tree.Clone()).ToArray();
        PopupBox.ClosePopupCommand.Execute(null, null);
    }

    private async Task<IResponse> GetResponse(RequestOption requestOption,
        IRequestItem? insertViewItem = null,
        CancellationToken token = default)
    {
        if (SelectedSession == null)
        {
            throw new NotSupportedException("未选择任务");
        }

        return await SelectedSession.NewResponse(requestOption, insertViewItem, token);
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        this.IsDataChanged = true;
    }

    public bool IsPreprocessing
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public abstract IAIFunctionGroup[] ProjectTools { get; }

    public virtual async Task PreviewProcessing(CancellationToken token = default)
    {
        if (!this.Option.Check())
        {
            throw new InvalidOperationException("当前项目配置不合法");
        }

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

    public virtual bool TryResolvePersistedFunctionGroup(AIFunctionGroupDefinitionPersistModel persistModel,
        out IAIFunctionGroup? functionGroup)
    {
        functionGroup = null;
        return false;
    }
}