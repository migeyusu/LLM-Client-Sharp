using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows.Input;
using LLMClient.Component.Utility;
using LLMClient.Component.ViewModel.Base;
using LLMClient.Data;
using Microsoft.Xaml.Behaviors.Core;

namespace LLMClient.Configuration;

public class PromptsResourceViewModel : BaseViewModel, IPromptsResource
{
    private ObservableCollection<PromptEntry> _prompts = [];

    private PromptEntry? _selectedPrompt;

    public const string PromptsFileName = "system_prompts.json";

    public ObservableCollection<PromptEntry> Prompts
    {
        get => _prompts;
        set
        {
            if (Equals(value, _prompts)) return;
            _prompts = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(NewPromptCommand));
            OnPropertyChanged(nameof(RemoveCommand));
            OnPropertyChanged(nameof(SaveCommand));
            OnPropertyChanged(nameof(SystemPrompts));
        }
    }

    public PromptEntry? SelectedPrompt
    {
        get => _selectedPrompt;
        set
        {
            if (value == _selectedPrompt) return;
            _selectedPrompt = value;
            OnPropertyChanged();
        }
    }

    public ICommand NewPromptCommand => new ActionCommand(o =>
    {
        Prompts.Add(new PromptEntry() { Prompt = "您是一个AI助手，帮助用户解决问题。" });
    });

    public ICommand RemoveCommand => new ActionCommand((o =>
    {
        if (SelectedPrompt != null)
        {
            this.Prompts.Remove(SelectedPrompt);
        }
    }));

    public ICommand SaveCommand => new ActionCommand((async o =>
    {
        var fileInfo = new FileInfo(PromptsFileName);
        fileInfo.Directory?.Create();
        if (!this.Prompts.Any())
        {
            if (fileInfo.Exists)
            {
                fileInfo.Delete();
            }
        }
        else
        {
            await this.Prompts.SaveJsonToFileAsync(PromptsFileName, Extension.DefaultJsonSerializerOptions);
        }

        MessageEventBus.Publish("已保存系统提示词");
    }));

    private ReadOnlyObservableCollection<PromptEntry> _readOnlyPrompts;

    public PromptsResourceViewModel()
    {
        _readOnlyPrompts = new ReadOnlyObservableCollection<PromptEntry>(Prompts);
    }

    public async Task Initialize()
    {
        var fileInfo = new FileInfo(PromptsFileName);
        if (!fileInfo.Exists)
        {
            return;
        }

        try
        {
            Prompts.Clear();
            await using (var fileStream = fileInfo.OpenRead())
            {
                var deserialize = JsonSerializer.Deserialize<IList<PromptEntry>>(fileStream,
                    Extension.DefaultJsonSerializerOptions);
                if (deserialize != null)
                {
                    foreach (var promptEntry in deserialize)
                    {
                        Prompts.Add(promptEntry);
                    }
                }
            }
        }
        catch (Exception e)
        {
            Trace.TraceError(e.ToString());
        }
    }

    public IReadOnlyList<PromptEntry> SystemPrompts
    {
        get { return _readOnlyPrompts; }
    }
}