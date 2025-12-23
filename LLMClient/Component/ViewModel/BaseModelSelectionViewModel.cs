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

public abstract class BaseModelSelectionViewModel : BaseViewModel, IParameterizedLLMModel
{
    private ILLMModel? _selectedModel;
    private bool _showModelParams;

    public ILLMModel? SelectedModel
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

        var chatClient = this.CreateChatClient(Mapper);
        if (chatClient == null)
        {
            throw new Exception("Create chat client failed.");
        }

        ServiceLocator.GetService<IEndpointService>()?.SetModelHistory(this.SelectedModel);
        return chatClient;
    }

    protected abstract void SubmitClient(ILLMChatClient client);
    
    public string Name { get; } = "Fake Client";

    public ILLMModel Model => SelectedModel ?? EmptyLLMChatModel.Instance;
}