using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Serialization;
using LLMClient.Abstraction;
using LLMClient.Data;
using LLMClient.Render;
using LLMClient.UI.Component;
using Microsoft.Extensions.AI;

namespace LLMClient.UI.Dialog;

public class ResponseViewItem : BaseViewModel, IResponseViewItem
{
    public ThemedIcon Icon
    {
        get { return Model?.Icon ?? ImageExtensions.APIIcon; }
    }

    public string EndPointName { get; }

    public string ModelName
    {
        get { return Model?.Name ?? string.Empty; }
    }

    public ILLMModel? Model { get; }

    /// <summary>
    /// 是否中断
    /// </summary>
    public bool IsInterrupt { get; }

    public long Tokens { get; }
    public int Latency { get; }

    public int Duration { get; }

    public string? ErrorMessage { get; }

    public double? Price { get; set; }

    private SearchableDocument? _flowDocument = null;

    public SearchableDocument? Document
    {
        get
        {
            if (this.ResponseMessages == null || !this.ResponseMessages.Any())
            {
                return null;
            }

            // 如果已经有了，则直接返回
            if (_flowDocument == null)
            {
                var stringBuilder = new StringBuilder();
                foreach (var message in ResponseMessages)
                {
                    foreach (var content in message.Contents)
                    {
                        switch (content)
                        {
                            case TextContent textContent:
                                stringBuilder.Append(textContent.Text);
                                break;
                            case FunctionCallContent functionCallContent:
                                stringBuilder.AppendLine();
                                stringBuilder.Append(FunctionCallBlockParser.FunctionCallTag);
                                stringBuilder.Append("CallId:");
                                stringBuilder.Append(functionCallContent.CallId);
                                stringBuilder.Append(',');
                                stringBuilder.Append("FunctionName:");
                                stringBuilder.Append(functionCallContent.Name);
                                stringBuilder.Append(',');
                                var arguments = functionCallContent.Arguments;
                                if (arguments != null)
                                {
                                    stringBuilder.Append("Arguments:");
                                    stringBuilder.Append(string.Join(", ",
                                        arguments.Select(kv => $"{kv.Key}={kv.Value}")));
                                }
                                else
                                {
                                    stringBuilder.Append("Arguments: null");
                                }

                                stringBuilder.Append(FunctionCallBlockParser.FunctionCallEndTag);
                                break;
                            case FunctionResultContent functionResultContent:
                                stringBuilder.AppendLine();
                                stringBuilder.Append(FunctionResultBlockParser.FunctionResultTag);
                                stringBuilder.Append("CallId:");
                                stringBuilder.Append(functionResultContent.CallId);
                                stringBuilder.Append(',');
                                var exception = functionResultContent.Exception;
                                if (exception != null)
                                {
                                    stringBuilder.Append(exception.GetType().Name);
                                    stringBuilder.Append(':');
                                    stringBuilder.Append(exception.Message);
                                }
                                else
                                {
                                    var result = functionResultContent.Result?.ToString();
                                    var formatJson = Extension.FormatJson(result);
                                    stringBuilder.Append(formatJson ?? result);
                                }

                                stringBuilder.Append(FunctionResultBlockParser.FunctionResultEndTag);
                                break;
                            default:
                                stringBuilder.Append($"Unknown content type: {content.GetType().FullName}");
                                break;
                        }
                    }
                }

                _flowDocument = new SearchableDocument(stringBuilder.ToString());
            }

            return _flowDocument;
        }
    }

    private string? _textContent = null;

    public string? TextContent
    {
        get
        {
            if (_textContent == null)
            {
                if (ResponseMessages != null && ResponseMessages.Any())
                {
                    var sb = new StringBuilder();
                    foreach (var message in ResponseMessages)
                    {
                        foreach (var messageContent in message.Contents)
                        {
                            if (messageContent is TextContent textContent)
                            {
                                sb.Append(textContent.Text);
                            }
                        }
                    }

                    _textContent = sb.ToString();
                }
                else
                {
                    _textContent = String.Empty;
                }
            }

            return _textContent;
        }
    }

    /// <summary>
    /// response messages 来源于回复，但是为了前向兼容，允许基于raw生成
    /// </summary>
    public IList<ChatMessage>? ResponseMessages { get; set; }

    public ChatFinishReason? FinishReason { get; set; }

    private string? _responseWithoutThinking;

    public string? TextWithoutThinking
    {
        get
        {
            if (_responseWithoutThinking == null)
            {
                var textContent = this.TextContent;
                if (textContent != null)
                {
                    var index = textContent.IndexOf(ThinkingEndTag, StringComparison.Ordinal);
                    _responseWithoutThinking =
                        index > 0 ? textContent[(index + ThinkingEndTag.Length)..].Trim() : textContent;
                }
                else
                {
                    _responseWithoutThinking = string.Empty;
                }
            }

            return _responseWithoutThinking;
        }
    }

    private const string ThinkingEndTag = "</think>";


    public ResponseViewItem(ILLMModel? model, IResponse response, string endPointName)
    {
        Duration = response.Duration;
        Model = model;
        ResponseMessages = response.ResponseMessages;
        Tokens = response.Tokens;
        IsInterrupt = response.IsInterrupt;
        ErrorMessage = response.ErrorMessage;
        EndPointName = endPointName;
        Latency = response.Latency;
        Price = response.Price;
    }

    public async IAsyncEnumerable<ChatMessage> GetMessages([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        if (ResponseMessages != null && ResponseMessages.Any())
        {
            foreach (var chatMessage in ResponseMessages)
            {
                yield return chatMessage;
            }
        }
    }


    public bool IsAvailableInContext
    {
        get { return !IsInterrupt; }
    }
}