using System.ComponentModel;
using AutoMapper;
using LLMClient.Abstraction;
using LLMClient.Dialog;
using LLMClient.Dialog.Models;
using LLMClient.ToolCall;

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
public class ProjectSessionViewModel : DialogSessionViewModel, IFunctionGroupSource
{
    private string? _name;

    public override string? Name
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

    /// <summary>
    /// Task内的上下文
    /// </summary>
    public override string? SystemPrompt
    {
        get { return ParentProject.Context; }
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

    private string? _summary;

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

    public ProjectSessionViewModel(ProjectViewModel parentProject, IMapper mapper,
        IList<CheckableFunctionGroupTree>? functionGroupTrees = null,
        IDialogItem? rootNode = null, IDialogItem? currentLeaf = null)
        : base(rootNode, currentLeaf)
    {
        ParentProject = parentProject;
        SelectedFunctionGroups = functionGroupTrees;
        PropertyChanged += OnPropertyChanged;
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