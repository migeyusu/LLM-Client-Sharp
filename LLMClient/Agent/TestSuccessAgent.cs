using System.ComponentModel;
using System.Runtime.CompilerServices;
using LLMClient.Abstraction;
using LLMClient.Dialog.Models;
using Microsoft.Extensions.AI;

namespace LLMClient.Agent;

[Description("Test Agent")]
public class TestSuccessAgent : IAgent
{
    public string Name { get; } = "TestSuccessAgent";

    public async IAsyncEnumerable<ReactStep> Execute(IDialogSession dialogSession,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        for (int i = 0; i < 5; i++)
        {
            if (cancellationToken.IsCancellationRequested) break;

            await Task.Delay(1000, cancellationToken);

            var step = new ReactStep();
            var text = $"Result from step {i + 1}";
            step.EmitText(text);
            step.Complete(new StepResult
            {
                Messages = [new ChatMessage(ChatRole.Assistant, text)],
                IsCompleted = true
            });
            yield return step;
        }
    }
}

[Description("Test Failed Agent")]
public class TestFailedAgent: IAgent
{
    public string Name { get; }="TestFailedAgent";

    public async IAsyncEnumerable<ReactStep> Execute(IDialogSession dialogSession,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Delay(1000, cancellationToken);

        var step1 = new ReactStep();
        step1.EmitText("Step 1 complete.");
        step1.Complete(new StepResult
        {
            Messages = [new ChatMessage(ChatRole.Assistant, "Step 1 complete.")],
            IsCompleted = false
        });
        yield return step1;

        await Task.Delay(1000, cancellationToken);
        await Task.Delay(1500, cancellationToken);
        
        // Simulate a failure
        throw new InvalidOperationException("Something went terribly wrong processing step 2!");
    }
}