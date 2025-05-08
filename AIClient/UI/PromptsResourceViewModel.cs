using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Xaml.Behaviors.Core;

namespace LLMClient.UI;

public class PromptsResourceViewModel : BaseViewModel, IPromptsResource
{
    private ObservableCollection<string>? _promptsSystem;
    private const string PromptsFileName = "system_prompts.json";

    public ObservableCollection<string>? PromptsSystem
    {
        get => _promptsSystem;
        set
        {
            if (Equals(value, _promptsSystem)) return;
            _promptsSystem = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(AddCommand));
            OnPropertyChanged(nameof(RemoveCommand));
            OnPropertyChanged(nameof(SaveCommand));
            OnPropertyChanged(nameof(SystemPrompts));
        }
    }

    public ICommand AddCommand => new ActionCommand((o =>
    {
        var textBox = o as TextBox;
        if (textBox == null)
            return;
        var value = textBox.Text;
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        if (PromptsSystem == null)
        {
            PromptsSystem = new ObservableCollection<string>();
        }
        
        PromptsSystem.Add(value);
        textBox.Clear();
    }));

    public ICommand RemoveCommand => new ActionCommand((o =>
    {
        if (o is int index && index >= 0)
        {
            this.PromptsSystem?.RemoveAt(index);
        }
    }));

    public ICommand SaveCommand => new ActionCommand((o =>
    {
        if (this.PromptsSystem == null || !this.PromptsSystem.Any())
        {
            return;
        }

        var fileInfo = new FileInfo(PromptsFileName);
        fileInfo.Directory?.Create();
        using (var fileStream = fileInfo.Open(FileMode.Create, FileAccess.Write))
        {
            JsonSerializer.SerializeAsync<IList<string>>(fileStream, this.PromptsSystem);
        }
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
                    PromptsSystem = new ObservableCollection<string>(deserialize);
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
        get
        {
            if (PromptsSystem != null)
            {
                return PromptsSystem;
            }

            return ArraySegment<string>.Empty;
        }
    }

    public IReadOnlyList<string> UserPrompts
    {
        get { return ArraySegment<string>.Empty; }
    }
}