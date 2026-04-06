using System.ComponentModel;
using System.Runtime.CompilerServices;
using LLMClient.Dialog.Models;
using LLMClient.Endpoints;
using Microsoft.Extensions.AI;

namespace LLMClient.Agent;

[Description("Test Agent")]
public class TestSuccessAgent : IAgent
{
    public string Name { get; } = "TestSuccessAgent";

    public async IAsyncEnumerable<ChatCallResult> Execute(ITextDialogSession dialogSession,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        for (int i = 0; i < 5; i++)
        {
            if (cancellationToken.IsCancellationRequested) break;

            await Task.Delay(1000, cancellationToken);
            await Task.Delay(500, cancellationToken);

            var message = new ChatMessage(ChatRole.Assistant, $"Result from step {i + 1}");
            yield return new ChatCallResult
            {
                Messages = new[] { message }
            };
        }
    }
}

[Description("Test Failed Agent")]
public class TestFailedAgent: IAgent
{
    public string Name { get; }="TestFailedAgent";

    public async IAsyncEnumerable<ChatCallResult> Execute(ITextDialogSession dialogSession,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Delay(1000, cancellationToken);
        
        yield return new ChatCallResult
        {
            Messages = new[] { new ChatMessage(ChatRole.Assistant, "Step 1 complete.") }
        };
        
        await Task.Delay(1000, cancellationToken);
        await Task.Delay(1500, cancellationToken);
        
        // Simulate a failure
        throw new InvalidOperationException("Something went terribly wrong processing step 2!");
    }
}