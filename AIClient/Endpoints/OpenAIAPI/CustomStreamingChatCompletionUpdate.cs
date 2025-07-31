using System.ClientModel.Primitives;

public class RawCapturePipelinePolicy : PipelinePolicy
{
    private readonly Func<PipelineMessage, CancellationToken, Task> _onResponse;
    
    public RawCapturePipelinePolicy(Func<PipelineMessage, CancellationToken, Task> onResponse)
    {
        _onResponse = onResponse;
    }

    public override void Process(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        throw new NotImplementedException();
    }

    public override async ValueTask ProcessAsync(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        await pipeline[currentIndex + 1].ProcessAsync(message, pipeline, currentIndex + 1);
        
        if (message.Response?.ContentStream != null)
        {
            await _onResponse(message, CancellationToken.None);
        }
    }
}