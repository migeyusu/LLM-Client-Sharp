using System.ClientModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using LLMClient.Endpoints.Messages;
using Microsoft.Extensions.AI;

namespace LLMClient.Endpoints.OpenAIAPI;

public class ClientContext
{
    public ClientContext(AdditionalPropertiesDictionary? additionalObjects = null)
    {
        AdditionalObjects = additionalObjects ?? new AdditionalPropertiesDictionary();
    }

    public bool Streaming { get; set; }

    public AdditionalPropertiesDictionary AdditionalObjects { get; }

    public ClientResult? Result { get; set; }

    public async Task CompleteStreamResponse(CompletedResult result)
    {
        
    }
    
    public async Task CompleteResponse(ChatResponse response, CompletedResult result)
    {
        if (this.Result == null)
        {
            return;
        }

        var rawResponse = this.Result.GetRawResponse();
        var stream = rawResponse.ContentStream;
        if (stream == null)
        {
            return;
        }

        if (stream.Length == 0)
        {
            return;
        }

        stream.Position = 0;

        var jsonNode = await JsonNode.ParseAsync(stream);
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
                            var annotations = message["annotations"]?.AsArray();
                            if (annotations != null)
                            {
                                result.Annotations ??= new List<ChatAnnotation>();
                                foreach (var annotation in annotations)
                                {
                                    if (annotation is JsonObject annotationObject)
                                    {
                                        var annotationJson = annotationObject.ToJsonString();
                                        var annotationObj = JsonSerializer.Deserialize<ChatAnnotation>(annotationJson);
                                        if (annotationObj != null)
                                        {
                                            result.Annotations.Add(annotationObj);
                                        }
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
}