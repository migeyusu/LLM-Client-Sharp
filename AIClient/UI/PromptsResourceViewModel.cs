using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows.Controls;
using System.Windows.Input;
using LLMClient.Data;
using LLMClient.UI.Component;
using Microsoft.Xaml.Behaviors.Core;

namespace LLMClient.UI;

public class PromptsResourceViewModel : BaseViewModel, IPromptsResource
{
    private ObservableCollection<string> _prompts = new ObservableCollection<string>();
    private int _selectedIndex = -1;
    private string? _selectedPrompt;
    private const string PromptsFileName = "system_prompts.json";

    public ObservableCollection<string> Prompts
    {
        get => _prompts;
        set
        {
            if (Equals(value, _prompts)) return;
            _prompts = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(AddCommand));
            OnPropertyChanged(nameof(RemoveCommand));
            OnPropertyChanged(nameof(SaveCommand));
            OnPropertyChanged(nameof(SystemPrompts));
            OnPropertyChanged(nameof(UpdateCommand));
        }
    }

    public string? SelectedPrompt
    {
        get => _selectedPrompt;
        set
        {
            if (value == _selectedPrompt) return;
            _selectedPrompt = value;
            OnPropertyChanged();
        }
    }

    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            if (value == _selectedIndex) return;
            _selectedIndex = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(UpdateCommand));
        }
    }

    public ICommand UpdateCommand => new ActionCommand((o =>
    {
        if (SelectedIndex < 0)
        {
            return;
        }

        if (o is TextBox textBox)
        {
            Prompts[SelectedIndex] = textBox.Text;
        }
    }));

    public ICommand AddCommand => new ActionCommand((o =>
    {
        Prompts.Add("您是一个AI助手，帮助用户解决问题。");
        SelectedIndex = Prompts.Count - 1;
    }));

    public ICommand RemoveCommand => new ActionCommand((o =>
    {
        if (SelectedIndex >= 0)
        {
            this.Prompts.RemoveAt(SelectedIndex);
        }
    }));

    public ICommand SaveCommand => new ActionCommand((o =>
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
            using (var fileStream = fileInfo.Open(FileMode.Create, FileAccess.Write))
            {
                JsonSerializer.SerializeAsync<IList<string>>(fileStream, this.Prompts);
            }
        }

        MessageEventBus.Publish("已保存系统提示词");
    }));

    public async Task Initialize()
    {
        var fileInfo = new FileInfo(PromptsFileName);
        if (!fileInfo.Exists)
        {
            return;
        }

        try
        {
            await using (var fileStream = fileInfo.OpenRead())
            {
                var deserialize = JsonSerializer.Deserialize<IList<string>>(fileStream);
                if (deserialize != null)
                {
                    Prompts = new ObservableCollection<string>(deserialize);
                }
            }
        }
        catch (Exception e)
        {
            Trace.TraceError(e.ToString());
        }
    }

    public IReadOnlyList<string> SystemPrompts
    {
        get { return Prompts; }
    }

    public IReadOnlyList<string> UserPrompts
    {
        get { return ArraySegment<string>.Empty; }
    }
}