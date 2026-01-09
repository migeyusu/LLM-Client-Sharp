using System.ClientModel;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using LLMClient.Endpoints.Messages;
using Microsoft.Extensions.AI;
using OpenAI.Assistants;
using OpenAI.Chat;

namespace LLMClient.Endpoints;

/// <summary>
/// 用于在聊天过程中存储额外的上下文信息
/// </summary>
public class ChatContext
{
    public ChatContext(IInvokeInteractor? interactor = null, AdditionalPropertiesDictionary? additionalObjects = null)
    {
        Interactor = interactor;
        AdditionalObjects = additionalObjects ?? new AdditionalPropertiesDictionary();
    }

    public bool Streaming { get; set; }

    public AdditionalPropertiesDictionary AdditionalObjects { get; }

    public List<AIContent> AdditionalFunctionCallResult { get; } = new List<AIContent>();

    public StringBuilder AdditionalUserMessage { get; } = new StringBuilder();

    public IInvokeInteractor? Interactor { get; }

    public ClientResult? Result { get; set; }

    private static readonly PropertyInfo InternalChoicePropertyInfo =
        typeof(StreamingChatCompletionUpdate).GetProperty("InternalChoiceDelta",
            BindingFlags.NonPublic | BindingFlags.Instance)!;

    private static PropertyInfo? _choiceAdditional;

    public void CompleteStreamResponse(CompletedResult result, ChatResponseUpdate update)
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