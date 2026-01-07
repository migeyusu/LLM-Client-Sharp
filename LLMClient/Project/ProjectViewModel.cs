using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Input;
using AutoMapper;
using CommunityToolkit.Mvvm.Input;
using DocumentFormat.OpenXml.Wordprocessing;
using Google.Apis.Util;
using LLMClient.Abstraction;
using LLMClient.Component.Converters;
using LLMClient.Component.ViewModel;
using LLMClient.Configuration;
using LLMClient.ContextEngineering;
using LLMClient.Data;
using LLMClient.Dialog;
using LLMClient.Endpoints;
using LLMClient.ToolCall;
using LLMClient.ToolCall.Servers;
using MaterialDesignThemes.Wpf;
using Microsoft.Xaml.Behaviors.Core;

namespace LLMClient.Project;

[TypeConverter(typeof(EnumDescriptionTypeConverter))]
[JsonConverter(typeof(JsonStringEnumConverter<ProjectTaskType>))]  
public enum ProjectType
{
    [Description("代码")] Standard,
    [Description("C#")] CSharp,
    [Description("C++")] Cpp
}

public class ProjectViewModel : FileBasedSessionBase, ILLMSessionLoader<ProjectViewModel>, IPromptableSession
{
    public const string SaveDir = "Projects";

    private readonly IMapper _mapper;


    private bool _isDataChanged = true;

    public override bool IsDataChanged
    {
        get { return Tasks.Any(task => task.IsDataChanged) || Requester.IsDataChanged || _isDataChanged; }
        set
        {
            _isDataChanged = value;
            if (!value)
            {
                //用于重置子项的变更状态
                foreach (var projectTask in this.Tasks)
                {
                    projectTask.IsDataChanged = value;
                }

                Requester.IsDataChanged = value;
            }
        }
    }

    public override bool IsBusy
    {
        get { return Tasks.Any(task => task.IsBusy); }
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

            var typeText = root[nameof(ProjectPersistModel.Type)]?.GetValue<string>();
            var (poType, viewmodelType) = ResolveTypePair(typeText);
            var persistModel = (ProjectPersistModel?)root.Deserialize(poType, SerializerOption);
            if (persistModel == null)
            {
                throw new Exception($"Project type mismatch: {typeText} != {poType}");
            }

            var viewModel = mapper.Map<ProjectPersistModel, ProjectViewModel>(persistModel, (_ => { }));
            if (viewModel.GetType() != viewmodelType)
            {
                throw new Exception($"Project mapping failed: {typeText} != {viewmodelType}");
            }

            viewModel.IsDataChanged = false;
            return viewModel;
        }
        catch (Exception e)
        {
            Trace.TraceError("Failed to load project: " + e.Message);
            return null;
        }
    }

    private static Tuple<Type, Type> ResolveTypePair(string? typeText)
    {
        if (string.IsNullOrWhiteSpace(typeText))
        {
            return new Tuple<Type, Type>(typeof(ProjectPersistModel), typeof(ProjectViewModel));
        }

        if (Enum.TryParse(typeText, true, out ProjectType projectType))
        {
            switch (projectType)
            {
                case ProjectType.Standard:
                    return new Tuple<Type, Type>(typeof(ProjectPersistModel), typeof(ProjectViewModel));
                case ProjectType.CSharp:
                    return new Tuple<Type, Type>(typeof(CSharpProjectPersistModel), typeof(CSharpProjectViewModel));
                case ProjectType.Cpp:
                    return new Tuple<Type, Type>(typeof(CppProjectPersistModel), typeof(CppProjectViewModel));
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        throw new Exception($"Unknown project type: {typeText}");
    }


    protected override async Task SaveToStream(Stream stream)
    {
        var (po, vmo) = ResolveTypePair(this.Option.Type.ToString());
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
    /// 项目级别的上下文，在task间共享
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

            _systemPromptBuilder.AppendFormat("这是一个名为{0}的{1}项目，项目代码位于文件夹{2}。", Option.Name,
                Option.Type.GetEnumDescription(), Option.FolderPath);
            _systemPromptBuilder.AppendLine();
            _systemPromptBuilder.AppendLine("项目背景/描述如下：");
            _systemPromptBuilder.AppendLine(Option.Description);
            if (this.ProjectContextPrompt is { IncludeContext: true })
            {
                //todo: 当前只支持项目上下文
                _systemPromptBuilder.AppendLine("项目上下文：");
                _systemPromptBuilder.AppendLine(ProjectContextPrompt.TotalContext);
            }

            var contextTasks = this.Tasks
                .Where(model => model.EnableInContext && string.IsNullOrEmpty(model.Summary))
                .ToArray();
            if (contextTasks.Any())
            {
                _systemPromptBuilder.AppendLine("以下是与当前任务相关的信息：");
                foreach (var projectTaskViewModel in contextTasks)
                {
                    _systemPromptBuilder.Append("#");
                    _systemPromptBuilder.AppendLine(projectTaskViewModel.Name);
                    _systemPromptBuilder.AppendLine(projectTaskViewModel.Summary);
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

    #region tasks

    public ICommand NewTaskCommand => new ActionCommand(_ => { AddTask(new ProjectTaskViewModel(this, _mapper)); });

    public void AddTask(ProjectTaskViewModel task)
    {
        task.PropertyChanged += OnTaskPropertyChanged;
        this.Tasks.Add(task);
    }

    public void RemoveTask(ProjectTaskViewModel task)
    {
        task.PropertyChanged -= OnTaskPropertyChanged;
        this.Tasks.Remove(task);
    }

    private void OnTaskPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        var propertyName = e.PropertyName;
        switch (propertyName)
        {
            case nameof(ProjectTaskViewModel.EnableInContext):
                OnPropertyChanged(nameof(Context));
                break;
            case nameof(DialogSessionViewModel.IsBusy):
                OnPropertyChanged(nameof(IsBusy));
                break;
        }
    }

    public ObservableCollection<ProjectTaskViewModel> Tasks { get; set; }

    private ProjectTaskViewModel? _selectedTask;

    public ProjectTaskViewModel? SelectedTask
    {
        get => _selectedTask;
        set
        {
            if (Equals(value, _selectedTask)) return;
            _selectedTask = value;
            OnPropertyChanged();
            Requester.FunctionGroupSource = value;
            Requester.FunctionTreeSelector.Reset();
        }
    }

    #endregion

    private readonly string[] _notTrackingProperties =
    [
        nameof(EditTime),
        nameof(SelectedTask)
    ];


    private string? _userSystemPrompt;


    public ProjectViewModel(ProjectOption projectOption, ILLMChatClient modelClient, IMapper mapper,
        GlobalOptions options,
        IRagSourceCollection ragSourceCollection, IEnumerable<ProjectTaskViewModel>? tasks = null)
    {
        this._mapper = mapper;
        this.Option = projectOption;
        projectOption.PropertyChanged += ProjectOptionOnPropertyChanged;
        Requester = new RequesterViewModel(modelClient, GetResponse, options, ragSourceCollection)
        {
            FunctionTreeSelector =
            {
                FunctionSelected = true,
            }
        };

        var functionTreeSelector = Requester.FunctionTreeSelector;
        functionTreeSelector.ConnectDefault()
            .ConnectSource(new ProxyFunctionGroupSource(() => this.SelectedTask?.SelectedFunctionGroups));
        functionTreeSelector.AfterSelect += FunctionTreeSelectorOnAfterSelect;
        Requester.RequestCompleted += response =>
        {
            this.TokensConsumption += response.Tokens;
            this.TotalPrice += (response.Price ?? 0);
        };
        this.Tasks = [];
        if (tasks != null)
        {
            foreach (var projectTask in tasks)
            {
                this.AddTask(projectTask);
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
        Tasks.CollectionChanged += OnCollectionChanged;
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
            case nameof(Option.FolderPath):
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
        if (this.SelectedTask == null)
        {
            return;
        }

        this.SelectedTask.SelectedFunctionGroups =
            this.Requester.FunctionTreeSelector.FunctionGroups.Where(tree => tree.IsSelected != false)
                .Select((tree => (CheckableFunctionGroupTree)tree.Clone())).ToArray();
        PopupBox.ClosePopupCommand.Execute(null, null);
    }

    protected virtual async Task<CompletedResult> GetResponse(ILLMChatClient arg1, IRequestItem arg2, int? index = null,
        CancellationToken token = default)
    {
        if (SelectedTask == null)
        {
            throw new NotSupportedException("未选择任务");
        }

        if (!SelectedTask.Validate())
        {
            throw new InvalidOperationException("当前任务配置不合法");
        }

        if (!this.Option.Check())
        {
            throw new InvalidOperationException("当前项目配置不合法");
        }

        this.Ready();
        if (this.ProjectContextPrompt != null)
            await this.ProjectContextPrompt.BuildAsync();
        return await SelectedTask.NewRequest(arg1, arg2, index, token);
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        this.IsDataChanged = true;
    }

    public void Ready()
    {
        var functionGroups = this.SelectedTask?.SelectedFunctionGroups;
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

    public void ForkPreTask(IDialogItem? dialogViewItem)
    {
        var projectTaskViewModel = this.SelectedTask;
        if (projectTaskViewModel == null)
        {
            return;
        }

        var dialogSessionClone =
            _mapper.Map<ProjectTaskViewModel, ProjectTaskPersistModel>(projectTaskViewModel, _ => { });
        if (dialogSessionClone == null)
        {
            return;
        }

        if (dialogViewItem != null)
        {
            var findIndex = projectTaskViewModel.DialogItems.IndexOf(dialogViewItem);
            if (findIndex >= 0)
            {
                dialogSessionClone.DialogItems = dialogSessionClone.DialogItems?.Take(findIndex + 1).ToArray();
            }
        }

        var cloneSession =
            _mapper.Map<ProjectTaskPersistModel, ProjectTaskViewModel>(dialogSessionClone,
                (options => { options.Items[AutoMapModelTypeConverter.ParentProjectViewModelKey] = this; }));
        cloneSession.IsDataChanged = true;
        this.AddTask(cloneSession);
    }
}