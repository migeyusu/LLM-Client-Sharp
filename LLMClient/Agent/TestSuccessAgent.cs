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
        IInvokeInteractor? interactor = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        for (int i = 0; i < 5; i++)
        {
            if (cancellationToken.IsCancellationRequested) break;

            interactor?.Info($"Running step {i + 1}/5...");
            await Task.Delay(1000, cancellationToken);

            interactor?.Write($"Step {i + 1} processing...");
            await Task.Delay(500, cancellationToken);

            interactor?.WriteLine($"Done.");

            if (i == 2 && interactor != null)
            {
                var allowed = await interactor.WaitForPermission("Confirmation", "Should I continue to step 3?");
                if (!allowed)
                {
                    interactor.Error("User denied permission.");
                    yield break;
                }
            }

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
        IInvokeInteractor? interactor = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        interactor?.Info("Starting risky operation...");
        await Task.Delay(1000, cancellationToken);
        
        interactor?.Write("Processing step 1...");
        yield return new ChatCallResult
    {
            Messages = new[] { new ChatMessage(ChatRole.Assistant, "Step 1 complete.") }
        };
        
        await Task.Delay(1000, cancellationToken);
        interactor?.WriteLine("Done.");

        interactor?.Info("Processing step 2 (This will fail)...");
        await Task.Delay(1500, cancellationToken);
        
        // Simulate a failure
        try
        {
            throw new InvalidOperationException("Something went terribly wrong processing step 2!");
        }
        catch (Exception ex)
        {
            interactor?.Error($"Agent failed: {ex.Message}");
            throw; // Re-throw to signal failure to the caller
        }
    }
}