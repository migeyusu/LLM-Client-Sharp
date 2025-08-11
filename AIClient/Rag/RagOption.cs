using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using AutoMapper;
using LLMClient.Abstraction;
using LLMClient.Data;
using LLMClient.Dialog;
using LLMClient.UI;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.DependencyInjection;

namespace LLMClient.Rag;

public class RagOption : BaseViewModel
{
    [JsonIgnore]
    public ILLMEndpoint? EmbeddingEndpoint
    {
        get
        {
            if (string.IsNullOrEmpty(EmbeddingEndpointName))
            {
                return null;
            }

            var endpointService = ServiceLocator.GetService<IEndpointService>();
            if (endpointService == null)
            {
                return null;
            }

            return endpointService.GetEndpoint(EmbeddingEndpointName);
        }
        set => EmbeddingEndpointName = value?.Name;
    }

    public ILLMClient? EmbeddingClient => string.IsNullOrEmpty(EmbeddingEndpointName)
        ? null
        : EmbeddingEndpoint?.NewClient(EmbeddingEndpointName);

    public string? EmbeddingEndpointName { get; set; }

    private string? _embeddingModelId;

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

    private LLMClientPersistModel? _digestClientPersist;

    public LLMClientPersistModel? DigestClientPersist
    {
        get => _digestClientPersist;
        set
        {
            if (Equals(value, _digestClientPersist)) return;
            _digestClientPersist = value;
            OnPropertyChanged();
        }
    }

    [JsonIgnore]
    public ILLMClient? DigestClient
    {
        get
        {
            if (this.DigestClientPersist == null)
            {
                return null;
            }

            return ServiceLocator.GetService<IMapper>()?
                .Map<LLMClientPersistModel, ILLMClient>(this.DigestClientPersist, (o) => { });
        }
        set
        {
            if (value == null)
            {
                this.DigestClientPersist = null;
                return;
            }

            this.DigestClientPersist = ServiceLocator.GetService<IMapper>()?
                .Map<ILLMClient, LLMClientPersistModel>(value, (options => { }));
        }
    }

    [JsonIgnore] public IEndpointService? EndpointService => ServiceLocator.GetService<IEndpointService>();

    [JsonIgnore]
    public ModelSelectionViewModel SelectionViewModel =>
        new ModelSelectionPopupViewModel((model => { this.DigestClient = model.GetClient(); }))
            { SuccessRoutedCommand = PopupBox.ClosePopupCommand };


    private string? _dbConnection;

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

    public SemanticKernelStore.SearchAlgorithm SearchAlgorithm { get; set; }

    [MemberNotNull(nameof(DBConnection))]
    [MemberNotNull(nameof(EmbeddingEndpoint))]
    [MemberNotNull(nameof(EmbeddingModelId))]
    [MemberNotNull(nameof(DigestClientPersist))]
    public void ThrowIfNotValid()
    {
        if (string.IsNullOrEmpty(DBConnection))
        {
            throw new InvalidOperationException("DBConnection is not set.");
        }

        if (EmbeddingEndpoint == null)
        {
            throw new InvalidOperationException("EmbeddingEndpoint is not set.");
        }

        if (string.IsNullOrEmpty(EmbeddingModelId))
        {
            throw new InvalidOperationException("EmbeddingModelId is not set.");
        }

        if (DigestClientPersist == null)
        {
            throw new InvalidOperationException("SummaryModel is not set.");
        }
    }
}