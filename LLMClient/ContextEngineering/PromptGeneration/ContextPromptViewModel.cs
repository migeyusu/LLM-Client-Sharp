// ContextPromptBuilder.cs

using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using LLMClient.Abstraction;
using LLMClient.Component.CustomControl;
using LLMClient.Component.ViewModel.Base;
using LLMClient.Project;

namespace LLMClient.ContextEngineering.PromptGeneration;

public abstract class ContextPromptViewModel : BaseViewModel, IDisposable
{
    public string? TotalContext
    {
        get => _totalContext;
        set
        {
            if (value == _totalContext) return;
            _totalContext = value;
            OnPropertyChanged();
        }
    }

    private string? _projectContext;

    /// <summary>
    /// 由于project/solution生成有明显延迟，所以不绑定到Context属性，使用特殊方法获取，UI也需要特殊触发
    /// </summary>
    /// <returns></returns>
    /// <exception cref="NotSupportedException"></exception>
    public string? ProjectContext
    {
        get => _projectContext;
        set
        {
            if (value == _projectContext) return;
            _projectContext = value;
            OnPropertyChanged();
        }
    }

    private int _projectContextTokens;

    public ICommand RefreshProjectContextCommand { get; }

    public int ProjectContextTokensCount
    {
        get => _projectContextTokens;
        set
        {
            if (value == _projectContextTokens) return;
            _projectContextTokens = value;
            OnPropertyChanged();
        }
    }

    private bool _isRefreshingContext;

    public bool IsRefreshingContext
    {
        get => _isRefreshingContext;
        set
        {
            if (value == _isRefreshingContext) return;
            _isRefreshingContext = value;
            OnPropertyChanged();
        }
    }
    
    private string? _totalContext;

    protected ContextPromptViewModel()
    {
        RefreshProjectContextCommand = new RelayCommand((async void () =>
        {
            IsRefreshingContext = true;
            try
            {
                this.ProjectContext = await BuildProjectContextAsync();
            }
            catch (Exception e)
            {
                MessageBoxes.Error(e.Message, "Error");
            }
            finally
            {
                IsRefreshingContext = false;
            }
        }));
    }

    public async Task BuildAsync()
    {
        this.ProjectContext = await BuildProjectContextAsync();
        var variables = new Dictionary<string, object?>
        {
            ["projectContext"] = ProjectContext,
            ["focusedContext"] = await BuildFocusedContextAsync(),
        };

        this.TotalContext = await PromptTemplateRenderer.RenderHandlebarsAsync(
            ContextPromptTemplates.CodeContextSectionTemplate,
            variables);
    }

    protected abstract Task<string> BuildFocusedContextAsync();

    protected abstract Task<string> BuildProjectContextAsync();

    public virtual void Dispose()
    {
    }
}

public abstract class ContextPromptViewModel<T> : ContextPromptViewModel
    where T : ProjectViewModel
{
    protected readonly T ProjectViewModel;

    protected ContextPromptViewModel(T projectViewModel)
        : base()
    {
        ProjectViewModel = projectViewModel;
    }
}