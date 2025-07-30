using System.ClientModel;
using OpenAI;
using OpenAI.Chat;

namespace LLMClient.Endpoints.OpenAIAPI;

public class OpenAIClientEx : OpenAIClient
{
    private readonly ApiKeyCredential _credential;
    private readonly OpenAIClientOptions _options;

    public OpenAIClientEx(ApiKeyCredential credential, OpenAIClientOptions options) : base(credential, options)
    {
        _credential = credential;
        _options = options;
    }

    public override ChatClient GetChatClient(string model)
    {
        return new OpenAIChatClientEx(model, _credential, this._options);
    }
}