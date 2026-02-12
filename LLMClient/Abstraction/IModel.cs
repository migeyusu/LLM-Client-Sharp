namespace LLMClient.Abstraction;

public interface IModel : IModelParams
{
    string? SeriesName { get; }

    string? Provider { get; }

    string? OfficialName
    {
        get
        {
            if (string.IsNullOrEmpty(SeriesName) || string.IsNullOrEmpty(Provider))
            {
                return null;
            }

            return $"{Provider}-{SeriesName}";
        }
    }
}