using System.ComponentModel;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Microsoft.SemanticKernel;
using ReverseMarkdown;

namespace LLMClient.ToolCall.Servers;

/// <summary>
/// A Kernel Plugin for fetching data from web pages.
/// Provides functions for getting raw HTML, clean text, Markdown, or JSON from a given URL.
/// Includes security measures to prevent access to private network resources.
/// </summary>
public class UrlFetcherPlugin : KernelFunctionGroup
{
    // 遵循最佳实践，使用静态HttpClient实例以避免套接字耗尽。
    // 在实际应用中，更推荐通过依赖注入来管理HttpClient的生命周期。
    private static readonly HttpClient HttpClient = new();

    public UrlFetcherPlugin() : base("UrlFetcher")
    {
        // 设置一个通用的浏览器User-Agent，以提高兼容性
        HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
    }

    public override string? AdditionPrompt => null;

    public override object Clone()
    {
        return new UrlFetcherPlugin();
    }

    [KernelFunction,
     Description(
         "Fetches the raw HTML content of a given public URL. This is useful when you need the full, unprocessed HTML source code.")]
    public async Task<string> FetchHtmlAsync(
        [Description("The public URL to fetch the content from.")]
        string url,
        [Description("Optional: The maximum number of characters to return. Default is 8000.")]
        int? maxLength = 8000,
        [Description("Optional: The starting character index for the content. Default is 0.")]
        int? startIndex = 0)
    {
        var responseContent = await FetchContentAsync(url);
        return ApplyLengthLimits(responseContent, maxLength, startIndex);
    }

    [KernelFunction,
     Description(
         "Fetches content from a URL, assumes it is JSON, and returns it as a string. Useful for interacting with APIs.")]
    public async Task<string> FetchJsonAsync(
        [Description("The public URL of the JSON endpoint to fetch.")]
        string url,
        [Description("Optional: The maximum number of characters to return. Default is 8000.")]
        int? maxLength = 8000,
        [Description("Optional: The starting character index for the content. Default is 0.")]
        int? startIndex = 0)
    {
        // 对于LLM，直接返回JSON字符串通常就足够了。我们仅验证内容，不在这里进行反序列化。
        var responseContent = await FetchContentAsync(url, "application/json");
        return ApplyLengthLimits(responseContent, maxLength, startIndex);
    }

    [KernelFunction,
     Description(
         "Fetches a web page, strips all scripting and styling, and returns the clean, human-readable text content. Ideal for summarizing articles or getting the main content of a page.")]
    public async Task<string> FetchTextAsync(
        [Description("The public URL to fetch and clean.")]
        string url,
        [Description("Optional: The maximum number of characters to return. Default is 8000.")]
        int? maxLength = 8000,
        [Description("Optional: The starting character index for the content. Default is 0.")]
        int? startIndex = 0)
    {
        var htmlContent = await FetchContentAsync(url);

        var doc = new HtmlDocument();
        doc.LoadHtml(htmlContent);

        // 移除所有 script 和 style 节点
        doc.DocumentNode.Descendants()
            .Where(n => n.Name == "script" || n.Name == "style")
            .ToList()
            .ForEach(n => n.Remove());

        // 获取body文本，如果body不存在则获取全文
        var bodyNode = doc.DocumentNode.SelectSingleNode("//body") ?? doc.DocumentNode;
        var text = bodyNode.InnerText;

        // 使用Regex进行规范化，将多个空白符合并为一个空格
        var normalizedText = Regex.Replace(text, @"\s+", " ").Trim();

        return ApplyLengthLimits(normalizedText, maxLength, startIndex);
    }

    [KernelFunction,
     Description(
         "Fetches a web page and converts its main content to Markdown format. This preserves structure like headings, lists, and links, which is great for structured data extraction.")]
    public async Task<string> FetchMarkdownAsync(
        [Description("The public URL to fetch and convert to Markdown.")]
        string url,
        [Description("Optional: The maximum number of characters to return. Default is 8000.")]
        int? maxLength = 8000,
        [Description("Optional: The starting character index for the content. Default is 0.")]
        int? startIndex = 0)
    {
        var htmlContent = await FetchContentAsync(url);
        // 使用ReverseMarkdown进行转换
        var converter = new Converter();
        var markdown = converter.Convert(htmlContent);

        return ApplyLengthLimits(markdown, maxLength, startIndex);
    }

    #region Private Helpers

    /// <summary>
    /// Core method to fetch content from a URL with security checks.
    /// </summary>
    private async Task<string> FetchContentAsync(string url, string acceptHeader = "text/html")
    {
        try
        {
            await ValidateUrlAndPreventSsrFAsync(url);

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Accept.ParseAdd(acceptHeader);

            var response = await HttpClient.SendAsync(request);
            response.EnsureSuccessStatusCode(); // 如果状态码不是 2xx，则抛出异常

            return await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            // 包装异常，为LLM提供更清晰的错误信息
            throw new InvalidOperationException($"Failed to fetch content from {url}. Error: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Validates the URL and ensures it does not point to a private IP address.
    /// This is a crucial security measure to prevent SSRF attacks.
    /// </summary>
    private async Task ValidateUrlAndPreventSsrFAsync(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            throw new ArgumentException("Invalid URL format.", nameof(url));
        }

        // 禁止非HTTP/HTTPS协议
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException("Only HTTP and HTTPS protocols are allowed.", nameof(url));
        }

        // 如果是IP地址，直接检查
        if (uri.IsLoopback)
        {
            throw new ArgumentException("Fetching from loopback addresses is forbidden.");
        }

        IPAddress[] addresses;
        try
        {
            addresses = await Dns.GetHostAddressesAsync(uri.DnsSafeHost);
        }
        catch (System.Net.Sockets.SocketException)
        {
            // 如果域名无法解析，也认为这是一个错误
            throw new ArgumentException($"Could not resolve the domain name: {uri.DnsSafeHost}");
        }

        if (addresses.Length == 0)
        {
            throw new ArgumentException($"Could not resolve the domain name: {uri.DnsSafeHost}");
        }

        foreach (var ipAddress in addresses)
        {
            if (IsPrivateIpAddress(ipAddress))
            {
                throw new ArgumentException(
                    $"Fetching from private IP addresses ({ipAddress}) is forbidden to prevent security vulnerabilities.");
            }
        }
    }

    /// <summary>
    /// Checks if an IP address is in a private range (RFC 1918) or is a loopback address.
    /// </summary>
    private bool IsPrivateIpAddress(IPAddress ipAddress)
    {
        if (IPAddress.IsLoopback(ipAddress)) return true;

        var bytes = ipAddress.GetAddressBytes();
        switch (ipAddress.AddressFamily)
        {
            case System.Net.Sockets.AddressFamily.InterNetwork: // IPv4
                // 10.0.0.0/8
                if (bytes[0] == 10) return true;
                // 172.16.0.0/12
                if (bytes[0] == 172 && (bytes[1] >= 16 && bytes[1] <= 31)) return true;
                // 192.168.0.0/16
                if (bytes[0] == 192 && bytes[1] == 168) return true;
                break;

            case System.Net.Sockets.AddressFamily.InterNetworkV6: // IPv6
                // fc00::/7 - Unique Local Unicast
                if ((bytes[0] & 0xFE) == 0xFC) return true;
                break;
        }

        return false;
    }

    /// <summary>
    /// Applies length and start index limits to a string, similar to the TypeScript implementation.
    /// </summary>
    private string ApplyLengthLimits(string text, int? maxLength, int? startIndex)
    {
        var start = startIndex.GetValueOrDefault(0);
        if (start >= text.Length)
        {
            return string.Empty;
        }

        var length = maxLength.GetValueOrDefault(8000);
        if (length <= 0)
        {
            return text.Substring(start);
        }

        return text.Substring(start, Math.Min(length, text.Length - start));
    }

    #endregion
}