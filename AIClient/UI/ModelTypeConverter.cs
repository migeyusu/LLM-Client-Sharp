using System.Collections.ObjectModel;
using AutoMapper;

namespace LLMClient.UI;

public class ModelTypeConverter : ITypeConverter<DialogViewModel, DialogPersistanceModel>,
    ITypeConverter<DialogPersistanceModel, DialogViewModel>
{
    private readonly IEndpointService _service;

    public ModelTypeConverter(IEndpointService service)
    {
        this._service = service;
    }

    public DialogPersistanceModel Convert(DialogViewModel source, DialogPersistanceModel destination,
        ResolutionContext context)
    {
        return new DialogPersistanceModel()
        {
            DialogId = source.DialogId,
            EditTime = source.EditTime,
            DialogItems = source.Dialog.ToArray(),
            Topic = source.Topic,
            EndPoint = source.Model?.Endpoint.Name,
            Model = source.Model?.Name,
            PromptString = source.PromptString,
            Params = source.Model?.Serialize(),
        };
    }

    public DialogViewModel Convert(DialogPersistanceModel source, DialogViewModel destination,
        ResolutionContext context)
    {
        var sourceDialogItems = source.DialogItems;
        if (sourceDialogItems != null)
        {
            sourceDialogItems = sourceDialogItems.Where(item => item is not ILLMModelClient).ToArray();
        }

        var llmEndpoint = _service.AvailableEndpoints.FirstOrDefault((endpoint => endpoint.Name == source.EndPoint));
        ILLMModelClient? llmModelClient = null;
        if (llmEndpoint != null)
        {
            llmModelClient = llmEndpoint.NewClient(source.Model ?? string.Empty);
            if (llmModelClient != null)
            {
                var sourceJsonModel = source.Params;
                if (sourceJsonModel != null)
                {
                    llmModelClient.Deserialize(sourceJsonModel);
                }
            }
        }

        return new DialogViewModel(source.Topic, llmModelClient, sourceDialogItems)
        {
            EditTime = source.EditTime,
            DialogId = source.DialogId,
            PromptString = source.PromptString,
        };
    }
}