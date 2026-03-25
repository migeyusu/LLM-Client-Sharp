using System.ComponentModel;
using System.Runtime.CompilerServices;
using LLMClient.Dialog.Models;
using LLMClient.Endpoints;
using Microsoft.Extensions.AI;

namespace LLMClient.Agent;

[Description("Test Agent")]
public class TestAgent : IAgent
{
    public string Name { get; } = "TestAgent";

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