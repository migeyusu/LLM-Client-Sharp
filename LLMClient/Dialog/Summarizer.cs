using System.Diagnostics;
using LLMClient.Abstraction;
using LLMClient.Agent;
using LLMClient.Configuration;
using LLMClient.UI.Component;

namespace LLMClient.Dialog;

public readonly struct Summarizer
{
    private readonly GlobalOptions _options;

    public Summarizer(GlobalOptions options)
    {
        _options = options;
    }

    public async Task<string?> SummarizeTopicAsync(DialogSessionViewModel dialog, int retryCount = 3)
    {
        var client = _options.CreateSubjectSummarizeClient();
        if (client == null)
        {
            return null;
        }

        try
        {
            var dialogItems = new List<IDialogItem>(3);
            dialogItems.AddRange(dialog.DialogItems);
            dialogItems.Add(new RequestViewItem(){TextMessage = _options.SubjectSummarizePrompt});
            var dialogContext = new DialogContext(dialogItems);
            var sendRequestAsync = await new PromptAgent(client, new TraceInvokeInteractor()).SendRequestAsync(dialogContext);
            return sendRequestAsync.FirstTextResponse;
        }
        catch (Exception e)
        {
            Trace.TraceError("生成话题摘要失败：" + e);
            return null;
        }
    }

    public string SummarizeContent()
    {
        throw new NotImplementedException();
    }
}