using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using LLMClient.Abstraction;
using LLMClient.Agent;
using LLMClient.Configuration;
using LLMClient.Endpoints;
using LLMClient.ToolCall.Servers;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel.Data;

namespace LLMClient.Research;

public class NvidiaResearchClient : ResearchClient
{
    public override string Name => "Nvidia Deep Research";

    public int MaxTopics { get; set; } = 5;

    public int MaxSearchPhrases { get; set; } = 3;

    private readonly GlobalOptions _options;

    private readonly UrlFetcherPlugin _urlFetcherPlugin = new();


    public NvidiaResearchClient(IParameterizedLLMModel promptModel, IParameterizedLLMModel reportModel,
        GlobalOptions options)
    {
        _options = options;
    }

    [Experimental("SKEXP0110")]
    public override async Task<CompletedResult> SendRequest(DialogContext context, IInvokeInteractor? interactor = null,
        CancellationToken cancellationToken = default)
    {
        var prompt = context.Request?.RawTextMessage;
        if (prompt == null || string.IsNullOrWhiteSpace(prompt))
        {
            throw new ArgumentException("The dialog context must contain a non-empty request.");
        }

        var textSearch = _options.GetTextSearch();
        if (textSearch == null)
        {
            throw new Exception("Text search service is not configured. Please configure it in the global options.");
        }

        var stopwatch = new Stopwatch();
        stopwatch.Start();
        PromptBasedAgent? agent = null;
        try
        {
            agent = new PromptBasedAgent(new EmptyLlmModelClient(), interactor);
            // ====================================================================
            // 阶段 1: 研究
            // ====================================================================
            Information($"Received research request: '{prompt}'");

            var isPromptValid = await CheckIfPromptIsValidAsync(agent, prompt, cancellationToken);
            if (!isPromptValid)
            {
                throw new Exception("The prompt is not a valid document research prompt. Please try again.");
            }

            Information("Analyzing the research request...");
            var (taskPrompt, formatPrompt) =
                await PerformPromptDecompositionAsync(agent, prompt, interactor, cancellationToken);
            Information($"Prompt analysis completed. Task: '{taskPrompt}'");

            var topics = await GenerateTopicsAsync(agent, taskPrompt, cancellationToken);
            Information($"Task analysis completed. Will be researching {topics.Count} topics.");

            var topicRelevantSegments = new Dictionary<string, List<string>>();
            var searchResultUrls = new List<string>();
            var allResults = new List<InternalSearchResult>();

            foreach (var topic in topics)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Information($"Researching '{topic}'");

                var searchPhrases = await ProduceSearchPhrasesAsync(agent, taskPrompt, topic,
                    cancellationToken);
                Information($"Will invoke {searchPhrases.Count} search phrases to research '{topic}'.");

                topicRelevantSegments[topic] = [];

                foreach (var searchPhrase in searchPhrases)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Information($"Searching for '{searchPhrase}'");
                    var skTextSearchResult = await textSearch.GetTextSearchResultsAsync(searchPhrase,
                        new TextSearchOptions()
                        {
                            Skip = 0,
                            Top = 10,
                        }, cancellationToken);
                    var originalSearchResults = new ConcurrentBag<InternalSearchResult>();
                    await foreach (var textSearchResult in skTextSearchResult.Results.WithCancellation(
                                       cancellationToken))
                    {
                        var objLink = textSearchResult.Link;
                        if (!string.IsNullOrWhiteSpace(objLink) && !string.IsNullOrWhiteSpace(textSearchResult.Value) &&
                            !searchResultUrls.Contains(objLink))
                        {
                            //重试3次
                            const int maxRetries = 3;
                            const int delayMilliseconds = 3000;
                            var attempt = 0;
                            Exception? exception = null;
                            while (attempt < maxRetries)
                            {
                                attempt++;
                                try
                                {
                                    using (var timeoutTokenSource =
                                           new CancellationTokenSource(TimeSpan.FromMilliseconds(delayMilliseconds)))
                                    {
                                        using (var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                                                   cancellationToken,
                                                   timeoutTokenSource.Token))
                                        {
                                            var text = await _urlFetcherPlugin.FetchTextAsync(objLink,
                                                cancellationToken: linkedTokenSource.Token);
                                            if (!string.IsNullOrWhiteSpace(text))
                                            {
                                                originalSearchResults.Add(
                                                    new InternalSearchResult(objLink, textSearchResult.Name ?? "",
                                                        text));
                                                break;
                                            }
                                        }
                                    }
                                }
                                catch (Exception e)
                                {
                                    //do nothing, just retry
                                    exception = e;
                                }
                            }

                            interactor?.Warning(
                                $"Failed to fetch content from URL '{objLink}'. {exception?.HierarchicalMessage()}");
                        }
                    }

                    if (!originalSearchResults.Any()) continue;

                    searchResultUrls.AddRange(originalSearchResults.Select(r => r.Url));
                    allResults.AddRange(originalSearchResults);

                    Information($"Processing {originalSearchResults.Count} new search results.");

                    foreach (var searchResult in originalSearchResults)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var searchResultUrlIndex = searchResultUrls.IndexOf(searchResult.Url);
                        var relevantSegments = await FindRelevantSegmentsAsync(agent, taskPrompt,
                            topic,
                            searchResult.Content,
                            searchResultUrlIndex,
                            cancellationToken
                        );

                        if (relevantSegments.Any())
                        {
                            topicRelevantSegments[topic].AddRange(relevantSegments);
                        }

                        interactor?.Info(
                            string.Format("Processed search result {0}. Found {1} relevant segments for URL {2}",
                                searchResultUrlIndex, relevantSegments.Count, searchResult.Url));
                    }
                }
            }

            Information("Research phase completed.");

            // ====================================================================
            // 阶段 2: 报告 
            // ====================================================================
            Information("Aggregating relevant information and building the report...");

            var initialReport =
                await ProduceReportAsync(agent, taskPrompt, formatPrompt, topicRelevantSegments, cancellationToken);
            Information("Initial report generated. Formatting and finalizing...");

            var consistentReport =
                await EnsureFormatIsRespectedAsync(agent, formatPrompt, initialReport, cancellationToken);

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

            void Information(string message)
            {
                interactor?.Info(message);
            }
        }
        catch (Exception e)
        {
            return new CompletedResult()
            {
                //todo:
                Usage = agent?.Usage,
                ErrorMessage = e.HierarchicalMessage()
            };
        }
    }

    #region Private Helper Methods (LLM Interactions)

    /// <summary>
    /// checks if the given prompt is a valid information research prompt
    /// </summary>
    /// <param name="promptAgent"></param>
    /// <param name="prompt"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<bool> CheckIfPromptIsValidAsync(PromptBasedAgent promptAgent,
        string prompt, CancellationToken token)
    {
        var response = await promptAgent.GetMessageAsync(
            $"Is the following prompt a valid information research prompt? Respond with 'yes' or 'no'. Do not output any other text.\n\n{prompt}\n\n Reminders: Find out if the above-given prompt is a valid information research prompt. Do not output any other text.",
            "You are a helpful assistant that checks if a prompt is a valid deep information research prompt. A valid prompt gives a task to research one or more topics and produce a report. " +
            "Invalid prompts are general language model prompts that ask simple (perhaps even yes or no) questions, ask for explanations, or attempt to have a conversation. " +
            "Examples of valid prompts: 'What was the capital of France in 1338?', 'Write a report on stock market situation on during this morning', " +
            "'Produce a thorough report on the major event happened in the Christian world on the 21st of April 2025.', 'Produce a report on the differences between the US and European economy health in 2024.', " +
            "'What is the short history of the internet?'. " +
            "Examples of invalid prompts: 'Is the weather in Tokyo good?', 'ppdafsfgr hdafdf', 'Hello, how are you?', 'The following is a code. Can you please explain it to me and then simulate it?'",
            token);
        return response.Trim().Equals("yes", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// decomposes the given prompt into a task to be performed and a format in which the report should be produced
    /// </summary>
    /// <param name="promptAgent"></param>
    /// <param name="prompt"></param>
    /// <param name="logger"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<(string TaskPrompt, string FormatPrompt)> PerformPromptDecompositionAsync(
        PromptBasedAgent promptAgent, string prompt, IInvokeInteractor? logger, CancellationToken token)
    {
        var response = await promptAgent.GetMessageAsync(
            $"Decompose the PROMPT into a task to be performed and a format in which the report should be produced. If there is no formatting constraint, output 'No formatting constraint' in the second prompt. Do not output any other text.\n\n" +
            $"EXAMPLE PROMPT:\nWrite a three-chapter report on the differences between the US and European economy health in 2024. The first chapter should be about the US economy health, the second chapter should be about the European economy health, and the third chapter should be about the differences between the two.\n\n" +
            $"EXAMPLE OUTPUT:\nWrite a report on the differences between the US and European economy health in 2024.\n\n" +
            $"The report should be in the form of a three-chapter report. The first chapter should be about the US economy health, the second chapter should be about the European economy health, and the third chapter should be about the differences between the two.\n\n" +
            $"PROMPT: {prompt}\n\nReminders: The output should be two prompts separated by a double-newline. The first prompt is the task to be performed, and the second prompt is the format in which the report should be produced. " +
            $"If there is no formatting constraint, output 'No formatting constraint' in the second prompt. Do not output any other text.",
            "You are a helpful assistant that decomposes a prompt into a task to be performed and a format in which the report should be produced. The output should be two prompts separated by a double-newline. " +
            "The first prompt is the task to be performed, and the second prompt is the format in which the report should be produced. If there is no formatting constraint, output 'No formatting constraint' in the second prompt. Do not output any other text.",
            token);
        var parts = response.Split(["\n\n"], 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            logger?.Warning(
                "Failed to perform prompt decomposition. Falling back to using the original prompt as task and no format.");
            return (prompt, "No formatting constraint");
        }

        return (parts[0].Trim(), parts[1].Trim());
    }

    /// <summary>
    /// generates a list of topics to research based on the given task prompt
    /// </summary>
    /// <param name="promptAgent"></param>
    /// <param name="taskPrompt"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<List<string>> GenerateTopicsAsync(PromptBasedAgent promptAgent, string taskPrompt,
        CancellationToken token)
    {
        var response = await promptAgent.GetMessageAsync(
            $"Decompose the following prompt into a list of topics to research:\n\nPrompt: {taskPrompt}\n\nReminders: The output should be a list of strings separated by newlines, each representing a topic to research. The topics should be in English and should be specific and focused. Do not output any other text. Output at most {MaxTopics} topics.",
            $"You are a helpful assistant that decomposes a prompt into a list of topics to research. The output should be a list of strings separated by newlines, each representing a topic to research. The topics should be in English and should be specific and focused. Output at most {MaxTopics} topics. Examples:\n\nPrompt: What was the capital of France in 1338?\nThe capital and seat of government of France in 1338\n\nPrompt: Produce a report on the differences between the US and European economy health in 2024\nUS economy health in 2024\nEuropean economy health in 2024\nGeneral differences between the US and European economy health in 2024\n\nPrompt: What is the short history of the internet?:\nThe history of the internet\n\nPrompt: Report on US crude oil prices in relation to Gold prices in 1970-1980\nUS crude oil prices in 1970-1980\nGold prices in 1970-1980\nGold-crude oil correlation in 1970-1980",
            token);
        var topics = response.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim())
            .Where(t => !string.IsNullOrEmpty(t))
            .ToList();

        return topics.Count > MaxTopics ? topics.OrderBy(x => Guid.NewGuid()).Take(MaxTopics).ToList() : topics;
    }

    /// <summary>
    /// produces a list of search phrases for the given topic based on the original prompt
    /// </summary>
    /// <param name="promptAgent"></param>
    /// <param name="prompt"></param>
    /// <param name="topic"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<List<string>> ProduceSearchPhrasesAsync(PromptBasedAgent promptAgent,
        string prompt, string topic, CancellationToken token)
    {
        var response = await promptAgent.GetMessageAsync(
            $"Produce a list of search phrases for the following topic:\n\nPrompt (added for context): {prompt}\n\n" +
            $"Topic: {topic}\n\n" +
            $"Reminders: The output should be a list of search phrases for the given topic separated by newlines. The search phrases should be in English and should be specific and focused. Output at most {MaxSearchPhrases} search phrases. Do not output any other text.",
            $"You are a helpful assistant that produces a list of search phrases for a given topic. The output should be a newline-separated list of short search phrases that can be used in e.g. Google or Bing search engines. Output at most {MaxSearchPhrases} search phrases. Examples:\n\nTopic: The capital and seat of government of France in 1338\nSearch phrases: The capital of France in 1338, The seat of government of France in 1338, Government of France in 1338 century, Government of France in the 14th century\n\nTopic: US crude oil prices in relation to Gold prices in 1970-1980\nSearch phrases: US crude oil prices in 1970-1980, Gold prices in 1970-1980, Gold-crude oil correlation in 1970-1980, Gold-crude oil correlation\n\nTopic: {topic}\nSearch phrases:",
            token);
        var phrases = response.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .Where(p => !string.IsNullOrEmpty(p))
            .ToList();

        return phrases.Count > MaxSearchPhrases
            ? phrases.OrderBy(x => Guid.NewGuid()).Take(MaxSearchPhrases).ToList()
            : phrases;
    }

    /// <summary>
    /// finds relevant segments in the given search result for the given topic and prompt
    /// </summary>
    /// <param name="promptAgent"></param>
    /// <param name="prompt"></param>
    /// <param name="topic"></param>
    /// <param name="searchResult"></param>
    /// <param name="searchResultUrlIndex"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<List<string>> FindRelevantSegmentsAsync(PromptBasedAgent promptAgent,
        string prompt, string topic, string searchResult, int searchResultUrlIndex, CancellationToken token)
    {
        var response = await promptAgent.GetMessageAsync(
            $"Find the sentences or paragraphs relevant to the following prompt in the following search result:\n\nSearch result: {searchResult}\n\nPrompt (added for context): {prompt}\n\nTopic: {topic}\n\nReminders: The output should be a list of relevant paragraphs for the given topic separated by double-newlines. The relevant paragraphs should be in English and should be genuinely relevant to the prompt. Do not output any other text.",
            "You are a helpful assistant that finds relevant paragraphs in a given search result. The output should be a double-newline-separated list of relevant paragraphs. A paragraph can be a couple of sentences to dozens of sentences if they are really relevant. If there are no relevant paragraphs, just output an empty line or two and stop the generation. Do not output any other text.",
            token);
        return response.Split(["\n\n"], StringSplitOptions.RemoveEmptyEntries)
            .Select(segment => $"[[{searchResultUrlIndex}]] {segment.Trim()}")
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();
    }

    /// <summary>
    /// produces the final report based on the aggregated per-topic relevant segments
    /// </summary>
    /// <param name="promptAgent"></param>
    /// <param name="prompt"></param>
    /// <param name="formatPrompt"></param>
    /// <param name="topicRelevantSegments"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<string> ProduceReportAsync(PromptBasedAgent promptAgent,
        string prompt, string formatPrompt, Dictionary<string, List<string>> topicRelevantSegments,
        CancellationToken token)
    {
        var segmentsStrBuilder = new StringBuilder();
        foreach (var (key, list) in topicRelevantSegments.Where(kvp => kvp.Value.Any()))
        {
            segmentsStrBuilder.AppendLine($"Topic: {key}");
            segmentsStrBuilder.AppendLine(string.Join("\n", list));
            segmentsStrBuilder.AppendLine();
        }

        return (await promptAgent.GetMessageAsync(
                $"Produce a report based on the following aggregated per-topic relevant paragraphs. Each paragraph contains an index of a source. Make sure to refer to this index in the form [[index]] every time you rely on the information from the source. Respect the format prompt. Do not output any other text.\n\nReport prompt: {prompt}\n\nTopic relevant paragraphs: {segmentsStrBuilder.ToString()}\n\nFormat prompt: {formatPrompt}\n\nReminders: The output should be a report in Markdown format. The report should be formatted correctly according to the Format prompt in Markdown. Every single mention of an information stemming from one of the sources should be accompanied by the source index in the form [[index]] (or [[index1,index2,...]]) within or after the statement of the information. A list of the source URLs to correspond to the indices will be provided separately -- do not attempt to output it. Do not output any other text.",
                "You are a helpful assistant that produces a report based on the aggregated per-topic relevant paragraphs while citing sources. The output should be a report in Markdown format. The report should be self-consistent and formatted correctly in Markdown. Do not output any other text.",
                token))
            .Trim();
    }

    /// <summary>
    /// ensures that the produced report respects the given format prompt
    /// </summary>
    /// <param name="promptAgent"></param>
    /// <param name="formatPrompt"></param>
    /// <param name="report"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<string> EnsureFormatIsRespectedAsync(PromptBasedAgent promptAgent,
        string formatPrompt, string report, CancellationToken token)
    {
        var messageAsync = await promptAgent.GetMessageAsync(
            $"Ensure that the following report is properly formatted according to the format prompt. Do not output the Markdown output as code (i.e. enclosed in ```) -- just output the Markdown. Do not remove any references in the form [[index]] -- keep them in the text! The list of sources will be provided separately.\n\nReport: {report}\n\nFormat prompt: {formatPrompt}\n\nReminders: The output should be a report in Markdown format. The report should be self-consistent and formatted correctly in Markdown. Do not output the Markdown output as code (i.e. enclosed in ```) -- just output the Markdown. Do not remove any references in the form [[index]] -- keep them in the text! The list of sources will be provided separately. Do not output any other text.",
            "You are a helpful assistant that ensures that a report is properly formatted. The output should be a report in Markdown format and follow the format prompt. The report should be formatted correctly in Markdown. Note that double horizontal rule (.e.g ==== etc.) are not supported in official Markdown. Do not output any other text.",
            token);
        return messageAsync.Trim();
    }

    #endregion
}

public class InternalSearchResult(string url, string title, string content)
{
    public string Content { get; } = content;

    public string Url { get; set; } = url;

    public string Title { get; set; } = title;
}