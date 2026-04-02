using System.Windows.Input;
using LLMClient.Abstraction;
using LLMClient.Component.ViewModel.Base;
using LLMClient.Endpoints.OpenAIAPI;
using Microsoft.Xaml.Behaviors.Core;

namespace LLMClient.Endpoints;

public class EndpointInfoViewModel : BaseViewModel
{
    private readonly NewApiUsageQueryService _newApiUsageQueryService;
    private CancellationTokenSource? _refreshCancellationTokenSource;
    private ILLMAPIEndpoint? _selectedEndpoint;
    private NewApiUsageSnapshot? _usageSnapshot;
    private string? _unsupportedReason;
    private string? _errorMessage;
    private string? _queryUrl;
    private bool _isLoading;
    private DateTimeOffset? _lastRefreshAt;

    public IReadOnlyList<ILLMAPIEndpoint> Endpoints { get; }

    public ICommand RefreshCommand { get; }

    public ILLMAPIEndpoint? SelectedEndpoint
    {
        get => _selectedEndpoint;
        set
        {
            if (!SetField(ref _selectedEndpoint, value))
            {
                return;
            }

            NotifySelectionStateChanged();
            _ = LoadSelectedEndpointAsync();
        }
    }

    public NewApiUsageSnapshot? UsageSnapshot
    {
        get => _usageSnapshot;
        private set
        {
            if (SetField(ref _usageSnapshot, value))
            {
                OnPropertyChanged(nameof(HasUsageData));
            }
        }
    }

    public string? UnsupportedReason
    {
        get => _unsupportedReason;
        private set
        {
            if (SetField(ref _unsupportedReason, value))
            {
                OnPropertyChanged(nameof(ShowUnsupportedState));
            }
        }
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetField(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public string? QueryUrl
    {
        get => _queryUrl;
        private set
        {
            if (SetField(ref _queryUrl, value))
            {
                OnPropertyChanged(nameof(HasQueryUrl));
            }
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetField(ref _isLoading, value))
            {
                OnPropertyChanged(nameof(CanRefresh));
            }
        }
    }

    public DateTimeOffset? LastRefreshAt
    {
        get => _lastRefreshAt;
        private set => SetField(ref _lastRefreshAt, value);
    }

    public bool HasSelection => SelectedEndpoint != null;

    public bool ShowEmptyState => !HasSelection;

    public bool CanQuerySelectedEndpoint => !string.IsNullOrWhiteSpace(QueryUrl) && string.IsNullOrWhiteSpace(UnsupportedReason);

    public bool ShowUnsupportedState => HasSelection && !CanQuerySelectedEndpoint;

    public bool HasUsageData => UsageSnapshot != null;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool HasQueryUrl => !string.IsNullOrWhiteSpace(QueryUrl);

    public bool CanRefresh => CanQuerySelectedEndpoint && !IsLoading;

    public string SelectedEndpointTypeText => SelectedEndpoint switch
    {
        APIEndPoint { Option.ModelsSource: not global::LLMClient.Endpoints.Converters.ModelSource.None } apiEndPoint => apiEndPoint.Option.ModelsSource.ToString(),
        APIEndPoint => "OpenAI Compatible",
        { } endpoint => endpoint.GetType().Name,
        null => string.Empty,
    };

    public EndpointInfoViewModel(IEndpointService service, NewApiUsageQueryService newApiUsageQueryService)
    {
        _newApiUsageQueryService = newApiUsageQueryService;
        Endpoints = service.AllEndpoints;
        RefreshCommand = new ActionCommand(async _ => { await RefreshSelectedEndpointAsync(); });
    }

    public async Task RefreshSelectedEndpointAsync(CancellationToken cancellationToken = default)
    {
        CancelPendingRefresh();

        var selectedEndpoint = SelectedEndpoint;
        if (selectedEndpoint == null)
        {
            ResetUsageState();
            return;
        }

        if (!_newApiUsageQueryService.TryResolve(selectedEndpoint, out var queryUri, out var reason) || queryUri == null)
        {
            UsageSnapshot = null;
            ErrorMessage = null;
            LastRefreshAt = null;
            QueryUrl = null;
            UnsupportedReason = reason;
            NotifySelectionStateChanged();
            return;
        }

        QueryUrl = queryUri.ToString();
        UnsupportedReason = null;
        ErrorMessage = null;
        OnPropertyChanged(nameof(CanQuerySelectedEndpoint));
        OnPropertyChanged(nameof(ShowUnsupportedState));
        OnPropertyChanged(nameof(CanRefresh));

        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _refreshCancellationTokenSource = cts;

        try
        {
            IsLoading = true;
            var usageSnapshot = await _newApiUsageQueryService.QueryAsync(selectedEndpoint, cts.Token);
            if (cts.IsCancellationRequested || !ReferenceEquals(SelectedEndpoint, selectedEndpoint))
            {
                return;
            }

            UsageSnapshot = usageSnapshot;
            LastRefreshAt = usageSnapshot.RefreshedAt;
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            UsageSnapshot = null;
            LastRefreshAt = null;
            ErrorMessage = ex.Message;
        }
        finally
        {
            if (ReferenceEquals(_refreshCancellationTokenSource, cts))
            {
                _refreshCancellationTokenSource = null;
            }

            IsLoading = false;
            cts.Dispose();
        }
    }

    private async Task LoadSelectedEndpointAsync()
    {
        ResetUsageState();

        if (SelectedEndpoint == null)
        {
            return;
        }

        if (_newApiUsageQueryService.TryResolve(SelectedEndpoint, out var queryUri, out var reason) && queryUri != null)
        {
            QueryUrl = queryUri.ToString();
            UnsupportedReason = null;
            NotifySelectionStateChanged();
            await RefreshSelectedEndpointAsync();
            return;
        }

        UnsupportedReason = reason;
        NotifySelectionStateChanged();
    }

    private void ResetUsageState()
    {
        CancelPendingRefresh();
        IsLoading = false;
        UsageSnapshot = null;
        ErrorMessage = null;
        LastRefreshAt = null;
        QueryUrl = null;
        UnsupportedReason = null;
        NotifySelectionStateChanged();
    }

    private void CancelPendingRefresh()
    {
        _refreshCancellationTokenSource?.Cancel();
        _refreshCancellationTokenSource?.Dispose();
        _refreshCancellationTokenSource = null;
    }

    private void NotifySelectionStateChanged()
    {
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(ShowEmptyState));
        OnPropertyChanged(nameof(CanQuerySelectedEndpoint));
        OnPropertyChanged(nameof(ShowUnsupportedState));
        OnPropertyChanged(nameof(CanRefresh));
        OnPropertyChanged(nameof(SelectedEndpointTypeText));
    }
}
