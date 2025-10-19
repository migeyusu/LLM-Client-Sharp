using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.Json.Serialization;
using System.Windows.Input;
using AutoMapper;
using LLMClient.Abstraction;
using LLMClient.Data;
using LLMClient.Dialog;
using LLMClient.UI;
using LLMClient.UI.Component;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using Microsoft.Xaml.Behaviors.Core;

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

    /*public ILLMChatClient? EmbeddingClient => string.IsNullOrEmpty(EmbeddingEndpointName)
        ? null
        : EmbeddingEndpoint?.NewClient(EmbeddingEndpointName);*/

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
    public ILLMChatClient? DigestClient
    {
        get
        {
            if (this.DigestClientPersist == null)
            {
                return null;
            }

            return ServiceLocator.GetService<IMapper>()?
                .Map<LLMClientPersistModel, ILLMChatClient>(this.DigestClientPersist, (_) => { });
        }
        private set
        {
            if (value == null)
            {
                this.DigestClientPersist = null;
                return;
            }

            this.DigestClientPersist = ServiceLocator.GetService<IMapper>()?
                .Map<ILLMChatClient, LLMClientPersistModel>(value, (_ => { }));
        }
    }

    public int MaxDigestParallelism
    {
        get => _maxDigestParallelism;
        set
        {
            if (value == _maxDigestParallelism) return;
            _maxDigestParallelism = value;
            OnPropertyChanged();
        }
    }

    [JsonIgnore] public IEndpointService? EndpointService => ServiceLocator.GetService<IEndpointService>();

    [JsonIgnore]
    public ModelSelectionViewModel SelectionViewModel =>
        new ModelSelectionPopupViewModel(client => { this.DigestClient = client; })
            { SuccessRoutedCommand = PopupBox.ClosePopupCommand };


    private string? _dbConnection = "Data Source=file_embedding.db";
    private int _maxDigestParallelism = 10;

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

    public ICommand SelectDatabaseCommand => new ActionCommand(_ =>
    {
        var openFileDialog = new OpenFileDialog()
        {
            Filter = "SQLite Database Files (*.db;*.sqlite)|*.db;*.sqlite",
            CheckFileExists = true,
            CheckPathExists = true,
        };
        if (openFileDialog.ShowDialog() == true)
        {
            var fileName = openFileDialog.FileName;
            if (!string.IsNullOrEmpty(this.DBConnection))
            {
                var dbPath = Path.GetFullPath(this.DBConnection
                    .Replace("Data Source=", string.Empty).Trim());
                if (fileName.Equals(dbPath, StringComparison.OrdinalIgnoreCase))
                {
                    MessageEventBus.Publish("已选择相同的数据库文件，无需更改。");
                    return; // No change needed if the same file is selected
                }
            }

            this.DBConnection = "Data Source=" + fileName;
        }
    });

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