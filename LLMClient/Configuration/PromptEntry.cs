using LLMClient.Component.ViewModel.Base;

namespace LLMClient.Configuration;

public class PromptEntry : BaseViewModel
{
    private string? _title;
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Prompt { get; set; } = string.Empty;

    public string? Title
    {
        get
        {
            if (string.IsNullOrEmpty(_title))
            {
                return string.IsNullOrEmpty(Prompt) ? null : Prompt;
            }

            return _title;
        }
        set => _title = value;
    }
}