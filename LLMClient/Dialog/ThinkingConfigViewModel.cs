using LLMClient.Abstraction;
using LLMClient.Endpoints;
using LLMClient.UI.ViewModel.Base;
using Microsoft.Extensions.AI;
using OpenAI.Chat;

namespace LLMClient.Dialog;

public class ThinkingConfigViewModel : NotifyDataErrorInfoViewModelBase, IThinkingConfig
{
    private bool _enable = false;

    public bool Enable
    {
        get => _enable;
        set
        {
            if (value == _enable) return;
            _enable = value;
            OnPropertyChanged();
        }
    }

    public const string DefaultEffortLevel = "Default";

#pragma warning disable OPENAI001
    public IList<ChatReasoningEffortLevel> EffortLevels { get; } =

    [
        new(DefaultEffortLevel),
        ChatReasoningEffortLevel.Low,
        ChatReasoningEffortLevel.Medium,
        ChatReasoningEffortLevel.High
    ];

    private ChatReasoningEffortLevel _effortLevel;

    public ChatReasoningEffortLevel EffortLevel
    {
        get => _effortLevel;
        set
        {
            if (value.Equals(_effortLevel)) return;
            _effortLevel = value;
            OnPropertyChanged();
        }
    }

#pragma warning restore OPENAI001

    public string? Effort
    {
        get
        {
            var s = EffortLevel.ToString();
            return s == DefaultEffortLevel ? null : s;
        }
        set
        {
            EffortLevel = value == null
                ? new ChatReasoningEffortLevel(DefaultEffortLevel)
                : new ChatReasoningEffortLevel(value);
            OnPropertyChanged();
        }
    }

    private bool _enableBudgetTokens;

    public bool EnableBudgetTokens
    {
        get => _enableBudgetTokens;
        set
        {
            if (value == _enableBudgetTokens) return;
            _enableBudgetTokens = value;
            OnPropertyChanged();
        }
    }

    private int? _budgetTokens;

    public int? BudgetTokens
    {
        get => _budgetTokens;
        set
        {
            if (value == _budgetTokens) return;
            _budgetTokens = value;
            OnPropertyChanged();
            ClearError();
            if (value < 0)
            {
                AddError("Budget Tokens must be greater than or equal to 0.");
            }
        }
    }

    private bool _showThinking = true;
    private IThinkingConfig? _config;

    public bool ShowThinking
    {
        get => _showThinking;
        set
        {
            if (value == _showThinking) return;
            _showThinking = value;
            OnPropertyChanged();
        }
    }

    public void EnableThinking(ChatOptions options)
    {
        throw new NotImplementedException();
    }

    public IThinkingConfig? Config
    {
        get => _config;
        set
        {
            if (Equals(value, _config)) return;
            _config = value;
            OnPropertyChanged();
        }
    }

    public void ResetConfig(ILLMChatClient client)
    {
        this.Config = IThinkingConfig.Get(client.Model);
    }

    public bool Validate()
    {
        if (HasErrors)
        {
            return false;
        }

        if (EnableBudgetTokens && BudgetTokens == null)
        {
            AddError("BudgetTokens cannot be null", nameof(BudgetTokens));
        }

        return HasErrors;
    }

    public object Clone()
    {
        throw new NotSupportedException();
    }
}