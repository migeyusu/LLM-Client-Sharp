using System.Collections.ObjectModel;

namespace LLMClient.Abstraction;

public interface IRagSourceCollection
{
    ObservableCollection<IRagSource> Sources { get; }

    Task LoadAsync();

    bool IsRunning { get; }
}