using System.Text.Json.Serialization;
using AutoMapper;
using LLMClient.Abstraction;
using LLMClient.Data;
using LLMClient.Dialog;
using LLMClient.UI;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.DependencyInjection;

namespace LLMClient.Rag.Document;

public class RagOption : BaseViewModel
{
    [JsonIgnore]
    public ILLMEndpoint? EmbeddingEndpoint
    {
        get => _embeddingEndpoint;
        set
        {
            if (value == _embeddingEndpoint) return;
            _embeddingEndpoint = value;
            OnPropertyChanged();
        }
    }

    public string? EmbeddingEndpointName
    {
        get { return this.EmbeddingEndpoint?.Name; }
        set
        {
            if (value == null)
            {
                this.EmbeddingEndpoint = null;
                return;
            }

            if (value == _embeddingEndpoint?.Name) return;
            var endpointService = ServiceLocator.GetService<IEndpointService>();
            if (endpointService == null)
            {
                return;
            }

            var endpoint = endpointService.GetEndpoint(value);
            this.EmbeddingEndpoint = endpoint ?? null;
        }
    }

    public string? EmbeddingModelId
    {
        get => _embeddingModelId;
        set
        {
            if (value == _embeddingModelId) return;
            _embeddingModelId = value;
            OnPropertyChanged();
        }
    }

    public LLMClientPersistModel? SummaryModel
    {
        get => _summaryModel;
        set
        {
            if (Equals(value, _summaryModel)) return;
            _summaryModel = value;
            OnPropertyChanged();
        }
    }

    [JsonIgnore] public IEndpointService? EndpointService => ServiceLocator.GetService<IEndpointService>();


    [JsonIgnore]
    public ModelSelectionViewModel SelectionViewModel =>
        new ModelSelectionPopupViewModel((model =>
        {
            var llmClient = model.GetClient();
            if (llmClient == null)
            {
                return;
            }

            this.SummaryModel = ServiceLocator.GetService<IMapper>()?
                .Map<ILLMClient, LLMClientPersistModel>(llmClient, (options => { }));
        })) { SuccessRoutedCommand = PopupBox.ClosePopupCommand };


    private string? _dbConnection;
    private LLMClientPersistModel? _summaryModel;
    private string? _embeddingModelId;
    private ILLMEndpoint? _embeddingEndpoint;

    public string? DBConnection
    {
        get => _dbConnection;
        set
        {
            if (value == _dbConnection) return;
            _dbConnection = value;
            OnPropertyChanged();
        }
    }
}