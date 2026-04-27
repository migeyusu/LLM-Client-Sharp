using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Input;
using AutoMapper;
using LLMClient.Abstraction;
using LLMClient.Component.CustomControl;
using LLMClient.Component.UserControls;
using LLMClient.Component.Utility;
using LLMClient.Component.ViewModel.Base;
using LLMClient.Dialog;
using LLMClient.Dialog.Models;
using LLMClient.Persistence;
using LLMClient.Rag;
using LLMClient.ToolCall.DefaultPlugins;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel.Data;
using Microsoft.Xaml.Behaviors.Core;

namespace LLMClient.Configuration;

//do not separate persistence and view model, because global option is simple enough.
public class GlobalOptions : NotifyDataErrorInfoViewModelBase
{
    public GlobalOptions()
    {
        ContextSummaryPopupSelectViewModel = new ModelSelectionPopupViewModel(this.ApplyContextSummarizeClient)
            { SuccessRoutedCommand = PopupBox.ClosePopupCommand };
        SubjectSummaryPopupViewModel =
            new ModelSelectionPopupViewModel(this.ApplySubjectSummarizeClient)
                { SuccessRoutedCommand = PopupBox.ClosePopupCommand };
        TextFormatterPopupViewModel =
            new ModelSelectionPopupViewModel(this.ApplyTextFormatterClient)
                { SuccessRoutedCommand = PopupBox.ClosePopupCommand };
    }

    public static string DefaultConfigFile
    {
        get
        {
            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            return Path.GetFullPath(DEFAULT_GLOBAL_CONFIG_FILE, baseDirectory);
        }
    }

    public const string DEFAULT_GLOBAL_CONFIG_FILE = "globalconfig.json";

    public bool IsFileEncryptReading
    {
        get => FileSystemPlugin.UseEncryptedReading;
        set
        {
            FileSystemPlugin.UseEncryptedReading = value;
            OnPropertyChanged();
        }
    }

    private static IMapper Mapper => _mapperLazy.Value;

    private static Lazy<IMapper> _mapperLazy = new(() => ServiceLocator.GetService<IMapper>()!);

    #region context summarize

    private const string DefaultContextSummarizePrompt =
        "Provide a concise and complete summarization of the entire dialog that does not exceed {0} words. \n\nThis summary must always:\n- Consider both user and assistant interactions\n- Maintain continuity for the purpose of further dialog\n- Include details from any existing summary\n- Focus on the most significant aspects of the dialog\n\nThis summary must never:\n- Critique, correct, interpret, presume, or assume\n- Identify faults, mistakes, misunderstanding, or correctness\n- Analyze what has not occurred\n- Exclude details from any existing summary";

    [JsonIgnore] public ModelSelectionPopupViewModel ContextSummaryPopupSelectViewModel { get; }

    [JsonPropertyName("TokenSummarizePrompt")]
    public string ContextSummarizePromptString { get; set; } = DefaultContextSummarizePrompt;

    [JsonIgnore]
    public string ContextSummarizePrompt
    {
        get { return string.Format(ContextSummarizePromptString, ContextSummarizeWordsCount); }
    }

    [JsonPropertyName("SummarizeWordsCount")]
    public int ContextSummarizeWordsCount
    {
        get;
        set
        {
            this.ClearError();
            if (value == field) return;
            if (value < 100)
            {
                this.AddError("Summarize words count must be greater than 100.");
            }

            field = value;
            OnPropertyChanged();
        }
    } = 1000;

    public ILLMChatClient? CreateContextSummarizeClient()
    {
        if (ContextSummarizeClientPersist == null)
        {
            return null;
        }

        return Mapper?
            .Map<ParameterizedLLMModelPO, ILLMChatClient>(ContextSummarizeClientPersist, (options => { }));
    }

    public void ApplyContextSummarizeClient(IParameterizedLLMModel? value)
    {
        if (value == null)
        {
            ContextSummarizeClientPersist = null;
            return;
        }

        ContextSummarizeClientPersist = Mapper?
            .Map<IParameterizedLLMModel, ParameterizedLLMModelPO>(value, (options => { }));
    }

    [JsonPropertyName("SummarizeModelPersistModel")]
    public ParameterizedLLMModelPO? ContextSummarizeClientPersist
    {
        get;
        set
        {
            if (Equals(value, field)) return;
            field = value;
            OnPropertyChanged();
        }
    }

    #endregion

    #region conversation history summary

    private const string DefaultConversationHistorySummaryPrompt
        = @"Your task is to create a comprehensive, detailed summary of the entire conversation that captures all essential information needed to seamlessly continue the work without any loss of context. This summary will be used to compact the conversation while preserving critical technical details, decisions, and progress.

## Recent Context Analysis

Pay special attention to the most recent agent commands and tool executions that led to this summarization being triggered. Include:
- **Last Agent Commands**: What specific actions/tools were just executed
- **Tool Results**: Key outcomes from recent tool calls (truncate if very long, but preserve essential information)
- **Immediate State**: What was the system doing right before summarization
- **Triggering Context**: What caused the token budget to be exceeded

## Analysis Process

Before providing your final summary, wrap your analysis in `<analysis>` tags to organize your thoughts systematically:

1. **Chronological Review**: Go through the conversation chronologically, identifying key phases and transitions
2. **Intent Mapping**: Extract all explicit and implicit user requests, goals, and expectations
3. **Technical Inventory**: Catalog all technical concepts, tools, frameworks, and architectural decisions
4. **Code Archaeology**: Document all files, functions, and code patterns that were discussed or modified
5. **Progress Assessment**: Evaluate what has been completed vs. what remains pending
6. **Context Validation**: Ensure all critical information for continuation is captured
7. **Recent Commands Analysis**: Document the specific agent commands and tool results from the most recent operations

## Summary Structure

Your summary must include these sections in order, following the exact format below:

<analysis>
[Chronological Review: Walk through conversation phases: initial request -> exploration -> implementation -> debugging -> current state]
[Intent Mapping: List each explicit user request with message context]
[Technical Inventory: Catalog all technologies, patterns, and decisions mentioned]
[Code Archaeology: Document every file, function, and code change discussed]
[Progress Assessment: What's done vs. pending with specific status]
[Context Validation: Verify all continuation context is captured]
[Recent Commands Analysis: Last agent commands executed, tool results (truncated if long), immediate pre-summarization state]
</analysis>

<summary>
1. Conversation Overview:
- Primary Objectives: [All explicit user requests and overarching goals with exact quotes]
- Session Context: [High-level narrative of conversation flow and key phases]
- User Intent Evolution: [How user's needs or direction changed throughout conversation]

2. Technical Foundation:
- [Core Technology 1]: [Version/details and purpose]
- [Framework/Library 2]: [Configuration and usage context]
- [Architectural Pattern 3]: [Implementation approach and reasoning]
- [Environment Detail 4]: [Setup specifics and constraints]

3. Codebase Status:
- [File Name 1]:
  - Purpose: [Why this file is important to the project]
  - Current State: [Summary of recent changes or modifications]
  - Key Code Segments: [Important functions/classes with brief explanations]
  - Dependencies: [How this relates to other components]
- [File Name 2]:
  - Purpose: [Role in the project]
  - Current State: [Modification status]
  - Key Code Segments: [Critical code blocks]
  - [Additional files as needed]

4. Problem Resolution:
- Issues Encountered: [Technical problems, bugs, or challenges faced]
- Solutions Implemented: [How problems were resolved and reasoning]
- Debugging Context: [Ongoing troubleshooting efforts or known issues]
- Lessons Learned: [Important insights or patterns discovered]

5. Progress Tracking:
- Completed Tasks: [What has been successfully implemented with status indicators]
- Partially Complete Work: [Tasks in progress with current completion status]
- Validated Outcomes: [Features or code confirmed working through testing]

6. Active Work State:
- Current Focus: [Precisely what was being worked on in most recent messages]
- Recent Context: [Detailed description of last few conversation exchanges]
- Working Code: [Code snippets being modified or discussed recently]
- Immediate Context: [Specific problem or feature being addressed before summary]

7. Recent Operations:
- Last Agent Commands: [Specific tools/actions executed just before summarization with exact command names]
- Tool Results Summary: [Key outcomes from recent tool executions - truncate long results but keep essential info]
- Pre-Summary State: [What the agent was actively doing when token budget was exceeded]
- Operation Context: [Why these specific commands were executed and their relationship to user goals]

8. Continuation Plan:
- [Pending Task 1]: [Details and specific next steps with verbatim quotes]
- [Pending Task 2]: [Requirements and continuation context]
- [Priority Information]: [Which tasks are most urgent or logically sequential]
- [Next Action]: [Immediate next step with direct quotes from recent messages]
</summary>

## Quality Guidelines

- **Precision**: Include exact filenames, function names, variable names, and technical terms
- **Completeness**: Capture all context needed to continue without re-reading the full conversation
- **Clarity**: Write for someone who needs to pick up exactly where the conversation left off
- **Verbatim Accuracy**: Use direct quotes for task specifications and recent work context
- **Technical Depth**: Include enough detail for complex technical decisions and code patterns
- **Logical Flow**: Present information in a way that builds understanding progressively

This summary should serve as a comprehensive handoff document that enables seamless continuation of all active work streams while preserving the full technical and contextual richness of the original conversation.";

    [JsonPropertyName("ConversationHistorySummaryPrompt")]
    public string ConversationHistorySummaryPromptString { get; set; } = DefaultConversationHistorySummaryPrompt;

    [JsonIgnore] public string ConversationHistorySummaryPrompt => ConversationHistorySummaryPromptString;

    #endregion

    #region subject summary

    public bool EnableAutoSubjectGeneration { get; set; } = true;

    [JsonIgnore] public ModelSelectionPopupViewModel SubjectSummaryPopupViewModel { get; }

    private const string DefaultSubjectSummarizePrompt =
        "Give a title of the dialog that does not exceed {0} words.";

    [JsonPropertyName("SubjectSummarizePrompt")]
    public string SubjectPromptString { get; set; } = DefaultSubjectSummarizePrompt;

    [JsonIgnore]
    public string SubjectSummarizePrompt
    {
        get { return string.Format(SubjectPromptString, 10); }
    }

    public ILLMChatClient? CreateSubjectSummarizeClient()
    {
        if (!EnableAutoSubjectGeneration)
        {
            return null;
        }

        if (SubjectSummarizeClientPersist == null)
        {
            return null;
        }

        return Mapper?
            .Map<ParameterizedLLMModelPO, ILLMChatClient>(SubjectSummarizeClientPersist, (options => { }));
    }

    public void ApplySubjectSummarizeClient(IParameterizedLLMModel? value)
    {
        if (value == null)
        {
            SubjectSummarizeClientPersist = null;
            return;
        }

        SubjectSummarizeClientPersist = Mapper?
            .Map<IParameterizedLLMModel, ParameterizedLLMModelPO>(value, (options => { }));
    }

    [JsonPropertyName("SubjectSummarizeClient")]
    public ParameterizedLLMModelPO? SubjectSummarizeClientPersist
    {
        get;
        set
        {
            if (Equals(value, field)) return;
            field = value;
            OnPropertyChanged();
        }
    }

    #endregion

    #region text formatter

    [JsonIgnore] public ModelSelectionPopupViewModel TextFormatterPopupViewModel { get; }

    public ILLMChatClient? CreateTextFormatterClient()
    {
        if (TextFormatterClientPersist == null)
        {
            return null;
        }

        return Mapper?
            .Map<ParameterizedLLMModelPO, ILLMChatClient>(TextFormatterClientPersist, (options => { }));
    }

    public void ApplyTextFormatterClient(IParameterizedLLMModel? value)
    {
        if (value == null)
        {
            TextFormatterClientPersist = null;
            return;
        }

        TextFormatterClientPersist = Mapper?
            .Map<IParameterizedLLMModel, ParameterizedLLMModelPO>(value, (options => { }));
    }

    [JsonPropertyName("TextFormatterClientPersist")]
    public ParameterizedLLMModelPO? TextFormatterClientPersist
    {
        get;
        set
        {
            if (Equals(value, field)) return;
            field = value;
            OnPropertyChanged();
        }
    }

    #endregion

    #region search

    public GoogleSearchOption GoogleSearchOption { get; set; } = new();

    public ITextSearch? GetTextSearch()
    {
        return GoogleSearchOption.GetTextSearch();
    }

    /// <summary>
    /// as global option
    /// </summary>
    public ProxyOption ProxyOption { get; set; } = new ProxyOption();

    #endregion

    public RagOption RagOption { get; set; } = new RagOption();

    public ReactHistoryCompressionOptions HistoryCompression { get; set; } = new();

    /*public ObservableCollection<ILLMChatModel> SuggestedModels { get; } =
        new ObservableCollection<ILLMChatModel>();*/

    [JsonIgnore]
    public ICommand SaveCommand => new ActionCommand(async (param) =>
    {
        if (HasErrors)
        {
            MessageEventBus.Publish("Cannot save global configuration due to validation errors.");
            return;
        }

        var currentDirectory = Directory.GetCurrentDirectory();
        var configFilePath = Path.GetFullPath(DefaultConfigFile, currentDirectory);
        try
        {
            await this.SaveJsonToFileAsync(configFilePath, Extension.DefaultJsonSerializerOptions);
        }
        catch (Exception e)
        {
            MessageBoxes.Error("Failed to save global configuration: " + e.Message, "Error");
        }

        MessageEventBus.Publish("Global configuration saved successfully.");
    });

    public static async Task<GlobalOptions> LoadOrCreate(string? configFilePath = DEFAULT_GLOBAL_CONFIG_FILE)
    {
        configFilePath ??= DefaultConfigFile;
        var currentDirectory = Directory.GetCurrentDirectory();
        configFilePath = Path.GetFullPath(configFilePath, currentDirectory);
        var fileInfo = new FileInfo(configFilePath);
        if (fileInfo.Exists)
        {
            try
            {
                using (var fileStream = fileInfo.OpenRead())
                {
                    var config =
                        await JsonSerializer.DeserializeAsync<GlobalOptions>(fileStream,
                            Extension.DefaultJsonSerializerOptions);
                    if (config != null)
                    {
                        return config;
                    }
                }
            }
            catch (Exception e)
            {
                Trace.TraceError(e.Message);
            }
        }

        return new GlobalOptions();
    }
}