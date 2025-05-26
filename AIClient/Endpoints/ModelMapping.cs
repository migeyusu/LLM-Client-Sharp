using System.Net.Http;
using LLMClient.Endpoints.OpenAIAPI;

namespace LLMClient.Endpoints;

public abstract class ModelMapping
{
    protected ModelMapping(string name)
    {
        Name = name;
    }

    public string Name { get; set; }

    public abstract Task<bool> Initialize();

    public abstract APIModelInfo Get(string displayName);
}


/*public class O3ModelMapping : ModelMapping
{
    public O3ModelMapping() : base("O3.Fan")
    {
    }

    public override async Task<bool> Initialize()
    {
        var httpClient = new HttpClient();
        var responseMessage = await httpClient.GetAsync(new Uri("https://geekai.co/api/models?source=web"));
        await using (var stream = await responseMessage.Content.ReadAsStreamAsync())
        {
            
        }
    }

    public override APIModelInfo Get(string displayName)
    {
        
    }
}

public class GeekAIMapping : ModelMapping
{
    public GeekAIMapping() : base("GeekAI")
    {
    }

    public override Task<bool> Initialize()
    {
        throw new NotImplementedException();
    }

    public override APIModelInfo Get(string displayName)
    {
        throw new NotImplementedException();
    }
}*/
