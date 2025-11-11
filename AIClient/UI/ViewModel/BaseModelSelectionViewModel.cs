using System.Windows;
using System.Windows.Input;
using LLMClient.Abstraction;
using LLMClient.UI.ViewModel.Base;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Xaml.Behaviors.Core;

namespace LLMClient.UI.ViewModel;

public abstract class BaseModelSelectionViewModel : BaseViewModel
{
    private ILLMChatModel? _selectedModel;

    public ILLMChatModel? SelectedModel
    {
        get => _selectedModel;
        set
        {
            if (Equals(value, _selectedModel)) return;
            _selectedModel = value;
            OnPropertyChanged();
        }
    }

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
}