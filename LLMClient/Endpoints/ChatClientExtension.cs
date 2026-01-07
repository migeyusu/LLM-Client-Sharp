using System.Text.Json;
using LLMClient.Endpoints.Messages;
using Microsoft.Extensions.AI;
using OpenAI.Chat;
using ChatFinishReason = Microsoft.Extensions.AI.ChatFinishReason;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace LLMClient.Endpoints;

public static class ChatClientExtension
{
    public static ChatFinishReason? GetFinishReasonFromAdditional(this ChatResponse? response)
    {
        if (response != null)
        {
            var messages = response.Messages;
            foreach (var message in messages)
            {
                var additionalProperties = message.AdditionalProperties;
                if (additionalProperties != null)
                {
                    if (additionalProperties.TryGetValue("FinishReason", out var finishReasonObj))
                    {
                        if (finishReasonObj is string finishReason)
                        {
                            return new ChatFinishReason(finishReason);
                        }
                    }
                }
            }
        }

        return null;
    }

    public static void TryAddExtendedData(this ChatResponseUpdate update)
    {
        if (update.RawRepresentation is StreamingChatCompletionUpdate streamingChatCompletionUpdate)
        {
            var s = streamingChatCompletionUpdate.Patch.ToString("J");
            if (string.IsNullOrEmpty(s))
            {
                return;
            }

            var chatChunk = JsonSerializer.Deserialize<Extended.ChatChunk>(s);
            var reasoningContent = chatChunk?.Choices?.FirstOrDefault()?.Delta?.ReasoningContent;
            if (reasoningContent != null && !update.Contents.Any((content => content is TextReasoningContent)))
            {
                update.Contents.Insert(0, new TextReasoningContent(reasoningContent));
            }
        }
    }


    public static UsageDetails? GetUsageDetailsFromAdditional(this ChatResponse? response)
    {
        UsageDetails? usageDetails = null;

        if (response != null)
        {
            TryGetUsage(response.AdditionalProperties);
            var messages = response.Messages;
            foreach (var message in messages)
            {
                var additionalProperties = message.AdditionalProperties;
                TryGetUsage(additionalProperties);
            }
        }

        return usageDetails;

        void TryGetUsage(AdditionalPropertiesDictionary? additionalProperties)
        {
            if (additionalProperties != null)
            {
                // 方法1: 检查 Metadata 中的 Usage 信息
                if (additionalProperties.TryGetValue("Usage", out var usageObj))
                {
                    if (usageObj is ChatTokenUsage chatTokenUsage)
                    {
                        var details = new UsageDetails()
                        {
                            InputTokenCount = chatTokenUsage.InputTokenCount,
                            OutputTokenCount = chatTokenUsage.OutputTokenCount,
                            TotalTokenCount = chatTokenUsage.TotalTokenCount,
                        };
                        if (usageDetails == null)
                        {
                            usageDetails = details;
                        }
                        else
                        {
                            usageDetails.Add(details);
                        }
                    }

                    /*if (usage.TryGetValue("TotalTokens", out var totalTokensObj))
                    {
                        // tokenCount = Convert.ToInt32(totalTokensObj);
                    }*/
                }
                // 方法2: 部分 AI 服务可能使用不同的元数据键
                /*if (usageDetails == null &&
                    dictionary.TryGetValue("CompletionTokenCount", out var completionTokensObj))
                {
                    // tokenCount = Convert.ToInt32(completionTokensObj);
                }

                // 方法3: 有些版本可能在 ModelResult 中提供 usage
                if (usageDetails == null &&
                    dictionary.TryGetValue("ModelResults", out var modelResultsObj))
                {
                    //do what?
                }*/
            }
        }
    }

    public static ChatMessage GetReasoningTrimmedMessage(this ChatMessage message)
    {
        //todo: trim <think></think> tags in TextContent too?
        if (message.Contents.Any(content => content is TextReasoningContent))
        {
            return new ChatMessage(message.Role,
                message.Contents.Where((content => content is not TextReasoningContent)).ToArray())
            {
                AdditionalProperties = message.AdditionalProperties,
                MessageId = message.MessageId,
                RawRepresentation = message.RawRepresentation,
                CreatedAt = message.CreatedAt,
                AuthorName = message.AuthorName,
            };
        }

        return message;
    }

    public static ChatResponse MergeResponse(this IEnumerable<ChatResponseUpdate> updates)
    {
        var chatResponse = updates.ToChatResponse();
        foreach (var message in chatResponse.Messages)
        {
            var oriContents = message.Contents
                .ToArray();
            message.Contents.Clear();
            //合并相同类型的内容
            foreach (var oriContent in oriContents)
            {
                if (oriContent is TextContent textContent)
                {
                    var @default = message.Contents.OfType<TextContent>().FirstOrDefault();
                    if (@default != null)
                    {
                        @default.Text += textContent.Text;
                    }
                    else
                    {
                        message.Contents.Add(textContent);
                    }
                }
                else if (oriContent is TextReasoningContent textReasoningContent)
                {
                    var @default = message.Contents.OfType<TextReasoningContent>().FirstOrDefault();
                    if (@default != null)
                    {
                        @default.Text += textReasoningContent.Text;
                    }
                    else
                    {
                        message.Contents.Insert(0, textReasoningContent);
                    }
                }
                else if (oriContent is UsageContent usageContent)
                {
                    var @default = message.Contents.OfType<UsageContent>().FirstOrDefault();
                    if (@default != null)
                    {
                        @default.Details.Add(usageContent.Details);
                    }
                    else
                    {
                        message.Contents.Add(usageContent);
                    }
                }
                else
                {
                    message.Contents.Add(oriContent);
                }
            }
        }

        return chatResponse;
    }
}