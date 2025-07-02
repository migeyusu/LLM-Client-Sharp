using System.Diagnostics;
using System.IO;
using AutoMapper;
using LLMClient.Abstraction;
using LLMClient.UI.Dialog;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace LLMClient.Obsolete;

/// <summary>
/// 将版本2的对话转换为版本3的对话格式
/// </summary>
public class Version3Converter
{
    private IServiceProvider _service;

    public Version3Converter(IServiceProvider service)
    {
        this._service = service;
    }

    /*public async Task ConvertToVersion3(string folderPath)
    {
        var endpointService = _service.GetService<IEndpointService>();
        await endpointService!.Initialize();
        //版本1到2的最大变化是移除了Raw，使之由ResponseMessages动态生成，从而可以适配新的UI效果
        var directoryInfo = new DirectoryInfo(folderPath);
        if (!directoryInfo.Exists)
        {
            return;
        }

        var fileInfos = directoryInfo.GetFiles();
        foreach (var fileInfo in fileInfos)
        {
            try
            {
                DialogViewModel? viewModel = await DialogViewModel.LoadFromFile(fileInfo, 2);
                if (viewModel == null)
                {
                    continue;
                }

                foreach (var dialogItem in viewModel.DialogItems)
                {
                    if (dialogItem is MultiResponseViewItem multiResponseItem)
                    {
                        foreach (var responseViewItem in multiResponseItem.Items.OfType<ResponseViewItem>())
                        {
                            if (responseViewItem.ResponseMessages?.Any() == true)
                            {
                                continue;
                            }

                            var raw = responseViewItem.Raw;
                            if (!string.IsNullOrEmpty(raw))
                            {
                                var chatMessages = new List<ChatMessage>();
                                await foreach (var chatMessage in responseViewItem.GetMessages(CancellationToken
                                                   .None))
                                {
                                    chatMessages.Add(chatMessage);
                                }

                                responseViewItem.ResponseMessages = chatMessages;
                            }

                            responseViewItem.Raw = null;
                        }
                    }
                }

                viewModel.IsDataChanged = true;
                // 保存为新的版本3格式
                await viewModel.SaveToLocal(folderPath);
            }
            catch (Exception e)
            {
                Trace.TraceError($"加载会话{fileInfo.FullName}失败：{e}");
            }
        }
    }*/
}