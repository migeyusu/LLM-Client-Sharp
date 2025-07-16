using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using System.Windows.Forms;
using System.Windows.Input;
using LLMClient.Abstraction;
using LLMClient.Endpoints;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Xaml.Behaviors.Core;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace LLMClient.UI.Dialog;

public class RequesterViewModel : BaseViewModel
{
    /// <summary>
    /// indicate whether data is changed after loading.
    /// </summary>
    public bool IsDataChanged { get; set; } = true;

    private bool _isNewResponding;

    public bool IsNewResponding
    {
        get => _isNewResponding;
        private set
        {
            if (value == _isNewResponding) return;
            _isNewResponding = value;
            OnPropertyChanged();
        }
    }

    public ICommand NewRequestCommand => new ActionCommand(async o =>
    {
        var requestViewItem = this.NewRequest();
        if (requestViewItem == null)
        {
            return;
        }

        IsNewResponding = true;
        var completedResult = await _getResponse.Invoke(this.DefaultClient, requestViewItem);
        IsNewResponding = false;
        if (!completedResult.IsInterrupt)
        {
            ClearRequest();
        }
    });

    public ICommand ConclusionCommand => new ActionCommand(async o =>
    {
        var summaryRequest = SummaryRequestViewItem.NewSummaryRequest();
        await _getResponse.Invoke(this._defaultClient, summaryRequest);
    });

    public ICommand ChangeModelCommand => new ActionCommand(async o =>
    {
        var service = ServiceLocator.GetService<IEndpointService>()!;
        var selectionViewModel = new ModelSelectionPopupViewModel(service);
        if (await DialogHost.Show(selectionViewModel) is true)
        {
            var model = selectionViewModel.GetClient();
            if (model == null)
            {
                MessageBox.Show("No model created!");
                return;
            }

            this.DefaultClient = model;
        }
    });

    private ILLMClient _defaultClient;

    public ILLMClient DefaultClient
    {
        get => _defaultClient;
        set
        {
            if (Equals(value, _defaultClient)) return;
            if (_defaultClient is INotifyPropertyChanged oldValue)
            {
                oldValue.PropertyChanged -= TagDataChanged;
            }

            if (_defaultClient.Parameters is INotifyPropertyChanged oldParameters)
            {
                oldParameters.PropertyChanged -= TagDataChanged;
            }

            _defaultClient = value;
            OnPropertyChanged();
            TrackClientChanged(value);
        }
    }

    #region attachment

    public ObservableCollection<Attachment> Attachments { get; set; } =
        new ObservableCollection<Attachment>();

    public ICommand AddImageCommand => new ActionCommand(o =>
    {
        var openFileDialog = new OpenFileDialog()
        {
            Filter = "Image files (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg",
            Multiselect = true
        };
        if (openFileDialog.ShowDialog() != true)
        {
            return;
        }

        foreach (var fileName in openFileDialog.FileNames)
        {
            this.Attachments.Add(new Attachment()
            {
                Type = AttachmentType.Image,
                OriUri = new Uri(fileName)
            });
        }
    });

    public ICommand RemoveAttachmentCommand => new ActionCommand(o =>
    {
        if (o is Attachment attachment)
        {
            this.Attachments.Remove(attachment);
        }
    });

    #endregion

    #region function call

    private bool _mcpEnabled;

    public bool MCPEnabled
    {
        get => _mcpEnabled;
        set
        {
            if (value == _mcpEnabled) return;
            _mcpEnabled = value;
            OnPropertyChanged();
        }
    }

    public IList<IAIFunctionGroup>? SelectedFunctions { get; set; }

    #endregion

    #region input

    private string? _promptString;

    public string? PromptString
    {
        get => _promptString;
        set
        {
            if (value == _promptString) return;
            _promptString = value;
            OnPropertyChanged();
        }
    }

    #endregion

    public Task GetResponse(IRequestItem requestViewItem)
    {
        IsNewResponding = true;
        return _getResponse.Invoke(this.DefaultClient, requestViewItem)
            .ContinueWith(t => IsNewResponding = false);
    }

    public Task GetSummary()
    {
        var summaryRequest = SummaryRequestViewItem.NewSummaryRequest();
        return GetResponse(summaryRequest);
    }

    private readonly Func<ILLMClient, IRequestItem, Task<CompletedResult>> _getResponse;

    public RequesterViewModel(ILLMClient modelClient,
        Func<ILLMClient, IRequestItem,Task<CompletedResult>> getResponse)
    {
        this._defaultClient = modelClient;
        _getResponse = getResponse;
        this.TrackClientChanged(modelClient);
    }

    private void TrackClientChanged(ILLMClient client)
    {
        if (client is INotifyPropertyChanged newValue)
        {
            newValue.PropertyChanged += TagDataChanged;
        }

        if (_defaultClient.Parameters is INotifyPropertyChanged newParameters)
        {
            newParameters.PropertyChanged += TagDataChanged;
        }
    }

    public void ClearRequest()
    {
        this.Attachments.Clear();
        this.PromptString = null;
    }

    public RequestViewItem? NewRequest(string additionalPrompt = "")
    {
        if (string.IsNullOrEmpty(this.PromptString))
        {
            return null;
        }

        var promptBuilder = new StringBuilder();
        if (!string.IsNullOrEmpty(additionalPrompt))
        {
            promptBuilder.Append(additionalPrompt);
        }

        promptBuilder.Append(PromptString);
        IList<IAIFunctionGroup>? tools = null;
        if (this.MCPEnabled)
        {
            tools = this.SelectedFunctions;
        }

        return new RequestViewItem()
        {
            InteractionId = Guid.NewGuid(),
            TextMessage = promptBuilder.ToString().Trim(),
            Attachments = Attachments.ToList(),
            FunctionGroups = tools,
        };
    }

    private void TagDataChanged(object? sender, PropertyChangedEventArgs e)
    {
        IsDataChanged = true;
    }
}