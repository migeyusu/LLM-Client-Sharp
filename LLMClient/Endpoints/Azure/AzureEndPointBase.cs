using LLMClient.Abstraction;
using LLMClient.Component.CustomControl;
using LLMClient.Component.ViewModel.Base;
using LLMClient.Data;

namespace LLMClient.Endpoints.Azure;

public abstract class AzureEndPointBase : BaseViewModel, ILLMAPIEndpoint
{
    private AzureOption _option = new AzureOption();

    public AzureOption Option
    {
        get => _option;
        protected set
        {
            if (Equals(value, _option)) return;
            _option = value;
            OnPropertyChanged();
        }
    }

    public virtual string DisplayName
    {
        get { return Name; }
    }

    public abstract bool IsInbuilt { get; }

    public bool IsEnabled { get; } = true;

    public abstract string Name { get; }

    private static readonly Lazy<ThemedIcon> Source = new((() => { return ModelIconType.Azure.GetIcon(); }));

    public virtual ThemedIcon Icon
    {
        get { return Source.Value; }
    }

    public abstract IReadOnlyCollection<IEndpointModel> AvailableModels { get; }

    public abstract ILLMChatClient? NewChatClient(IEndpointModel model);

    public abstract IEndpointModel? GetModel(string modelName);

    public abstract Task InitializeAsync();
}