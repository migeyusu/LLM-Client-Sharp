using System.Windows;
using System.Windows.Input;
using AutoMapper;
using LLMClient.Abstraction;
using LLMClient.Component.ViewModel.Base;
using LLMClient.Endpoints;
using LLMClient.Endpoints.OpenAIAPI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Xaml.Behaviors.Core;

namespace LLMClient.Component.ViewModel;

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

    public ICommand CreateDefaultCommand => new ActionCommand(o =>
    {
        try
        {
            var chatClient = CreateClient();
            SubmitClient(chatClient);
        }
        catch (Exception e)
        {
            MessageBox.Show(e.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    });

    public ILLMChatClient CreateClient()
    {
        if (SelectedModel == null)
        {
            throw new Exception("Please select model.");
        }

        var chatClient = this.SelectedModel.CreateChatClient();
        if (chatClient == null)
        {
            throw new Exception("Create chat client failed.");
        }

        Mapper.Map(Parameters, chatClient.Parameters);
        ServiceLocator.GetService<IEndpointService>()?.AddModelFrequency(this.SelectedModel);
        return chatClient;
    }

    protected abstract void SubmitClient(ILLMChatClient client);
    public string Name { get; } = "Fake Client";
    public ILLMAPIEndpoint Endpoint { get; } = EmptyLLMEndpoint.Instance;

    public ILLMChatModel Model => SelectedModel ?? EmptyLLMChatModel.Instance;

    public bool IsResponding { get; set; } = false;

    public Task<CompletedResult> SendRequest(DialogContext context, IInvokeInteractor? interactor = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }
}