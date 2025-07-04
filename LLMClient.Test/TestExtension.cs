using ModelContextProtocol.Protocol;

namespace LLMClient.Test;

public static class TestExtension
{
    public static string GetTextContent(this CallToolResult result)
    {
        var contentBlock = result.Content.FirstOrDefault((block => block.Type == "text"));
        if (contentBlock is TextContentBlock textBlock)
        {
            return textBlock.Text;
        }

        return string.Empty;
    }
}