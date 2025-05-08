using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using System.Windows.Media;
using LLMClient.UI;
using Microsoft.Extensions.AI;

namespace LLMClient.Endpoints;

public abstract class LlmClientBase : BaseViewModel, ILLMModelClient
{
    [JsonIgnore] public virtual ChatMessage? Message { get; } = null;

    [JsonIgnore] public virtual bool IsEnable { get; } = false;
    [JsonIgnore] public long Tokens { get; } = 0;

    public abstract string Name { get; }

    public abstract ILLMEndpoint Endpoint { get; }
    public abstract ImageSource? Icon { get; }

    private bool _isResponding;

    [JsonIgnore]
    public bool IsResponding
    {
        get => _isResponding;
        protected set
        {
            if (value == _isResponding) return;
            _isResponding = value;
            OnPropertyChanged();
        }
    }

    [JsonIgnore]
    public virtual ILLMModel? Info
    {
        get { return null; }
    }

    [JsonIgnore] public virtual ObservableCollection<string> PreResponse { get; } = new ObservableCollection<string>();

    public long TokensConsumption
    {
        get => _totalTokens;
        set
        {
            if (value == _totalTokens) return;
            _totalTokens = value;
            OnPropertyChanged();
        }
    }

    private long _totalTokens;

    public abstract void Deserialize(IModelParams info);

    public abstract IModelParams Serialize();

    public abstract Task<CompletedResult> SendRequest(IEnumerable<IDialogViewItem> dialogItems,
        CancellationToken cancellationToken = default);
}