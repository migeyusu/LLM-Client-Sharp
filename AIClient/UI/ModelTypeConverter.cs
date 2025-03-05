using System.Collections.ObjectModel;
using AutoMapper;

namespace LLMClient.UI;

public class ModelTypeConverter : ITypeConverter<DialogViewModel, DialogModel>,
    ITypeConverter<DialogModel, DialogViewModel>
{
    private readonly IEndpointService _service;

    public ModelTypeConverter(IEndpointService service)
    {
        this._service = service;
    }

    public DialogModel Convert(DialogViewModel source, DialogModel destination, ResolutionContext context)
    {
        return new DialogModel()
        {
            DialogId = source.DialogId,
            DialogItems = source.Dialog.ToArray(),
            Topic = source.Topic,
            EndPoint = source.Endpoint?.Name,
            Model = source.Model?.Name,
            PromptString = source.PromptString,
            Params = source.Model?.Serialize(),
        };
    }

    public DialogViewModel Convert(DialogModel source, DialogViewModel destination, ResolutionContext context)
    {
        var dialogViewModel = new DialogViewModel()
        {
            Topic = source.Topic,
            DialogId = source.DialogId,
            PromptString = source.PromptString,
        };
        var sourceDialogItems = source.DialogItems;
        if (sourceDialogItems != null)
        {
            dialogViewModel.Dialog =
                new ObservableCollection<IDialogViewItem>(sourceDialogItems.Where(item => item is not ILLMModelClient));
        }

        var llmEndpoint = _service.AvailableEndpoints.FirstOrDefault((endpoint => endpoint.Name == source.EndPoint));
        if (llmEndpoint != null)
        {
            dialogViewModel.Endpoint = llmEndpoint;
            var llmModel = llmEndpoint.NewClient(source.Model ?? string.Empty);
            if (llmModel != null)
            {
                dialogViewModel.Model = llmModel;
                var sourceJsonModel = source.Params;
                if (sourceJsonModel != null)
                {
                    llmModel.Deserialize(sourceJsonModel);
                }
            }
        }

        return dialogViewModel;
    }
}