using LLMClient.Component.CustomControl;
using LLMClient.Component.Utility;
using LLMClient.Endpoints;
using LLMClient.ToolCall;

namespace LLMClient.Dialog;

internal static class InvokePermissionDialog
{
    public static Task<bool> RequestAsync(string title, string message)
    {
        if (IsAutoApproveEnabled())
        {
            return Task.FromResult(true);
        }

        return Task.FromResult(MessageBoxes.Question(message, title));
    }

    public static Task<bool> RequestAsync(object content)
    {
        if (IsAutoApproveEnabled())
        {
            return Task.FromResult(true);
        }

        return Task.FromResult(MessageBoxes.Question(content, GetTitle(content)));
    }

    private static bool IsAutoApproveEnabled()
    {
        return AsyncContextStore<ChatContext>.Current?.AutoApproveAllInvocations == true;
    }

    private static string GetTitle(object content)
    {
        return content switch
        {
            ToolCallRequestViewModel => "工具调用确认",
            FileEditRequestViewModel fileEdit => fileEdit.Title,
            _ => "执行确认"
        };
    }
}

