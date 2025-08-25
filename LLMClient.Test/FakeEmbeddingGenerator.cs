using Microsoft.Extensions.AI;

namespace LLMClient.Test;

sealed class FakeEmbeddingGenerator() : IEmbeddingGenerator<string, Embedding<float>>
{
    private readonly float[] _embeddings = Enumerable.Repeat(1f, 1536).ToArray();

    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var results = new GeneratedEmbeddings<Embedding<float>>();
        var count = values.Count();
        for (int i = 0; i < count; i++)
        {
            results.Add(new Embedding<float>(_embeddings));
        }

        /*foreach (var value in values)
        {
            var vector = value.TrimStart('[').TrimEnd(']').Split(',').Select(s => float.Parse(s.Trim())).ToArray();
            if (replaceLast is not null)
            {
                vector[^1] = replaceLast.Value;
            }
            results.Add(new Embedding<float>(vector));
        }*/
        return Task.FromResult(results);
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
        => null;


    public void Dispose()
    {
    }
}