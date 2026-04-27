using LLMClient.Dialog.Models;

namespace LLMClient.Agent;

public class AgentRunOption
{
    public ReactStepConsumeMode ReactStepConsumeMode { get; set; } = ReactStepConsumeMode.Reset;

    public static AgentRunOption Default => new AgentRunOption
    {
        ReactStepConsumeMode = ReactStepConsumeMode.Reset
    };
}