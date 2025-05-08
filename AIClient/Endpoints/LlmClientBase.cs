using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using System.Windows.Media;
using LLMClient.Endpoints.OpenAIAPI;
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

    [JsonIgnore]
    public virtual ImageSource? Icon
    {
        get { return this.Info.Icon; }
    }

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
    public virtual ILLMModel Info
    {
        get { return null; }
    }

    public IModelParams Parameters { get; set; } = new DefaultModelParam();

    [JsonIgnore] public virtual ObservableCollection<string> PreResponse { get; } = new ObservableCollection<string>();

    protected virtual ChatOptions CreateChatOptions(IList<ChatMessage> messages)
    {
        var modelInfo = this.Info;
        var modelParams = this.Parameters;
        if (modelInfo.SystemPromptEnable && !string.IsNullOrWhiteSpace(modelParams.SystemPrompt))
        {
            messages.Add(new ChatMessage(ChatRole.System, modelParams.SystemPrompt));
        }

        var chatOptions = new ChatOptions()
        {
            ModelId = modelInfo.Id,
        };

        if (modelInfo.TopPEnable)
        {
            chatOptions.TopP = modelParams.TopP;
        }

        if (modelInfo.TopKEnable)
        {
            chatOptions.TopK = modelParams.TopK;
        }

        if (modelInfo.TemperatureEnable)
        {
            chatOptions.Temperature = modelParams.Temperature;
        }

        if (modelInfo.MaxTokensEnable)
        {
            chatOptions.MaxOutputTokens = modelParams.MaxTokens;
        }

        if (modelInfo.FrequencyPenaltyEnable)
        {
            chatOptions.FrequencyPenalty = modelParams.FrequencyPenalty;
        }

        if (modelInfo.PresencePenaltyEnable)
        {
            chatOptions.PresencePenalty = modelParams.PresencePenalty;
        }

        if (modelInfo.SeedEnable && modelParams.Seed.HasValue)
        {
            chatOptions.Seed = modelParams.Seed.Value;
        }

        return chatOptions;
    }

    public abstract Task<CompletedResult> SendRequest(IEnumerable<IDialogViewItem> dialogItems,
        CancellationToken cancellationToken = default);
}