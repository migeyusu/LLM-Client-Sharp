using System.Collections;
using System.Collections.ObjectModel;
using System.Text.Json;
using AutoMapper;
using LLMClient.Abstraction;
using LLMClient.Component.ViewModel;
using LLMClient.Component.ViewModel.Base;
using LLMClient.Configuration;
using LLMClient.ContextEngineering.Analysis;
using LLMClient.ContextEngineering.Tools;

using LLMClient.Dialog;
using LLMClient.Dialog.Models;
using LLMClient.Endpoints;

using LLMClient.Persistence;
using LLMClient.Project;
using LLMClient.ToolCall;
using LLMClient.ToolCall.DefaultPlugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace LLMClient.Test;

public class ProjectPersistenceTests
{
    [Fact]
    public void ResponsePersistence_Deserializes_ContextUsage_WithPreservedReferenceMetadata()
    {
        var sharedUsage = new Microsoft.Extensions.AI.UsageDetails
        {
            InputTokenCount = 120,
            OutputTokenCount = 64,
            TotalTokenCount = 184,
        };

        var persistItem = new RawResponsePersistItem
        {
            Usage = sharedUsage,
            LastSuccessfulUsage = new ContextUsagePO
            {
                MaxContextLength = 4096,
                UsageDetails = sharedUsage,
            },
        };

        var json = JsonSerializer.Serialize(persistItem, FileBasedSessionBase.SerializerOption);
        var restored = JsonSerializer.Deserialize<RawResponsePersistItem>(json, FileBasedSessionBase.SerializerOption);

        Assert.NotNull(restored);
        Assert.NotNull(restored!.LastSuccessfulUsage);
        Assert.NotNull(restored.LastSuccessfulUsage!.UsageDetails);
        Assert.Equal(120, restored.LastSuccessfulUsage.UsageDetails.InputTokenCount);
    }

    [Fact]
    public void ProjectPersistence_RoundTrips_ProjectSessions()
    {
        TestFixture.RunInStaThread(() =>
        {
            var serviceProvider = CreateServiceProvider();
            BaseViewModel.ServiceLocator = serviceProvider;

            var factory = serviceProvider.GetRequiredService<IViewModelFactory>();
            var mapper = serviceProvider.GetRequiredService<IMapper>();
            var rootPath = Path.Combine(Path.GetTempPath(), "LLMClient.ProjectPersistenceTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(rootPath);

            var project = factory.CreateViewModel<CppProjectViewModel>(
                new ProjectOption
                {
                    Name = "Persistence Test",
                    Description = "Verify project session persistence.",
                    RootPath = rootPath,
                    Type = ProjectType.Cpp,
                },
                string.Empty,
                EmptyLlmModelClient.Instance);

            var session = factory.CreateViewModel<ProjectSessionViewModel>(project);
            session.Topic = "Task 1";
            session.Summary = "Session summary";
            project.AddSession(session);

            var persistModel = Assert.IsType<CppProjectPersistModel>(
                mapper.Map<ProjectViewModel, ProjectPersistModel>(project, _ => { }));
            var persistedSession = Assert.Single(Assert.IsAssignableFrom<IEnumerable<ProjectSessionPersistModel>>(persistModel.Sessions!));
            Assert.Equal("Task 1", persistedSession.Name);
            Assert.Equal("Session summary", persistedSession.Summary);

            var clone = Assert.IsType<CppProjectViewModel>(project.Clone());
            var clonedSession = Assert.Single(clone.Session);
            Assert.Same(clone, clonedSession.ParentProject);
            Assert.Equal("Task 1", clonedSession.Topic);
            Assert.Equal("Session summary", clonedSession.Summary);
        });
    }

    [Fact]
    public void ProjectPersistence_RoundTrips_CSharpProjectScopedFunctionGroups()
    {
        TestFixture.RunInStaThread(() =>
        {
            var serviceProvider = CreateServiceProvider();
            BaseViewModel.ServiceLocator = serviceProvider;

            var factory = serviceProvider.GetRequiredService<IViewModelFactory>();
            var mapper = serviceProvider.GetRequiredService<IMapper>();
            var rootPath = Path.Combine(Path.GetTempPath(), "LLMClient.CSharpProjectPersistenceTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(rootPath);

            var project = factory.CreateViewModel<CSharpProjectViewModel>(
                new ProjectOption
                {
                    Name = "CSharp Persistence Test",
                    Description = "Verify C# project function persistence.",
                    RootPath = rootPath,
                    Type = ProjectType.CSharp,
                },
                string.Empty,
                EmptyLlmModelClient.Instance);
            project.SolutionFilePath = Path.Combine(rootPath, "Test.sln");

            var session = factory.CreateViewModel<ProjectSessionViewModel>(project);
            session.Topic = "Task 1";
            session.SelectedFunctionGroups = [CreateProjectScopedTree(project)];
            project.AddSession(session);

            var persistModel = Assert.IsType<CSharpProjectPersistModel>(
                mapper.Map<ProjectViewModel, ProjectPersistModel>(project, _ => { }));
            Assert.Equal(project.SolutionFilePath, persistModel.SolutionFilePath);

            var persistedSession = Assert.Single(persistModel.Sessions!);
            var persistedGroup = Assert.Single(persistedSession.AllowedFunctions!);
            Assert.Null(persistedGroup.FunctionGroup);
            Assert.IsType<ProjectAwarenessPluginPersistModel>(persistedGroup.FunctionGroupPersistModel);

            var clone = Assert.IsType<CSharpProjectViewModel>(
                mapper.Map<ProjectPersistModel, ProjectViewModel>(persistModel, _ => { }));
            Assert.Equal(project.SolutionFilePath, clone.SolutionFilePath);

            var clonedSession = Assert.Single(clone.Session);
            var clonedTree = Assert.Single(clonedSession.SelectedFunctionGroups!);
            Assert.True(clone.TryResolvePersistedFunctionGroup(new ProjectAwarenessPluginPersistModel(),
                out var resolvedFunctionGroup));
            Assert.Same(resolvedFunctionGroup, clonedTree.Data);
            Assert.Equal(typeof(ProjectAwarenessPlugin), clonedTree.Data.GetType());
        });
    }

    [Fact]
    public void RequestPersistence_RoundTrips_FunctionGroups_WithParentProjectContext()
    {
        TestFixture.RunInStaThread(() =>
        {
            var serviceProvider = CreateServiceProvider();
            BaseViewModel.ServiceLocator = serviceProvider;

            var factory = serviceProvider.GetRequiredService<IViewModelFactory>();
            var mapper = serviceProvider.GetRequiredService<IMapper>();
            var rootPath = Path.Combine(Path.GetTempPath(), "LLMClient.RequestPersistenceTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(rootPath);

            var project = factory.CreateViewModel<CSharpProjectViewModel>(
                new ProjectOption
                {
                    Name = "Request Persistence Test",
                    Description = "Verify request function group persistence.",
                    RootPath = rootPath,
                    Type = ProjectType.CSharp,
                },
                string.Empty,
                EmptyLlmModelClient.Instance);
            var session = factory.CreateViewModel<ProjectSessionViewModel>(project);

            var request = new RequestViewItem("hello", session)
            {
                FunctionGroups = [CreateProjectScopedTree(project)],
            };

            var persistModel = mapper.Map<RequestViewItem, RequestPersistItem>(request, _ => { });
            var persistedGroup = Assert.Single(persistModel.FunctionGroups!);
            Assert.IsType<ProjectAwarenessPluginPersistModel>(persistedGroup.FunctionGroupPersistModel);

            var restored = mapper.Map<RequestPersistItem, RequestViewItem>(persistModel, opts =>
            {
                opts.Items[AutoMapModelTypeConverter.ParentDialogViewModelKey] = session;
                opts.Items[AutoMapModelTypeConverter.ParentProjectViewModelKey] = project;
            });

            var restoredGroup = Assert.IsType<CheckableFunctionGroupTree>(Assert.Single(restored.FunctionGroups!));
            Assert.True(project.TryResolvePersistedFunctionGroup(new ProjectAwarenessPluginPersistModel(),
                out var resolvedFunctionGroup));
            Assert.Same(resolvedFunctionGroup, restoredGroup.Data);
        });
    }

    [Fact]
    public void ProjectRequester_FunctionSelector_DoesNotDuplicate_ProjectScopedFunctionGroups_AfterRestore()
    {
        TestFixture.RunInStaThread(() =>
        {
            var serviceProvider = CreateServiceProvider();
            BaseViewModel.ServiceLocator = serviceProvider;

            var factory = serviceProvider.GetRequiredService<IViewModelFactory>();
            var mapper = serviceProvider.GetRequiredService<IMapper>();
            var rootPath = Path.Combine(Path.GetTempPath(), "LLMClient.ProjectRequesterTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(rootPath);

            var project = factory.CreateViewModel<CSharpProjectViewModel>(
                new ProjectOption
                {
                    Name = "Requester Dedup Test",
                    Description = "Verify project requester deduplicates restored tools.",
                    RootPath = rootPath,
                    Type = ProjectType.CSharp,
                },
                string.Empty,
                EmptyLlmModelClient.Instance);
            project.SolutionFilePath = Path.Combine(rootPath, "Test.sln");

            var session = factory.CreateViewModel<ProjectSessionViewModel>(project);
            session.Topic = "Task 1";
            session.SelectedFunctionGroups = [CreateProjectScopedTree(project)];
            project.AddSession(session);

            var persistModel = Assert.IsType<CSharpProjectPersistModel>(
                mapper.Map<ProjectViewModel, ProjectPersistModel>(project, _ => { }));
            var clone = Assert.IsType<CSharpProjectViewModel>(
                mapper.Map<ProjectPersistModel, ProjectViewModel>(persistModel, _ => { }));
            var clonedSession = Assert.Single(clone.Session);
            clone.SelectedSession = clonedSession;

            clone.Requester.FunctionTreeSelector.RefreshSourceAsync().GetAwaiter().GetResult();

            Assert.True(clone.TryResolvePersistedFunctionGroup(new ProjectAwarenessPluginPersistModel(),
                out var resolvedFunctionGroup));
            Assert.NotNull(resolvedFunctionGroup);

            var matchingGroups = clone.Requester.FunctionTreeSelector.FunctionGroups
                .Where(group => AIFunctionGroupComparer.Instance.Equals(group, resolvedFunctionGroup))
                .ToArray();

            var restoredGroup = Assert.Single(matchingGroups);
            Assert.Same(resolvedFunctionGroup, restoredGroup.Data);
            Assert.True(restoredGroup.IsSelected);
        });
    }

    [Fact]
    public void DialogPersistence_LoadsLegacySummaryRequest_AsErasePlusRequest()
    {
        TestFixture.RunInStaThread(() =>
        {
            var serviceProvider = CreateServiceProvider();
            BaseViewModel.ServiceLocator = serviceProvider;

            var mapper = serviceProvider.GetRequiredService<IMapper>();
            var summaryId = Guid.NewGuid();
            var persistModel = new DialogFilePersistModel
            {
                Topic = "Legacy Summary",
                DialogItems =
                [
                    new SummaryRequestPersistItem
                    {
                        Id = summaryId,
                        InteractionId = Guid.NewGuid(),
                        SummaryPrompt = "legacy summary",
                    }
                ],
                CurrentLeaf = new SummaryRequestPersistItem { Id = summaryId },
            };

            var dialog = mapper.Map<DialogFilePersistModel, DialogViewModel>(persistModel, _ => { });

            var erase = Assert.IsType<EraseViewItem>(Assert.Single(dialog.VisualDialogItems.OfType<EraseViewItem>()));
            var request = Assert.IsType<RequestViewItem>(Assert.Single(dialog.VisualDialogItems.OfType<RequestViewItem>()));
            Assert.Equal("legacy summary", request.RawTextMessage);
            Assert.Same(erase, request.PreviousItem);
            Assert.Contains(erase, dialog.VisualDialogItems);
            Assert.Contains(request, dialog.VisualDialogItems);
            Assert.Same(request, dialog.CurrentLeaf);
        });
    }

    [Fact]
    public void SummaryRequestViewItem_MapsTo_PersistItem_Correctly()
    {
        TestFixture.RunInStaThread(() =>
        {
            var serviceProvider = CreateServiceProvider();
            BaseViewModel.ServiceLocator = serviceProvider;
            var mapper = serviceProvider.GetRequiredService<IMapper>();

            var interactionId = Guid.NewGuid();
            var viewItem = new SummaryRequestViewItem("summarize this")
            {
                State = SummaryRequestState.Completed,
                InteractionId = interactionId,
            };

            var persistItem = mapper.Map<SummaryRequestViewItem, SummaryRequestPersistItem>(viewItem);

            Assert.Equal(SummaryRequestState.Completed, persistItem.State);
            Assert.Equal("summarize this", persistItem.SummaryPrompt);
            Assert.Equal(interactionId, persistItem.InteractionId);
        });
    }

    [Fact]
    public void SummaryRequestPersistItem_MapsTo_ViewItem_Correctly()
    {
        TestFixture.RunInStaThread(() =>
        {
            var serviceProvider = CreateServiceProvider();
            BaseViewModel.ServiceLocator = serviceProvider;
            var mapper = serviceProvider.GetRequiredService<IMapper>();
            var options = serviceProvider.GetRequiredService<GlobalOptions>();
            var summarizer = serviceProvider.GetRequiredService<Summarizer>();
            var factory = serviceProvider.GetRequiredService<IViewModelFactory>();

            var dialog = new DialogViewModel("test", "", EmptyLlmModelClient.Instance, mapper, summarizer, options, factory);

            var interactionId = Guid.NewGuid();
            var persistItem = new SummaryRequestPersistItem
            {
                SummaryPrompt = "summarize this",
                State = SummaryRequestState.Failed,
                InteractionId = interactionId,
            };

            var viewItem = mapper.Map<SummaryRequestPersistItem, SummaryRequestViewItem>(persistItem, opts =>
            {
                opts.Items[AutoMapModelTypeConverter.ParentDialogViewModelKey] = dialog;
            });

            Assert.IsType<SummaryRequestViewItem>(viewItem);
            Assert.Equal(SummaryRequestState.Failed, viewItem.State);
            Assert.Equal(interactionId, viewItem.InteractionId);
            var message = Assert.Single(viewItem.Messages);
            Assert.Equal("summarize this", message.Text);
        });
    }

    [Fact]
    public void SummaryRequestPersistItem_RoundTrips_ThroughJson()
    {
        var interactionId = Guid.NewGuid();
        var persistItem = new SummaryRequestPersistItem
        {
            Id = Guid.NewGuid(),
            SummaryPrompt = "summarize this",
            State = SummaryRequestState.Completed,
            InteractionId = interactionId,
            OutputLength = 1024,
        };

        var json = JsonSerializer.Serialize(persistItem, FileBasedSessionBase.SerializerOption);
        var restored = JsonSerializer.Deserialize<SummaryRequestPersistItem>(json, FileBasedSessionBase.SerializerOption);

        Assert.NotNull(restored);
        Assert.Equal(SummaryRequestState.Completed, restored!.State);
        Assert.Equal("summarize this", restored.SummaryPrompt);
        Assert.Equal(interactionId, restored.InteractionId);
        Assert.Equal(1024, restored.OutputLength);
    }

    private static ServiceProvider CreateServiceProvider()
    {
        return new ServiceCollection()
            .AddSingleton<IViewModelFactory, ViewModelFactory>()
            .AddTransient<AutoMapModelTypeConverter>()
            .AddSingleton<Microsoft.Extensions.Logging.ILoggerFactory>(NullLoggerFactory.Instance)
            .AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<>), typeof(NullLogger<>))
            .AddSingleton<GlobalOptions>()
            .AddSingleton<IPromptsResource, TestPromptsResource>()
            .AddSingleton<IEndpointService, TestEndpointService>()
            .AddSingleton<IMcpServiceCollection, TestMcpServiceCollection>()
            .AddSingleton<IRagSourceCollection, TestRagSourceCollection>()
            .AddSingleton<BuiltInFunctionsCollection>()
            .AddSingleton<ITokensCounter, DefaultTokensCounter>()
            .AddTransient<AnalyzerConfig>()
            .AddTransient<RoslynProjectAnalyzer>()
            .AddSingleton<Summarizer>()
            .AddSingleton<Profile, FunctionGroupPersistenceProfile>()
            .AddSingleton<Profile, DialogItemPersistenceProfile>()
            .AddSingleton<Profile, DialogMappingProfile>()
            .AddSingleton<Profile, ResponseProfile>()
            .AddSingleton<Profile, SessionProjectPersistenceProfile>()
            .AddMap()
            .BuildServiceProvider();
    }

    private static CheckableFunctionGroupTree CreateProjectScopedTree(CSharpProjectViewModel project)
    {
        Assert.True(project.TryResolvePersistedFunctionGroup(new ProjectAwarenessPluginPersistModel(),
            out var functionGroup));
        Assert.NotNull(functionGroup);

        var tree = new CheckableFunctionGroupTree(functionGroup);
        var firstFunction = Assert.Single(functionGroup.AvailableTools!.Take(1));
        tree.Functions.Add(new VirtualFunctionViewModel(firstFunction, tree) { IsSelected = true });
        tree.IsSelected = true;
        return tree;
    }

    private sealed class TestPromptsResource : IPromptsResource
    {
        public IReadOnlyList<PromptEntry> SystemPrompts => Array.Empty<PromptEntry>();

        public Task Initialize()
        {
            return Task.CompletedTask;
        }
    }

    private sealed class TestEndpointService : IEndpointService
    {
        public IReadOnlyList<ILLMAPIEndpoint> AllEndpoints => Array.Empty<ILLMAPIEndpoint>();

        public IReadOnlyList<ILLMAPIEndpoint> CandidateEndpoints => Array.Empty<ILLMAPIEndpoint>();

        public IReadOnlyList<IEndpointModel> HistoryModels => Array.Empty<IEndpointModel>();

        public IReadOnlyList<IEndpointModel> SuggestedModels => Array.Empty<IEndpointModel>();

        public void SetModelHistory(IEndpointModel model)
        {
        }

        public Task Initialize()
        {
            return Task.CompletedTask;
        }

        public Task SaveActivities()
        {
            return Task.CompletedTask;
        }
    }

    private sealed class TestMcpServiceCollection : IMcpServiceCollection
    {
        public bool IsInitialized => true;

        public bool IsLoaded => true;

        public Task InitializeToolsAsync()
        {
            return Task.CompletedTask;
        }

        public Task LoadAsync()
        {
            return Task.CompletedTask;
        }

        public Task EnsureAsync()
        {
            return Task.CompletedTask;
        }

        public IAIFunctionGroup TryGet(IAIFunctionGroup functionGroup)
        {
            return functionGroup;
        }

        public IEnumerator<IAIFunctionGroup> GetEnumerator()
        {
            return Enumerable.Empty<IAIFunctionGroup>().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    private sealed class TestRagSourceCollection : IRagSourceCollection
    {
        public ObservableCollection<IRagSource> Sources { get; } = [];

        public bool IsRunning => false;

        public Task LoadAsync()
        {
            return Task.CompletedTask;
        }
    }
}

