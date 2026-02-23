// ContextPromptBuilder.cs

using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using LLMClient.Abstraction;
using LLMClient.Component.ViewModel.Base;
using LLMClient.Project;

namespace LLMClient.ContextEngineering.Prompt;

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
            if (!string.IsNullOrEmpty(value))
            {
                CalculateProjectContextTokens(value, count => { ProjectContextTokensCount = (int)count; });
            }
        }
    }

    private int _projectContextTokens;

    public bool IncludeContext
    {
        get => _includeContext;
        set
        {
            if (value == _includeContext) return;
            _includeContext = value;
            OnPropertyChanged();
        }
    }

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

    private readonly ITokensCounter _tokensCounter;
    private string? _totalContext;
    private bool _includeContext = true;

    protected ContextPromptViewModel(ITokensCounter tokensCounter)
    {
        _tokensCounter = tokensCounter;
        RefreshProjectContextCommand = new RelayCommand((async void () =>
        {
            IsRefreshingContext = true;
            try
            {
                this.ProjectContext = await BuildProjectContextAsync();
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsRefreshingContext = false;
            }
        }));
    }

    private async void CalculateProjectContextTokens(string context, Action<long> callBack)
    {
        var countTokens = await _tokensCounter.CountTokens(context);
        callBack.Invoke(countTokens);
    }

    public async Task BuildAsync()
    {
        this.ProjectContext = await BuildProjectContextAsync();
        var variables = new Dictionary<string, object?>
        {
            ["projectContext"] = ProjectContext,
            ["focusedContext"] = await BuildFocusedContextAsync(),
            ["relevantSnippets"] = await BuildRelevantSnippetsAsync()
        };

        this.TotalContext = await PromptTemplateRenderer.RenderHandlebarsAsync(
            ContextPromptTemplates.CodeContextSectionTemplate,
            variables);
    }

    protected abstract Task<string> BuildRelevantSnippetsAsync();

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

    protected ContextPromptViewModel(T projectViewModel, ITokensCounter tokensCounter)
        : base(tokensCounter)
    {
        ProjectViewModel = projectViewModel;
    }
}