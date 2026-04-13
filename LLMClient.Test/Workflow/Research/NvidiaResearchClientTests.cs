using System.Runtime.CompilerServices;
using AutoMapper;
using LLMClient.Abstraction;
using LLMClient.Agent;
using LLMClient.Agent.Research;
using LLMClient.Component.ViewModel.Base;
using LLMClient.Configuration;

using LLMClient.Dialog;
using LLMClient.Dialog.Models;
using LLMClient.Endpoints;
using LLMClient.Endpoints.OpenAIAPI;

using LLMClient.Persistence;
using LLMClient.ToolCall.DefaultPlugins;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
using LLM_DataSerializeContext = LLMClient.Persistence.LLM_DataSerializeContext;

namespace LLMClient.Test.Workflow.Research;

public class NvidiaResearchClientTests
{
    private readonly Mock<IParameterizedLLMModel> _mockPromptModel;
    private readonly Mock<IParameterizedLLMModel> _mockReportModel;
    private readonly Mock<IEndpointModel> _mockEndpointModel;
    private readonly Mock<ILLMAPIEndpoint> _mockEndpoint;
    private readonly Mock<ILLMChatClient> _mockChatClient;
    private readonly GlobalOptions _globalOptions;
    private readonly IServiceProvider _serviceProvider;

    public NvidiaResearchClientTests()
    {
        // Setup mocks
        _mockPromptModel = new Mock<IParameterizedLLMModel>();
        _mockReportModel = new Mock<IParameterizedLLMModel>();
        _mockEndpointModel = new Mock<IEndpointModel>();
        _mockEndpoint = new Mock<ILLMAPIEndpoint>();
        _mockChatClient = new Mock<ILLMChatClient>();

        // Setup endpoint model
        _mockEndpointModel.SetupGet(m => m.Endpoint).Returns(_mockEndpoint.Object);
        _mockEndpoint.SetupGet(e => e.IsEnabled).Returns(true);
        _mockEndpoint.Setup(e => e.NewChatClient(It.IsAny<IEndpointModel>()))
            .Returns(_mockChatClient.Object);

        // Setup parameterized models
        _mockPromptModel.SetupGet(m => m.Model).Returns(_mockEndpointModel.Object);
        _mockReportModel.SetupGet(m => m.Model).Returns(_mockEndpointModel.Object);

        // Setup GlobalOptions without text search (for basic tests)
        _globalOptions = new GlobalOptions();

        // Setup DI for AutoMapper
        var services = new ServiceCollection()
            .AddTransient<AutoMapModelTypeConverter>()
            .AddSingleton<ITokensCounter, DefaultTokensCounter>()
            .AddMap()
            .AddSingleton(_globalOptions)
            .BuildServiceProvider();
        BaseViewModel.ServiceLocator = services;
        _serviceProvider = services;
    }

    [Fact]
    public void Constructor_WithValidParameters_InitializesCorrectly()
    {
        // Arrange & Act
        var client = new NvidiaResearchClient(
            _mockPromptModel.Object,
            _mockReportModel.Object,
            _globalOptions);

        // Assert
        Assert.Equal("Nvidia Deep Research", client.Name);
        Assert.Equal(5, client.MaxTopics);
        Assert.Equal(3, client.MaxSearchPhrases);
        Assert.Same(_mockPromptModel.Object, client.PromptModel);
        Assert.Same(_mockReportModel.Object, client.ReportModel);
    }

    [Fact]
    public void Constructor_WithNullPromptModel_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new NvidiaResearchClient(null!, _mockReportModel.Object, _globalOptions));
    }

    [Fact]
    public void AutoMapper_ForwardMapping_PreservesAllProperties()
    {
        // Arrange
        var client = new NvidiaResearchClient(
            _mockPromptModel.Object,
            _mockReportModel.Object,
            _globalOptions)
        {
            MaxTopics = 10,
            MaxSearchPhrases = 5
        };

        var mapper = _serviceProvider.GetRequiredService<IMapper>();

        // Act
        var persistModel = mapper.Map<NvidiaResearchClientPersistModel>(client);

        // Assert
        Assert.Equal(10, persistModel.MaxTopics);
        Assert.Equal(5, persistModel.MaxSearchPhrases);
        Assert.NotNull(persistModel.PromptModel);
        Assert.NotNull(persistModel.ReportModel);
    }

    [Fact]
    public void AutoMapper_BackwardMapping_RestoresClientCorrectly()
    {
        // Arrange
        var persistModel = new NvidiaResearchClientPersistModel
        {
            MaxTopics = 8,
            MaxSearchPhrases = 4,
            PromptModel = new ParameterizedLLMModelPO
            {
                ModelName = "test-prompt-model",
                Parameters = new DefaultModelParam()
            },
            ReportModel = new ParameterizedLLMModelPO
            {
                ModelName = "test-report-model",
                Parameters = new DefaultModelParam()
            }
        };

        var mapper = _serviceProvider.GetRequiredService<IMapper>();

        // Act
        var client = mapper.Map<NvidiaResearchClient>(persistModel);

        // Assert
        Assert.Equal("Nvidia Deep Research", client.Name);
        Assert.Equal(8, client.MaxTopics);
        Assert.Equal(4, client.MaxSearchPhrases);
        Assert.NotNull(client.PromptModel);
        Assert.NotNull(client.ReportModel);
    }

    [Fact]
    public void AutoMapper_WithNameAttribute_PreservesAgentType()
    {
        // Arrange
        var mapper = _serviceProvider.GetRequiredService<IMapper>();
        var client = new NvidiaResearchClient(
            _mockPromptModel.Object,
            _mockReportModel.Object,
            _globalOptions);

        // Act
        var persistModel = mapper.Map<AgentPersistModel>(client);

        // Assert
        Assert.IsType<NvidiaResearchClientPersistModel>(persistModel);
        var nvPersistModel = Assert.IsType<NvidiaResearchClientPersistModel>(persistModel);
        Assert.Equal(5, nvPersistModel.MaxTopics);
    }

    [Fact]
    public void SerializeToJson_AndDeserialize_RetainsAllProperties()
    {
        // Arrange
        var client = new NvidiaResearchClient(
            _mockPromptModel.Object,
            _mockReportModel.Object,
            _globalOptions)
        {
            MaxTopics = 7,
            MaxSearchPhrases = 4
        };

        var mapper = _serviceProvider.GetRequiredService<IMapper>();
        var persistModel = mapper.Map<NvidiaResearchClientPersistModel>(client);

        // Act - Serialize
        var json = System.Text.Json.JsonSerializer.Serialize(persistModel, LLM_DataSerializeContext.Default.NvidiaResearchClientPersistModel);

        // Act - Deserialize
        var deserializedModel = System.Text.Json.JsonSerializer.Deserialize<NvidiaResearchClientPersistModel>(
            json, LLM_DataSerializeContext.Default.NvidiaResearchClientPersistModel);

        // Assert
        Assert.NotNull(deserializedModel);
        Assert.Equal(7, deserializedModel.MaxTopics);
        Assert.Equal(4, deserializedModel.MaxSearchPhrases);
    }

    [Fact]
    public void Properties_CanBeModifiedAfterConstruction()
    {
        // Arrange
        var client = new NvidiaResearchClient(
            _mockPromptModel.Object,
            _mockReportModel.Object,
            _globalOptions);

        // Act
        client.MaxTopics = 15;
        client.MaxSearchPhrases = 8;

        // Assert
        Assert.Equal(15, client.MaxTopics);
        Assert.Equal(8, client.MaxSearchPhrases);
    }

    private sealed class TestTextDialogSession : ITextDialogSession
    {
        private readonly List<IChatHistoryItem> _history;

        public TestTextDialogSession(IRequestItem request)
        {
            _history = [request];
            DialogItems = [];
        }

        public IReadOnlyList<IDialogItem> DialogItems { get; }

        public List<IChatHistoryItem> GetHistory()
        {
            return _history;
        }

        public Task CutContextAsync(IRequestItem? requestItem = null)
        {
            return Task.CompletedTask;
        }

        public string? SystemPrompt => null;
    }
}
