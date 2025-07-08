using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.Metrics;
using LLMClient.Abstraction;
using LLMClient.UI.Dialog;
using LLMClient.UI.MCP.Servers;

namespace LLMClient.UI.Project;

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
 *
 */
public class ProjectTask : NotifyDataErrorInfoViewModelBase
{
    private string? _name;
    private string? _taskPrompt; //"请描述任务的内容和目标。";
    private string? _summary;

    public ProjectTask()
    {
    }

    public Guid ID { get; } = Guid.NewGuid();

    public string? Name
    {
        get => _name;
        set
        {
            if (value == _name) return;
            _name = value;
            OnPropertyChanged();
            ClearError();
            if (string.IsNullOrEmpty(value))
            {
                AddError("Name cannot be null or empty.");
            }
        }
    }

    public string? TaskPrompt
    {
        get => _taskPrompt;
        set
        {
            if (value == _taskPrompt) return;
            _taskPrompt = value;
            OnPropertyChanged();
            ClearError();
            if (string.IsNullOrEmpty(value))
            {
                AddError("TaskPrompt cannot be null or empty.");
            }
        }
    }

    /// <summary>
    /// Task内的上下文
    /// </summary>
    public string? Context
    {
        get
        {
            if (this.HasErrors)
            {
                return null;
            }

            if (string.IsNullOrEmpty(Name) || string.IsNullOrEmpty(TaskPrompt))
            {
                return null;
            }

            return $"这是一个{Type.GetEnumDescription()}任务，我希望你能帮助我完成它。{TaskPrompt}";
        }
    }

    /// <summary>
    /// summary of the task, used to summarize the task for the user.
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

    public ProjectTaskType Type { get; set; }

    public ProjectTaskStatus Status { get; set; } = ProjectTaskStatus.InProgress;

    public ObservableCollection<KernelFunctionGroup> LocalFunctions { get; set; } =
        new ObservableCollection<KernelFunctionGroup>();

    public ObservableCollection<IAIFunctionGroup> McpFunctions { get; set; } =
        new ObservableCollection<IAIFunctionGroup>();

    public DialogViewModel DialogViewModel { get; set; }
}

public enum ProjectTaskStatus
{
    InProgress,
    Completed,
    RolledBack
}

public enum ProjectTaskType
{
    [Description("需求变更")] NewDemand,
    [Description("修复Bug")] BugFix,
    [Description("代码翻译")] Translation,
    [Description("代码重构")] CodeRefactor,
    [Description("代码审查")] CodeReview,
    [Description("代码生成")] CodeGeneration,
    [Description("代码优化")] CodeOptimization,
    [Description("代码文档编写")] CodeDocumentation,
    [Description("单元测试编写")] UnitTestConstruction,
}