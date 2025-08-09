namespace LLMClient.Abstraction;

public interface IRagSource
{
    string Name { get; set; }

    Guid Id { get; }
}

public interface IRagSourceCollection : IReadOnlyCollection<IRagSource>
{
    Task LoadAsync();
}