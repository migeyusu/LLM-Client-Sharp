using System.Net.Http;

namespace LLMClient.ToolCall;

/// <summary>
/// 强制缓冲请求体，从而生成 Content-Length 头并禁用 Transfer-Encoding: chunked
/// 专门用于解决某些不支持 Chunked 传输的 MCP 服务器（如 JetBrains Rider）
/// </summary>
public class CustomHttpHandler : DelegatingHandler
{
    public bool BufferedRequest { get; set; } = false;

    public bool RemoveCharSet { get; set; } = false;

    public CustomHttpHandler(HttpMessageHandler innerHandler) : base(innerHandler)
    {
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (request.Content != null)
        {
            // 核心逻辑：强制将请求内容加载到内存缓冲区
            // 这会自动计算 Content-Length 并在发送时移除 Transfer-Encoding: chunked
            await request.Content.LoadIntoBufferAsync();

            // 显式确保 Chunked 属性为 null 或 false (虽然 LoadIntoBufferAsync 通常会自动处理)
            request.Headers.TransferEncodingChunked = false;
            var contentType = request.Content.Headers.ContentType;
            if (contentType != null && contentType.MediaType == "application/json")
            {
                // 将 CharSet 设为 null，.NET 就不会发送 "; charset=utf-8"
                contentType.CharSet = null;
            }
        }

        return await base.SendAsync(request, cancellationToken);
    }
}