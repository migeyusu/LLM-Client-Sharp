using System.Collections.ObjectModel;
using AutoMapper;
using LLMClient.Abstraction;
using LLMClient.Data;

namespace LLMClient.UI;

public class ModelTypeConverter : ITypeConverter<DialogViewModel, DialogPersistModel>,
    ITypeConverter<DialogPersistModel, DialogViewModel>,
    ITypeConverter<MultiResponsePersistItem, MultiResponseViewItem>,
    ITypeConverter<MultiResponseViewItem, MultiResponsePersistItem>,
    ITypeConverter<ResponsePersistItem, ResponseViewItem>
{
    private readonly IEndpointService _service;

    public ModelTypeConverter(IEndpointService service)
    {
        this._service = service;
    }

    public DialogPersistModel Convert(DialogViewModel source, DialogPersistModel destination,
        ResolutionContext context)
    {
        var dialogItems = source.DialogItems.Select<IDialogViewItem, IDialogItem>((item =>
        {
            if (item is EraseViewItem eraseViewItem)
            {
                return eraseViewItem;
            }
            else if (item is RequestViewItem requestViewItem)
            {
                return requestViewItem;
            }

            if (item is ResponseViewItem responseViewItem)
            {
                return context.Mapper.Map<ResponseViewItem, ResponsePersistItem>(responseViewItem);
            }

            if (item is MultiResponseViewItem multiResponseViewItem)
            {
                return context.Mapper.Map<MultiResponseViewItem, MultiResponsePersistItem>(multiResponseViewItem);
            }

            throw new NotSupportedException();
        })).ToArray();

        return new DialogPersistModel()
        {
            EditTime = source.EditTime,
            DialogItems = dialogItems,
            Topic = source.Topic,
            EndPoint = source.Model?.Endpoint.Name,
            Model = source.Model?.Name,
            PromptString = source.PromptString,
            Params = source.Model?.Parameters,
            TokensConsumption = source.TokensConsumption,
        };
    }

    public DialogViewModel Convert(DialogPersistModel source, DialogViewModel destination,
        ResolutionContext context)
    {
        var sourceDialogItems = source.DialogItems?.Select<IDialogItem, IDialogViewItem>((item =>
        {
            if (item is ResponsePersistItem responsePersistItem)
            {
                return context.Mapper.Map<ResponsePersistItem, ResponseViewItem>(responsePersistItem);
            }

            if (item is MultiResponsePersistItem multiResponsePersistItem)
            {
                return context.Mapper.Map<MultiResponsePersistItem, MultiResponseViewItem>(multiResponsePersistItem);
            }

            if (item is RequestViewItem requestViewItem)
            {
                return requestViewItem;
            }

            if (item is EraseViewItem eraseViewItem)
            {
                return eraseViewItem;
            }

            throw new NotSupportedException();
        })).ToArray();

        var llmEndpoint = source.EndPoint == null ? null : _service.GetEndpoint(source.EndPoint);
        ILLMModelClient? llmModelClient = null;
        if (llmEndpoint != null)
        {
            llmModelClient = llmEndpoint.NewClient(source.Model ?? string.Empty);
            if (llmModelClient != null)
            {
                var sourceJsonModel = source.Params;
                if (sourceJsonModel != null)
                {
                    llmModelClient.Parameters = sourceJsonModel;
                }
            }
        }

        return new DialogViewModel(source.Topic, llmModelClient, sourceDialogItems)
        {
            EditTime = source.EditTime,
            PromptString = source.PromptString,
            TokensConsumption = source.TokensConsumption,
        };
    }

    public ResponseViewItem Convert(ResponsePersistItem source, ResponseViewItem destination,
        ResolutionContext context)
    {
        var model = _service.GetEndpoint(source.EndPointName)?.GetModel(source.ModelName);
        return new ResponseViewItem(model, source.Raw, source.Tokens, source.IsInterrupt, source.ErrorMessage,
            source.EndPointName);
    }

    public MultiResponseViewItem Convert(MultiResponsePersistItem source, MultiResponseViewItem destination,
        ResolutionContext context)
    {
        return new MultiResponseViewItem()
        {
            AcceptedIndex = source.AcceptedIndex,
            Items = new ObservableCollection<ResponseViewItem>(source.ResponseItems.Select(x =>
                new ResponseViewItem(_service.GetEndpoint(x.EndPointName)?.GetModel(x.ModelName), x.Raw, x.Tokens,
                    x.IsInterrupt, x.ErrorMessage, x.EndPointName)))
        };
    }

    public MultiResponsePersistItem Convert(MultiResponseViewItem source, MultiResponsePersistItem destination,
        ResolutionContext context)
    {
        return new MultiResponsePersistItem()
        {
            AcceptedIndex = source.AcceptedIndex,
            ResponseItems = source.Items.Select(x => context.Mapper.Map<ResponseViewItem, ResponsePersistItem>(x))
                .ToArray()
        };
    }
}