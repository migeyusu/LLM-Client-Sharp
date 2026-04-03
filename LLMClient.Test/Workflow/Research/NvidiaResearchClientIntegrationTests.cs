using LLMClient.Abstraction;
using LLMClient.Agent;
using LLMClient.Configuration;
using LLMClient.Dialog.Models;
using LLMClient.Workflow.Research;
using Moq;
using Xunit;

namespace LLMClient.Test.Workflow.Research;

/// <summary>
/// Integration tests for NvidiaResearchClient
/// These tests focus on realistic usage scenarios
/// Note: Some tests are skipped due to ITextSearch API compatibility issues
/// </summary>
public class NvidiaResearchClientIntegrationTests
{
    private readonly Mock<IParameterizedLLMModel> _mockPromptModel;
    private readonly Mock<IParameterizedLLMModel> _mockReportModel;
    private readonly Mock<IEndpointModel> _mockEndpointModel;
    private readonly Mock<ILLMAPIEndpoint> _mockEndpoint;
    private readonly Mock<ILLMChatClient> _mockChatClient;
    private readonly GlobalOptions _globalOptions;

    public NvidiaResearchClientIntegrationTests()
    {
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

        // Setup GlobalOptions without text search (will test basic functionality)
        _globalOptions = new GlobalOptions();
    }

    [Fact(Skip = "Integration test - requires actual LLM and search service")]
    public async Task Execute_WithValidInput_ProducesResearchReport()
    {
        // This test would require:
        // 1. Actual LLM endpoint
        // 2. Actual text search service
        // 3. Real network connectivity
        // Marked as Skip for unit test suite
    }

    [Fact]
    public void Client_AgentInterface_IsProperlyImplemented()
    {
        // Arrange
        var client = new NvidiaResearchClient(
            _mockPromptModel.Object,
            _mockReportModel.Object,
            _globalOptions);

        // Act & Assert
        Assert.IsAssignableFrom<IAgent>(client);
        Assert.Equal("Nvidia Deep Research", client.Name);
    }

    [Fact]
    public void MaxTopics_CanBeConfigured()
    {
        // Arrange & Act
        var client = new NvidiaResearchClient(
            _mockPromptModel.Object,
            _mockReportModel.Object,
            _globalOptions)
        {
            MaxTopics = 10
        };

        // Assert
        Assert.Equal(10, client.MaxTopics);
    }

    [Fact]
    public void MaxSearchPhrases_CanBeConfigured()
    {
        // Arrange & Act
        var client = new NvidiaResearchClient(
            _mockPromptModel.Object,
            _mockReportModel.Object,
            _globalOptions)
        {
            MaxSearchPhrases = 5
        };

        // Assert
        Assert.Equal(5, client.MaxSearchPhrases);
    }

    [Fact]
    public void ResearchClient_BaseClass_ImplementsIAgent()
    {
        // Arrange
        var client = new NvidiaResearchClient(
            _mockPromptModel.Object,
            _mockReportModel.Object,
            _globalOptions);

        // Act
        IAgent agent = client;

        // Assert
        Assert.NotNull(agent);
        Assert.Equal(client.Name, agent.Name);
    }
}
