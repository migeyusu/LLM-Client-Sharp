using System.Windows;
using System.Windows.Input;
using AutoMapper;
using CommunityToolkit.Mvvm.Input;
using LLMClient.Abstraction;
using LLMClient.Component.CustomControl;
using LLMClient.Component.ViewModel.Base;
using LLMClient.Endpoints;
using LLMClient.Endpoints.OpenAIAPI;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.DependencyInjection;

namespace LLMClient.Component.ViewModel;

public abstract class BaseModelSelectionViewModel : BaseViewModel, IParameterizedLLMModel
{
    private IEndpointModel? _selectedModel;
    private bool _showModelParams;

    public IEndpointModel? SelectedModel
    {
        get => _selectedModel;
        set
        {
            if (Equals(value, _selectedModel)) return;
            _selectedModel = value;
            OnPropertyChanged();
            if (value != null)
            {
                LLMClient.Extension.ParamMapper.Map(value, Parameters);
            }
            else
            {
                this.ShowModelParams = false;
            }

            OnPropertyChanged(nameof(Model));
        }
    }

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

    private static ICommand? _createDefaultCommand;

    public static ICommand CreateDefaultCommand
    {
        get
        {
            return _createDefaultCommand ??= new RelayCommand<BaseModelSelectionViewModel>(o =>
            {
                try
                {
                    o?.ApplyModel();
                }
                catch (Exception e)
                {
                    MessageBoxes.Error($"Error Create Client:{e.Message}", "Error");
                }
            });
        }
    }

    public ICommand CreateByModelCommand
    {
        get
        {
            return field ??= new RelayCommand<IEndpointModel>(o =>
            {
                try
                {
                    if (o == null)
                    {
                        return;
                    }

                    this.SelectedModel = o;
                    this.ApplyModel();
                }
                catch (Exception e)
                {
                    MessageBoxes.Error($"Error Create Client:{e.Message}", "Error");
                }
            });
        }
    }


    public ILLMChatClient CreateClient()
    {
        if (SelectedModel == null)
        {
            throw new Exception("Please select model.");
        }

        var chatClient = this.CreateChatClient();
        if (chatClient == null)
        {
            throw new Exception("Create chat client failed.");
        }

        //只有通过这里创建的客户端，才会设置模型历史记录
        ServiceLocator.GetService<IEndpointService>()?.SetModelHistory(this.SelectedModel);
        return chatClient;
    }

    protected abstract void ApplyModel();

    public IEndpointModel Model => SelectedModel ?? EmptyLLMChatModel.Instance;
}