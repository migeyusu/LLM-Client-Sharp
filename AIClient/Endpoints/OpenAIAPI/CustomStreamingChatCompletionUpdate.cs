using System.ClientModel;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using LLMClient.Endpoints.Messages;
using OpenAI;
using OpenAI.Chat;


public class RawCapturePipelinePolicy : PipelinePolicy
{
    private readonly Func<PipelineMessage, CancellationToken, Task> _onResponse;
    
    public RawCapturePipelinePolicy(Func<PipelineMessage, CancellationToken, Task> onResponse)
    {
        _onResponse = onResponse;
    }

    public override void Process(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        throw new NotImplementedException();
    }

    public override async ValueTask ProcessAsync(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        await pipeline[currentIndex + 1].ProcessAsync(message, pipeline, currentIndex + 1);
        
        if (message.Response?.ContentStream != null)
        {
            await _onResponse(message, CancellationToken.None);
        }
    }
}

public class StreamingChatClientWithRawCapture : IDisposable
{
    private readonly ChatClient _client;
    private readonly RawCapturePipelinePolicy _capturePolicy;
    
    public StreamingChatClientWithRawCapture(string model, string apiKey)
    {
        var options = new OpenAIClientOptions();
        _capturePolicy = new RawCapturePipelinePolicy(OnResponseCaptured);
        
        options.AddPolicy(_capturePolicy, PipelinePosition.PerCall);
        _client = new ChatClient(model, new ApiKeyCredential(apiKey), options);
    }
    
    private async Task OnResponseCaptured(PipelineMessage message, CancellationToken cancellationToken)
    {
        var stream = message.Response.ContentStream;
        if (stream != null && IsStreamingResponse(message))
        {
            // 创建可重读的流
            var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, cancellationToken);
            buffer.Position = 0;
            
            // 替换原始流，保持流式特性
            message.Response.ContentStream = buffer;
            
            // 异步解析原始数据
            _ = Task.Run(() => ParseRawStreamAsync(buffer));
        }
    }
    
    private bool IsStreamingResponse(PipelineMessage message)
    {
        return true;
        // return message.Response?.Headers?.ContentType?.Contains("text/event-stream") == true;
    }
    
    public async Task ParseRawStreamAsync(Stream stream)
    {
        stream.Position = 0;
        using var reader = new StreamReader(stream);
        
        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync();
            if (line?.StartsWith("data: ") == true)
            {
                var jsonData = line.Substring(6);
                if (jsonData != "[DONE]")
                {
                    Debugger.Break();
                }
            }
        }
    }
    
    public AsyncCollectionResult<StreamingChatCompletionUpdate> CompleteChatStreamingAsync(
        IEnumerable<ChatMessage> messages, 
        ChatCompletionOptions options = null)
    {
        return _client.CompleteChatStreamingAsync(messages, options);
    }

    public void Dispose()
    {
        // TODO 在此释放托管资源
    }
}