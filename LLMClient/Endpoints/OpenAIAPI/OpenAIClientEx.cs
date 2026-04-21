using System.ClientModel;
using OpenAI;
using OpenAI.Chat;

namespace LLMClient.Endpoints.OpenAIAPI;

public class OpenAIClientEx : OpenAIClient
{
    private readonly ApiKeyCredential _credential;
    private readonly OpenAIClientOptions _options;
    private readonly bool _treatNullChoicesAsEmptyResponse;

    public OpenAIClientEx(ApiKeyCredential credential, OpenAIClientOptions options,
        bool treatNullChoicesAsEmptyResponse = false) : base(credential, options)
    {
        _credential = credential;
        _options = options;
        _treatNullChoicesAsEmptyResponse = treatNullChoicesAsEmptyResponse;
    }
    
    public override ChatClient GetChatClient(string model)
    {
        return new OpenAIChatClientEx(model, _credential, this._options, _treatNullChoicesAsEmptyResponse);
    }
}