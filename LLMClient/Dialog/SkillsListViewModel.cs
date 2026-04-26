using System.Text;
using System.Windows.Input;
using LLMClient.Component.Utility;
using LLMClient.Component.ViewModel.Base;
using LLMClient.Dialog.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Xaml.Behaviors.Core;

namespace LLMClient.Dialog;

public class SkillsListViewModel : BaseViewModel
{
    private readonly IDialogSession _session;

    public bool SkillsDetected
    {
        get
        {
            if (_session?.ContextProviders == null)
            {
                return false;
            }

            return _session?.ContextProviders.Length > 0;
        }
    }


    public bool IsEnsuring
    {
        get;
        private set
        {
            if (value == field) return;
            field = value;
            PostOnPropertyChanged();
        }
    } = false;


    public IList<AITool>? Skills
    {
        get;
        set
        {
            if (Equals(value, field)) return;
            field = value;
            PostOnPropertyChanged();
        }
    }

    public string? Instructions
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            PostOnPropertyChanged();
        }
    }

    public ICommand RefreshSourceCommand { get; }

    public ICommand CopySelectedItemCommand { get; }

    public SkillsListViewModel(IDialogSession session)
    {
        _session = session;
        RefreshSourceCommand = new ActionCommand(async (o) => { await RefreshSkills(); });
        CopySelectedItemCommand = new ActionCommand((o =>
        {
            if (Skills == null)
            {
                return;
            }

            var stringBuilder = new StringBuilder();
            foreach (var aiTool in Skills)
            {
                stringBuilder.Append("Name: ").AppendLine(aiTool.Name);
                stringBuilder.Append("Description: ").AppendLine(aiTool.Description);
            }

            CommonCommands.CopyCommand.Execute(stringBuilder.ToString());
        }));
    }

    private bool _isInitialized = false;

    /// <summary>
    /// 重置状态，表示需要重新初始化
    /// </summary>
    public void Reset()
    {
        _isInitialized = false;
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized)
        {
            return;
        }

        await RefreshSkills();
        _isInitialized = true;
    }

    private async Task RefreshSkills()
    {
        if (IsEnsuring)
        {
            return;
        }

        IsEnsuring = true;
        var contextProviders = _session?.ContextProviders;
        if (contextProviders == null)
        {
            IsEnsuring = false;
            return;
        }

        var aiContext = new AIContext();
#pragma warning disable MAAI001
        var agentRunContext = new AIContextProvider.InvokingContext(new MockAIAgent(), null, aiContext);
#pragma warning restore MAAI001
        foreach (var aiContextProvider in contextProviders)
        {
            aiContext = await aiContextProvider.InvokingAsync(agentRunContext);
        }

        this.Skills = aiContext.Tools?.ToArray();
        this.Instructions = aiContext.Instructions;
        IsEnsuring = false;
    }
}