using System.ClientModel;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using LLMClient.Abstraction;
using LLMClient.Dialog.Models;
using LLMClient.Endpoints.Messages;
using Microsoft.Extensions.AI;
using OpenAI.Chat;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace LLMClient.Endpoints;

/// <summary>
/// 用于在聊天过程中存储额外的上下文信息
/// </summary>
public class ChatStackContext
{
    public ChatStackContext(AdditionalPropertiesDictionary? additionalObjects = null)
    {
        AdditionalObjects = additionalObjects ?? new AdditionalPropertiesDictionary();
    }

    public bool Streaming { get; set; }

    public bool ShowRequestJson { get; set; }

    public bool AutoApproveAllInvocations { get; set; }

    public AdditionalPropertiesDictionary AdditionalObjects { get; }

    public List<AIContent> AdditionalFunctionCallResult { get; } = new();

    public StringBuilder AdditionalUserMessage { get; } = new();

    /// <summary>
    /// 当前 ReAct 步骤的事件写入器（用于插件请求权限等场景）
    /// </summary>
    public ReactStep? CurrentStep { get; set; }

    public bool EnableSchemaCleaning { get; set; } = true;

    public Dictionary<string, string>? AdditionalHttpHeader { get; set; }

    public ClientResult? ResponseResult { get; set; }

    public static ChatStackContext CreateForRequest(IRequestContext requestContext,
        AdditionalPropertiesDictionary? additionalObjects,
        bool streaming,
        ChatStackContext? parentContext = null)
    {
        return new ChatStackContext(additionalObjects)
        {
            Streaming = streaming,
            ShowRequestJson = requestContext.ShowRequestJson,
            AutoApproveAllInvocations = requestContext.AutoApproveAllInvocations ||
                                        parentContext?.AutoApproveAllInvocations == true
        };
    }

    private static readonly PropertyInfo InternalChoicePropertyInfo =
        typeof(StreamingChatCompletionUpdate).GetProperty("InternalChoiceDelta",
            BindingFlags.NonPublic | BindingFlags.Instance)!;

    private static PropertyInfo? _choiceAdditional;

    public void CompleteStreamResponse(StepResult result, ChatResponseUpdate update)
    {
        result.Annotations ??= new List<ChatAnnotation>();
        result.AdditionalProperties ??= new AdditionalPropertiesDictionary();
        var dictionary = result.AdditionalProperties;
        if (update.RawRepresentation is StreamingChatCompletionUpdate rawUpdate)
        {
            var patch = rawUpdate.Patch.ToString();
            var node = (JsonArray?)JsonNode.Parse(patch);
            if (node?.Count > 0)
            {
                foreach (var jsonNode in node)
                {
                    if (jsonNode is JsonObject jsonObject)
                    {
                        foreach (var keyValuePair in jsonObject)
                        {
                            dictionary[keyValuePair.Key] = keyValuePair.Value?.ToString() ?? string.Empty;
                        }
                    }
                }
            }


            var choice = InternalChoicePropertyInfo.GetValue(rawUpdate);
            if (choice != null)
            {
                if (_choiceAdditional == null)
                {
                    _choiceAdditional = choice.GetType().GetProperty("SerializedAdditionalRawData",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    if (_choiceAdditional == null)
                    {
                        return;
                    }
                }

                if (_choiceAdditional.GetValue(choice) is IDictionary<string, BinaryData> choiceValue)
                {
                    if (choiceValue.TryGetValue("reasoning", out var reasoning))
                    {
                        var s = reasoning.ToObjectFromJson<string>();
                        if (!string.IsNullOrEmpty(s))
                        {
                            update.Contents.Add(new TextReasoningContent(s));
                        }
                    }

                    if (choiceValue.TryGetValue("annotations", out var annotations))
                    {
                        var s = annotations?.ToString();
                        if (string.IsNullOrEmpty(s)) return;
                        var jsonNode = JsonNode.Parse(s);
                        if (jsonNode == null)
                        {
                            return;
                        }

                        var tryGetAnnotations = TryGetAnnotations(jsonNode.AsArray());
                        if (result.Annotations == null)
                        {
                            result.Annotations = new List<ChatAnnotation>(tryGetAnnotations);
                        }
                        else
                        {
                            foreach (var chatAnnotation in tryGetAnnotations)
                            {
                                result.Annotations.Add(chatAnnotation);
                            }
                        }
                    }
                }
            }
        }
    }

    public async Task CompleteResponse(ChatResponse response, StepResult result)
    {
        if (this.ResponseResult == null)
        {
            return;
        }

        var jsonNode = await this.ResponseResult.ToJsonNode();
        if (jsonNode == null)
        {
            return;
        }

        result.AdditionalProperties ??= new AdditionalPropertiesDictionary();
        var propertiesDictionary = result.AdditionalProperties;
        var provider = jsonNode["provider"]?.ToString();
        if (provider != null)
        {
            propertiesDictionary["provider"] = provider;
        }

        var responseMessages = response.Messages;
        var choicesNode = jsonNode["choices"];
        int index = 0;
        if (choicesNode is JsonArray choicesArray)
        {
            //往往只有1个choice
            foreach (var choiceNode in choicesArray)
            {
                if (choiceNode is JsonObject choice)
                {
                    var chatMessage = responseMessages[index];
                    var choiceMessage = choice["message"]?.AsObject();
                    if (choiceMessage != null)
                    {
                        string? reasoning = null;
                        if (choiceMessage.ContainsKey("reasoning"))
                        {
                            reasoning = choiceMessage["reasoning"]?.ToString();
                        }
                        else if (choiceMessage.ContainsKey("reasoning_content"))
                        {
                            reasoning = choiceMessage["reasoning_content"]?.ToString();
                        }

                        if (!string.IsNullOrEmpty(reasoning) && !HasReasoningContent(chatMessage, reasoning))
                        {
                            chatMessage.Contents.Insert(0, new TextReasoningContent(reasoning));
                        }

                        if (index == 0)
                        {
                            var annotationsArray = choiceMessage["annotations"]?.AsArray();
                            if (annotationsArray != null)
                            {
                                var annotations = TryGetAnnotations(annotationsArray);
                                if (result.Annotations == null)
                                {
                                    result.Annotations = new List<ChatAnnotation>(annotations);
                                }
                                else
                                {
                                    foreach (var chatAnnotation in annotations)
                                    {
                                        result.Annotations.Add(chatAnnotation);
                                    }
                                }
                            }
                        }
                    }
                }

                index++;
            }
        }
    }

    private IEnumerable<ChatAnnotation> TryGetAnnotations(JsonArray annotationsArray)
    {
        foreach (var annotation in annotationsArray)
        {
            if (annotation is JsonObject annotationObject)
            {
                var annotationJson = annotationObject.ToJsonString();
                var annotationObj = JsonSerializer.Deserialize<ChatAnnotation>(annotationJson);
                if (annotationObj != null)
                {
                    yield return annotationObj;
                }
            }
        }
    }

    private static bool HasReasoningContent(ChatMessage chatMessage, string reasoning)
    {
        foreach (var content in chatMessage.Contents)
        {
            if (content is TextReasoningContent reasoningContent &&
                string.Equals(reasoningContent.Text, reasoning, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}