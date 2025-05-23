﻿using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media;
using LLMClient.Endpoints;
using LLMClient.UI;

namespace LLMClient.Abstraction;

public interface ILLMModelClient
{
    /// <summary>
    /// 唯一名称
    /// </summary>
    string Name { get; }

    ILLMEndpoint Endpoint { get; }

    ILLMModel Info { get; }

    bool IsResponding { get; set; }

    IModelParams Parameters { get; set; }

    ObservableCollection<string> PreResponse { get; }
    
    Task<CompletedResult> SendRequest(IEnumerable<IDialogViewItem> dialogItems,
        CancellationToken cancellationToken = default);
}