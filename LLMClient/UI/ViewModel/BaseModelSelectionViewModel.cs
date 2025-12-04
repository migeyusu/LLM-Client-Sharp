using System.Windows;
using System.Windows.Input;
using AutoMapper;
using LLMClient.Abstraction;
using LLMClient.Endpoints;
using LLMClient.Endpoints.OpenAIAPI;
using LLMClient.UI.ViewModel.Base;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Xaml.Behaviors.Core;

namespace LLMClient.UI.ViewModel;

public abstract class BaseModelSelectionViewModel : BaseViewModel, ILLMChatClient
{
    private ILLMChatModel? _selectedModel;
    private bool _showModelParams;

    public ILLMChatModel? SelectedModel
    {
        get => _selectedModel;
        set
        {
            if (Equals(value, _selectedModel)) return;
            _selectedModel = value;
            OnPropertyChanged();
            if (value != null)
            {
                Mapper.Map(value, Parameters);
            }
            else
            {
                this.ShowModelParams = false;
            }

            OnPropertyChanged(nameof(Model));
        }
    }

    private IMapper Mapper => MapperLazy.Value;

    private Lazy<IMapper> MapperLazy => new(() => ServiceLocator.GetService<IMapper>()!);

    public bool ShowModelParams
    {
        get => _showModelParams;
        set
        {
            if (value == _showModelParams) return;
            _showModelParams = value;
            OnPropertyChanged();
        }
    }

    public IModelParams Parameters { get; set; } = new DefaultModelParam();

    public ILLMChatClient? GetClient()
    {
        return this.SelectedModel?.CreateChatClient();
    }

    public ICommand CreateDefaultCommand => new ActionCommand(o =>
    {
        if (SelectedModel == null)
        {
            MessageBox.Show("Please select model.");
            return;
        }

        var chatClient = this.SelectedModel.CreateChatClient();
        if (chatClient == null)
        {
            MessageBox.Show("Create chat client failed.");
            return;
        }

        Mapper.Map(Parameters, chatClient.Parameters);
        ServiceLocator.GetService<IEndpointService>()?.AddModelFrequency(this.SelectedModel);
        SubmitClient(chatClient);
    });

    public ICommand CreateResearchCommand => new ActionCommand(o =>
    {
        if (SelectedModel == null)
        {
            MessageBox.Show("Please select model.");
            return;
        }

        var chatClient = this.SelectedModel.CreateChatClient();
        if (chatClient == null)
        {
            MessageBox.Show("Create chat client failed.");
            return;
        }
        Mapper.Map(Parameters, chatClient.Parameters);
        if (o is string researchName)
        {
            var researchClient = ServiceLocator.GetService<IResearchModelService>()
                ?.CreateResearchClient(researchName, chatClient);
            if (researchClient != null) SubmitClient(researchClient);
            /*var client = modelSelection.SelectedModel?.CreateChatClient();
            if (client == null)
            {
                throw new InvalidOperationException("Selected model is null or cannot create client.");
            }*/
        }
    });

    protected abstract void SubmitClient(ILLMChatClient client);
    public string Name { get; } = "Fake Client";
    public ILLMEndpoint Endpoint { get; } = EmptyLLMEndpoint.Instance;

    public ILLMChatModel Model => SelectedModel ?? EmptyLLMChatModel.Instance;

    public bool IsResponding { get; set; } = false;

    public Task<CompletedResult> SendRequest(DialogContext context, IInvokeInteractor? interactor = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }
}