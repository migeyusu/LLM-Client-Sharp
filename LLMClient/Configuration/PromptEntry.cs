using LLMClient.Component.ViewModel.Base;

namespace LLMClient.Configuration;

public class PromptEntry : BaseViewModel
{
    private string? _title;
    private string _prompt = string.Empty;
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Prompt
    {
        get => _prompt;
        set
        {
            if (value == _prompt) return;
            _prompt = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Title));
        }
    }

    public string? Title
    {
        get
        {
            if (string.IsNullOrEmpty(_title))
            {
                return string.IsNullOrEmpty(Prompt) ? null : Prompt.Substring(0, int.Min(Prompt.Length, 20));
            }

            return _title;
        }
        set
        {
            if (value != null && value == _title) return;
            _title = value;
            OnPropertyChanged();
        }
    }

    protected bool Equals(PromptEntry other)
    {
        if (ReferenceEquals(this, other)) return true;
        return Id.Equals(other.Id);
    }

    public override bool Equals(object? obj)
    {
        if (obj is PromptEntry other) return Equals(other);
        return false;
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }
}