using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using LLMClient.Abstraction;
using LLMClient.Agent;
using LLMClient.Dialog;
using LLMClient.Endpoints;
using LLMClient.MCP.Servers;
using LLMClient.UI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.Data;

namespace LLMClient.Research;

public class NvidiaResearchClient : ResearchClient
{
    public override string Name => "Nvidia Deep Research via " + ProxyClient.Name;

    public int MaxTopics { get; set; } = 5;

    public int MaxSearchPhrases { get; set; } = 3;

    private readonly IRagSource _searchService;

    public NvidiaResearchClient(ILLMChatClient proxyClient, IRagSource searchService) :
        base(proxyClient)
    {
        _searchService = searchService;
    }

    [Experimental("SKEXP0110")]
    public override async Task<CompletedResult> SendRequest(DialogContext context, Action<string>? stream = null,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            var prompt = context.Request?.TextMessage;
            if (prompt == null || string.IsNullOrWhiteSpace(prompt))
            {
                throw new ArgumentException("The dialog context must contain a non-empty request.");
            }

            var agent = new PromptAgent(this.ProxyClient, stream, logger);

            // ====================================================================
            // 阶段 1: 研究 
            // ====================================================================
            Information("Received research request: '{0}'", prompt);

            var isPromptValid = await CheckIfPromptIsValidAsync(agent, prompt);
            if (!isPromptValid)
            {
                throw new Exception("The prompt is not a valid document research prompt. Please try again.");
            }

            Information("Analyzing the research request...");
            (var taskPrompt, var formatPrompt) = await PerformPromptDecompositionAsync(agent, prompt, logger);
            Information("Prompt analysis completed. Task: '{0}'", taskPrompt);

            var topics = await GenerateTopicsAsync(agent, taskPrompt);
            Information("Task analysis completed. Will be researching {0} topics.", topics.Count);

            var topicRelevantSegments = new Dictionary<string, List<string>>();
            var searchResultUrls = new List<string>();
            var allResults = new List<InternalSearchResult>();

            foreach (var topic in topics)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Information("Researching '{0}'", topic);

                var searchPhrases = await ProduceSearchPhrasesAsync(agent, taskPrompt, topic);
                Information("Will invoke {0} search phrases to research '{Topic}'.",
                    searchPhrases.Count, topic);

                topicRelevantSegments[topic] = [];

                foreach (var searchPhrase in searchPhrases)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Information("Searching for '{0}'", searchPhrase);
                    List<InternalSearchResult> originalSearchResults;
                    if (_searchService is GoogleSearchPlugin)
                    {
                        var searchResult = (SKTextSearchResult)await _searchService.QueryAsync(searchPhrase,
                            new TextSearchOptions()
                            {
                                Skip = 0,
                                Top = 10,
                            }, cancellationToken);
                        var searchResults =
                            (await searchResult.Results.ToArrayAsync(cancellationToken: cancellationToken)).Select(r =>
                                new InternalSearchResult(r.Link ?? String.Empty, r.Name ?? String.Empty, r.Value));
                        originalSearchResults = searchResults
                            .Where(r => !string.IsNullOrWhiteSpace(r.Url) && !string.IsNullOrWhiteSpace(r.Content) &&
                                        !searchResultUrls.Contains(r.Url))
                            .ToList();
                    }
                    else
                    {
                        throw new NotSupportedException("Not supported search service type.");
                    }

                    if (!originalSearchResults.Any()) continue;

                    searchResultUrls.AddRange(originalSearchResults.Select(r => r.Url));
                    allResults.AddRange(originalSearchResults);

                    Information("Processing {0} new search results.", originalSearchResults.Count);

                    foreach (var searchResult in originalSearchResults)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var searchResultUrlIndex = searchResultUrls.IndexOf(searchResult.Url);
                        var relevantSegments = await FindRelevantSegmentsAsync(agent, taskPrompt,
                            topic,
                            searchResult.Content,
                            searchResultUrlIndex
                        );

                        if (relevantSegments.Any())
                        {
                            topicRelevantSegments[topic].AddRange(relevantSegments);
                        }

                        logger?.LogDebug(
                            "Processed search result {Index}. Found {SegmentCount} relevant segments for URL {Url}",
                            searchResultUrlIndex, relevantSegments.Count, searchResult.Url);
                    }
                }
            }

            Information("Research phase completed.");

            // ====================================================================
            // 阶段 2: 报告 (对应原 do_reporting 函数)
            // ====================================================================
            Information("Aggregating relevant information and building the report...");

            var initialReport =
                await ProduceReportAsync(agent, taskPrompt, formatPrompt, topicRelevantSegments);
            Information("Initial report generated. Formatting and finalizing...");

            var consistentReport =
                await EnsureFormatIsRespectedAsync(agent, formatPrompt, initialReport);

            var finalReportBuilder = new StringBuilder(consistentReport);
            finalReportBuilder.AppendLine("\n\n---");

            for (var i = 0; i < searchResultUrls.Count; i++)
            {
                var result = allResults.FirstOrDefault(r => r.Url == searchResultUrls[i]);
                finalReportBuilder.AppendLine(result != null
                    ? $" - [[{i}]] [{result.Title}][{i}]"
                    : $" - [[{i}]] [Source {i}][{i}]");
            }

            finalReportBuilder.AppendLine("\n");
            for (var i = 0; i < searchResultUrls.Count; i++)
            {
                finalReportBuilder.AppendLine($"[{i}]: {searchResultUrls[i]}");
            }

            var finalReport = finalReportBuilder.ToString();
            Information("Report completed.");

            return new CompletedResult
            {
                Usage = agent.Usage,
                Latency = 0,
                Duration = stopwatch.Elapsed.Seconds,
                Price = agent.Price,
                ResponseMessages = [new ChatMessage(ChatRole.Assistant, finalReport)]
            };

            void Information(string chunk, params object[] args)
            {
                var formattedChunk = args.Length != 0 ? string.Format(chunk, args) : chunk;
                stream?.Invoke(formattedChunk);
            }
        }
        catch (Exception e)
        {
            return new CompletedResult()
            {
                ErrorMessage = e.Message
            };
        }
    }

    #region Private Helper Methods (LLM Interactions)

    private async Task<bool> CheckIfPromptIsValidAsync(PromptAgent promptAgent,
        string prompt)
    {
        var response = await promptAgent.GetMessageAsync(
            $"Is the following prompt a valid information research prompt? Respond with 'yes' or 'no'. Do not output any other text.\n\n{prompt}\n\n Reminders: Find out if the above-given prompt is a valid information research prompt. Do not output any other text.",
            "You are a helpful assistant that checks if a prompt is a valid deep information research prompt. A valid prompt gives a task to research one or more topics and produce a report. Invalid prompts are general language model prompts that ask simple (perhaps even yes or no) questions, ask for explanations, or attempt to have a conversation. Examples of valid prompts: 'What was the capital of France in 1338?', 'Write a report on stock market situation on during this morning', 'Produce a thorough report on the major event happened in the Christian world on the 21st of April 2025.', 'Produce a report on the differences between the US and European economy health in 2024.', 'What is the short history of the internet?'. Examples of invalid prompts: 'Is the weather in Tokyo good?', 'ppdafsfgr hdafdf', 'Hello, how are you?', 'The following is a code. Can you please explain it to me and then simulate it?'");
        return response.Trim().Equals("yes", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<(string TaskPrompt, string FormatPrompt)> PerformPromptDecompositionAsync(
        PromptAgent promptAgent,
        string prompt, ILogger? logger)
    {
        var response = await promptAgent.GetMessageAsync(
            $"Decompose the PROMPT into a task to be performed and a format in which the report should be produced. If there is no formatting constraint, output 'No formatting constraint' in the second prompt. Do not output any other text.\n\nEXAMPLE PROMPT:\nWrite a three-chapter report on the differences between the US and European economy health in 2024. The first chapter should be about the US economy health, the second chapter should be about the European economy health, and the third chapter should be about the differences between the two.\n\nEXAMPLE OUTPUT:\nWrite a report on the differences between the US and European economy health in 2024.\n\nThe report should be in the form of a three-chapter report. The first chapter should be about the US economy health, the second chapter should be about the European economy health, and the third chapter should be about the differences between the two.\n\nPROMPT: {prompt}\n\nReminders: The output should be two prompts separated by a double-newline. The first prompt is the task to be performed, and the second prompt is the format in which the report should be produced. If there is no formatting constraint, output 'No formatting constraint' in the second prompt. Do not output any other text.",
            "You are a helpful assistant that decomposes a prompt into a task to be performed and a format in which the report should be produced. The output should be two prompts separated by a double-newline. The first prompt is the task to be performed, and the second prompt is the format in which the report should be produced. If there is no formatting constraint, output 'No formatting constraint' in the second prompt. Do not output any other text.");
        var parts = response.Split(["\n\n"], 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            logger?.LogWarning(
                "Failed to perform prompt decomposition. Falling back to using the original prompt as task and no format.");
            return (prompt, "No formatting constraint");
        }

        return (parts[0].Trim(), parts[1].Trim());
    }

    private async Task<List<string>> GenerateTopicsAsync(PromptAgent promptAgent,
        string taskPrompt)
    {
        var response = await promptAgent.GetMessageAsync(
            $"Decompose the following prompt into a list of topics to research:\n\nPrompt: {taskPrompt}\n\nReminders: The output should be a list of strings separated by newlines, each representing a topic to research. The topics should be in English and should be specific and focused. Do not output any other text. Output at most {MaxTopics} topics.",
            $"You are a helpful assistant that decomposes a prompt into a list of topics to research. The output should be a list of strings separated by newlines, each representing a topic to research. The topics should be in English and should be specific and focused. Output at most {MaxTopics} topics. Examples:\n\nPrompt: What was the capital of France in 1338?\nThe capital and seat of government of France in 1338\n\nPrompt: Produce a report on the differences between the US and European economy health in 2024\nUS economy health in 2024\nEuropean economy health in 2024\nGeneral differences between the US and European economy health in 2024\n\nPrompt: What is the short history of the internet?:\nThe history of the internet\n\nPrompt: Report on US crude oil prices in relation to Gold prices in 1970-1980\nUS crude oil prices in 1970-1980\nGold prices in 1970-1980\nGold-crude oil correlation in 1970-1980");
        var topics = response.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim())
            .Where(t => !string.IsNullOrEmpty(t))
            .ToList();

        return topics.Count > MaxTopics ? topics.OrderBy(x => Guid.NewGuid()).Take(MaxTopics).ToList() : topics;
    }

    private async Task<List<string>> ProduceSearchPhrasesAsync(PromptAgent promptAgent,
        string prompt, string topic)
    {
        var response = await promptAgent.GetMessageAsync(
            $"Produce a list of search phrases for the following topic:\n\nPrompt (added for context): {prompt}\n\nTopic: {topic}\n\nReminders: The output should be a list of search phrases for the given topic separated by newlines. The search phrases should be in English and should be specific and focused. Output at most {MaxSearchPhrases} search phrases. Do not output any other text.",
            $"You are a helpful assistant that produces a list of search phrases for a given topic. The output should be a newline-separated list of short search phrases that can be used in e.g. Google or Bing search engines. Output at most {MaxSearchPhrases} search phrases. Examples:\n\nTopic: The capital and seat of government of France in 1338\nSearch phrases: The capital of France in 1338, The seat of government of France in 1338, Government of France in 1338 century, Government of France in the 14th century\n\nTopic: US crude oil prices in relation to Gold prices in 1970-1980\nSearch phrases: US crude oil prices in 1970-1980, Gold prices in 1970-1980, Gold-crude oil correlation in 1970-1980, Gold-crude oil correlation\n\nTopic: {topic}\nSearch phrases:");
        var phrases = response.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .Where(p => !string.IsNullOrEmpty(p))
            .ToList();

        return phrases.Count > MaxSearchPhrases
            ? phrases.OrderBy(x => Guid.NewGuid()).Take(MaxSearchPhrases).ToList()
            : phrases;
    }

    private async Task<List<string>> FindRelevantSegmentsAsync(PromptAgent promptAgent,
        string prompt, string topic, string searchResult,
        int searchResultUrlIndex)
    {
        var response = await promptAgent.GetMessageAsync(
            $"Find the sentences or paragraphs relevant to the following prompt in the following search result:\n\nSearch result: {searchResult}\n\nPrompt (added for context): {prompt}\n\nTopic: {topic}\n\nReminders: The output should be a list of relevant paragraphs for the given topic separated by double-newlines. The relevant paragraphs should be in English and should be genuinely relevant to the prompt. Do not output any other text.",
            "You are a helpful assistant that finds relevant paragraphs in a given search result. The output should be a double-newline-separated list of relevant paragraphs. A paragraph can be a couple of sentences to dozens of sentences if they are really relevant. If there are no relevant paragraphs, just output an empty line or two and stop the generation. Do not output any other text.");
        return response.Split(["\n\n"], StringSplitOptions.RemoveEmptyEntries)
            .Select(segment => $"[[{searchResultUrlIndex}]] {segment.Trim()}")
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();
    }

    private async Task<string> ProduceReportAsync(PromptAgent promptAgent,
        string prompt, string formatPrompt,
        Dictionary<string, List<string>> topicRelevantSegments)
    {
        var segmentsStrBuilder = new StringBuilder();
        foreach (var entry in topicRelevantSegments.Where(kvp => kvp.Value.Any()))
        {
            segmentsStrBuilder.AppendLine($"Topic: {entry.Key}");
            segmentsStrBuilder.AppendLine(string.Join("\n", entry.Value));
            segmentsStrBuilder.AppendLine();
        }

        return (await promptAgent.GetMessageAsync(
                $"Produce a report based on the following aggregated per-topic relevant paragraphs. Each paragraph contains an index of a source. Make sure to refer to this index in the form [[index]] every time you rely on the information from the source. Respect the format prompt. Do not output any other text.\n\nReport prompt: {prompt}\n\nTopic relevant paragraphs: {segmentsStrBuilder.ToString()}\n\nFormat prompt: {formatPrompt}\n\nReminders: The output should be a report in Markdown format. The report should be formatted correctly according to the Format prompt in Markdown. Every single mention of an information stemming from one of the sources should be accompanied by the source index in the form [[index]] (or [[index1,index2,...]]) within or after the statement of the information. A list of the source URLs to correspond to the indices will be provided separately -- do not attempt to output it. Do not output any other text.",
                "You are a helpful assistant that produces a report based on the aggregated per-topic relevant paragraphs while citing sources. The output should be a report in Markdown format. The report should be self-consistent and formatted correctly in Markdown. Do not output any other text."))
            .Trim();
    }

    private async Task<string> EnsureFormatIsRespectedAsync(PromptAgent promptAgent,
        string formatPrompt, string report)
    {
        var messageAsync = await promptAgent.GetMessageAsync(
            $"Ensure that the following report is properly formatted according to the format prompt. Do not output the Markdown output as code (i.e. enclosed in ```) -- just output the Markdown. Do not remove any references in the form [[index]] -- keep them in the text! The list of sources will be provided separately.\n\nReport: {report}\n\nFormat prompt: {formatPrompt}\n\nReminders: The output should be a report in Markdown format. The report should be self-consistent and formatted correctly in Markdown. Do not output the Markdown output as code (i.e. enclosed in ```) -- just output the Markdown. Do not remove any references in the form [[index]] -- keep them in the text! The list of sources will be provided separately. Do not output any other text.",
            "You are a helpful assistant that ensures that a report is properly formatted. The output should be a report in Markdown format and follow the format prompt. The report should be formatted correctly in Markdown. Note that double horizontal rule (.e.g ==== etc.) are not supported in official Markdown. Do not output any other text.");
        return messageAsync.Trim();
    }

    #endregion
}

public class InternalSearchResult
{
    public string Content { get; }

    public string Url { get; set; }

    public string Title { get; set; }

    public InternalSearchResult(string url, string title, string content)
    {
        Url = url;
        Title = title;
        Content = content;
    }
}