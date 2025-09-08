using System.ComponentModel;
using System.Text;
using AutoMapper;
using LLMClient.Abstraction;
using LLMClient.Dialog;
using LLMClient.MCP;
using LLMClient.MCP.Servers;

namespace LLMClient.Project;

/**
 * 基本思路，基于以下几个前提：
 * 1.LLM对话的项目更改，由于对话的结果往往需要修改，所以需要按需应用代码到项目中。
 * 2.项目的更改可能会被拆分为多个独立任务，其他任务只需要知道最终结果而不关心过程。
 * 3.可能需要回滚更改（暂定）
 * 所以将项目组织为一个任务列表，每个任务代表一个独立的更改，内含大量对话，任务间共享公共上下文且会将本次完成的任务加入上下文。而任务内共享一个连续的上下文。
 * 任务对项目的更改，如果不可回滚，那么便不可删除记录。
 * 任务的状态分为：进行中、已完成、已回滚。
 * 工程本身是一个抽象类，可以基于不同的MCP特化到不同功能
 * 首先尝试工程，然后演进到Agent workflow
 * 本质上，project的目的是上下文和任务的组织和管理。
 * 待实现的功能：
 * 1. 如何构建工程级别的上下文
 * 2. 如何构建任务级别的上下文
 */
public class ProjectTaskViewModel : DialogSessionViewModel, IFunctionGroupSource
{
    private string? _name;
    private string? _summary;
    private ProjectTaskType _type;
    private string? _description;

    public IList<CheckableFunctionGroupTree>? SelectedFunctionGroups
    {
        get => _selectedFunctionGroups;
        set
        {
            if (Equals(value, _selectedFunctionGroups)) return;
            _selectedFunctionGroups = value;
            OnPropertyChanged();
        }
    }

    public string? Name
    {
        get => _name;
        set
        {
            if (value == _name) return;
            ClearError();
            if (string.IsNullOrEmpty(value))
            {
                AddError("Name cannot be null or empty.");
                return;
            }

            _name = value;
            OnPropertyChanged();
        }
    }

    public string? Description
    {
        get => _description;
        set
        {
            if (value == _description) return;
            ClearError();
            if (string.IsNullOrEmpty(value))
            {
                AddError("Description cannot be null or empty.");
                return;
            }

            _description = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SystemPrompt));
        }
    }


    private readonly StringBuilder _systemPromptBuilder = new StringBuilder(1024);
    private IList<CheckableFunctionGroupTree>? _selectedFunctionGroups;

    public ProjectPromptTemplateViewModel PromptTemplate { get; }

    /// <summary>
    /// Task内的上下文
    /// </summary>
    public override string? SystemPrompt
    {
        get
        {
            if (!Validate())
            {
                return null;
            }

            _systemPromptBuilder.Clear();
            _systemPromptBuilder.AppendLine(ParentProject.Context);
            _systemPromptBuilder.AppendFormat("现在有一个{0}类型的任务，我希望你能帮助我完成它。", Type.GetEnumDescription());
            _systemPromptBuilder.AppendLine();
            _systemPromptBuilder.Append("任务描述：");
            _systemPromptBuilder.AppendLine(Description);
            return _systemPromptBuilder.ToString();
        }
        set => throw new NotSupportedException();
    }

    public ProjectViewModel ParentProject { get; }

    /// <summary>
    /// indicate whether enable this task in context when generate response.
    /// </summary>
    public bool EnableInContext
    {
        get => _enableInContext;
        set
        {
            if (value == _enableInContext) return;
            _enableInContext = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// summary of the task, when task end, generate for total context.
    /// </summary>
    public string? Summary
    {
        get => _summary;
        set
        {
            if (value == _summary) return;
            _summary = value;
            OnPropertyChanged();
        }
    }

    public ProjectTaskType Type
    {
        get => _type;
        set
        {
            if (value == _type) return;
            _type = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SystemPrompt));
        }
    }

    public ProjectTaskViewModel(ProjectViewModel parentProject, IMapper mapper,
        IList<CheckableFunctionGroupTree>? functionGroupTrees = null,
        IList<IDialogItem>? items = null) : base(mapper, items)
    {
        ParentProject = parentProject;
        SelectedFunctionGroups = functionGroupTrees;
        PropertyChanged += OnPropertyChanged;
        this.PromptTemplate = new ProjectPromptTemplateViewModel(this);
    }

    private readonly string[] _notTrackingProperties =
    [
        nameof(ScrollViewItem),
        nameof(SearchText)
    ];

    private bool _enableInContext;

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        var propertyName = e.PropertyName;
        if (_notTrackingProperties.Contains(propertyName))
        {
            return;
        }

        IsDataChanged = true;
    }


    public bool Validate()
    {
        if (this.HasErrors)
        {
            return false;
        }

        if (string.IsNullOrEmpty(Name))
        {
            AddError("Name cannot be null or empty.", nameof(Name));
            return false;
        }

        if (string.IsNullOrEmpty(Description))
        {
            AddError("Description cannot be null or empty.", nameof(Description));
            return false;
        }

        return true;
    }

    public IEnumerable<IAIFunctionGroup> GetFunctionGroups()
    {
        if (SelectedFunctionGroups == null)
        {
            yield break;
        }

        foreach (var checkableFunctionGroupTree in SelectedFunctionGroups)
        {
            checkableFunctionGroupTree.RefreshCheckState();
            if (checkableFunctionGroupTree.IsSelected != false)
            {
                yield return checkableFunctionGroupTree;
            }
        }
    }
}

public class ProjectPromptTemplateViewModel
{
    public ProjectPromptTemplateViewModel(ProjectTaskViewModel projectTask)
    {
        Task = projectTask;
        Project = projectTask.ParentProject;
    }

    public ProjectTaskViewModel Task { get; }

    public ProjectViewModel Project { get; }
}