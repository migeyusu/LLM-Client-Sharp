using System.ClientModel;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using LLMClient.Endpoints.Messages;
using Microsoft.Extensions.AI;
using OpenAI.Chat;

namespace LLMClient.Endpoints;

/// <summary>
/// 用于在聊天过程中存储额外的上下文信息
/// </summary>
public class ChatContext
{
    public ChatContext(AdditionalPropertiesDictionary? additionalObjects = null)
    {
        AdditionalObjects = additionalObjects ?? new AdditionalPropertiesDictionary();
    }

    public bool Streaming { get; set; }

    public AdditionalPropertiesDictionary AdditionalObjects { get; }

    public List<AIContent> AdditionalFunctionCallResult { get; } = new List<AIContent>();

    public StringBuilder AdditionalUserMessage { get; } = new StringBuilder();

    public ClientResult? Result { get; set; }

    private static readonly PropertyInfo AdditionalRawDataPropertyInfo =
        typeof(StreamingChatCompletionUpdate).GetProperty("SerializedAdditionalRawData",
            BindingFlags.NonPublic | BindingFlags.Instance)!;

    private static readonly PropertyInfo InternalChoicePropertyInfo =
        typeof(StreamingChatCompletionUpdate).GetProperty("InternalChoiceDelta",
            BindingFlags.NonPublic | BindingFlags.Instance)!;

    private static PropertyInfo? ChoiceAdditional;

    public void CompleteStreamResponse(CompletedResult result, ChatResponseUpdate update)
    {
        result.AdditionalProperties ??= new AdditionalPropertiesDictionary();
        var dictionary = result.AdditionalProperties;
        if (update.RawRepresentation is StreamingChatCompletionUpdate rawUpdate)
        {
            if (AdditionalRawDataPropertyInfo.GetValue(rawUpdate) is IDictionary<string, BinaryData> value)
            {
                foreach (var kv in value)
                {
                    dictionary[kv.Key] = kv.Value.ToString();
                }
            }

            var choice = InternalChoicePropertyInfo.GetValue(rawUpdate);
            if (choice != null)
            {
                if (ChoiceAdditional == null)
                {
                    ChoiceAdditional = choice.GetType().GetProperty("SerializedAdditionalRawData",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    if (ChoiceAdditional == null)
                    {
                        return;
                    }
                }

                if (ChoiceAdditional.GetValue(choice) is IDictionary<string, BinaryData> choiceValue)
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

    public async Task CompleteResponse(ChatResponse response, CompletedResult result)
    {
        if (this.Result == null)
        {
            return;
        }

        var jsonNode = await this.Result.ToJsonNode();
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
            foreach (var choice in choicesArray)
            {
                if (choice is JsonObject choiceObject)
                {
                    /*var index = choiceObject["index"]?.GetValue<int>();
                    if (index == null)
                        continue;*/
                    var chatMessage = responseMessages[index];
                    var message = choiceObject["message"]?.AsObject();
                    if (message != null)
                    {
                        string? reasoning = null;
                        if (message.ContainsKey("reasoning"))
                        {
                            reasoning = message["reasoning"]?.ToString();
                        }
                        else if (message.ContainsKey("reasoning_content"))
                        {
                            reasoning = message["reasoning_content"]?.ToString();
                        }

                        if (!string.IsNullOrEmpty(reasoning))
                        {
                            chatMessage.Contents.Insert(0, new TextReasoningContent(reasoning));
                        }

                        if (index == 0)
                        {
                            var annotationsArray = message["annotations"]?.AsArray();
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
}