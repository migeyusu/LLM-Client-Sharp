using System.Collections.ObjectModel;
using AutoMapper;
using LLMClient.Abstraction;
using LLMClient.Data;
using LLMClient.Endpoints;
using LLMClient.UI.Dialog;

namespace LLMClient.UI;

public class AutoMapModelTypeConverter : ITypeConverter<DialogSession, DialogSessionPersistModel>,
    ITypeConverter<DialogSessionPersistModel, DialogSession>,
    ITypeConverter<MultiResponsePersistItem, MultiResponseViewItem>,
    ITypeConverter<MultiResponseViewItem, MultiResponsePersistItem>,
    ITypeConverter<ResponsePersistItem, ResponseViewItem>
{
    private readonly IEndpointService _endpointService;

    public AutoMapModelTypeConverter(IEndpointService service)
    {
        this._endpointService = service;
    }

    public DialogSessionPersistModel Convert(DialogSession from, DialogSessionPersistModel destination,
        ResolutionContext context)
    {
        var source = from.Dialog;
        var dialogItems = source.DialogItems.Select<IDialogItem, IDialogPersistItem>((item =>
        {
            if (item is EraseViewItem eraseViewItem)
            {
                return eraseViewItem;
            }

            if (item is RequestViewItem requestViewItem)
            {
                return requestViewItem;
            }

            if (item is MultiResponseViewItem multiResponseViewItem)
            {
                return context.Mapper.Map<MultiResponseViewItem, MultiResponsePersistItem>(multiResponseViewItem);
            }

            throw new NotSupportedException();
        })).ToArray();

        return new DialogSessionPersistModel()
        {
            EditTime = from.EditTime,
            DialogItems = dialogItems,
            Topic = source.Topic,
            EndPoint = source.DefaultClient.Endpoint.Name,
            Model = source.DefaultClient.Name,
            PromptString = source.PromptString,
            Params = source.DefaultClient.Parameters,
            TokensConsumption = source.TokensConsumption,
            TotalPrice = source.TotalPrice,
        };
    }

    public DialogSession Convert(DialogSessionPersistModel source, DialogSession destination,
        ResolutionContext context)
    {
        var sourceDialogItems = source.DialogItems?.Select<IDialogPersistItem, IDialogItem>((item =>
        {
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

        var llmEndpoint = source.EndPoint == null ? null : _endpointService.GetEndpoint(source.EndPoint);
        var llmModelClient = llmEndpoint?.NewClient(source.Model ?? string.Empty) ?? new NullLlmModelClient();
        var sourceJsonModel = source.Params;
        if (sourceJsonModel != null)
        {
            llmModelClient.Parameters = sourceJsonModel;
        }

        var dialogViewModel = new DialogViewModel(source.Topic, llmModelClient, sourceDialogItems)
        {
            PromptString = source.PromptString,
            TokensConsumption = source.TokensConsumption,
            TotalPrice = source.TotalPrice,
        };
        return new DialogSession(dialogViewModel) { EditTime = source.EditTime };
    }

    public ResponseViewItem Convert(ResponsePersistItem source, ResponseViewItem destination,
        ResolutionContext context)
    {
        var model = _endpointService.GetEndpoint(source.EndPointName)?.GetModel(source.ModelName);
        return new ResponseViewItem(model, source, source.EndPointName);
    }

    public MultiResponseViewItem Convert(MultiResponsePersistItem source, MultiResponseViewItem destination,
        ResolutionContext context)
    {
        var items = source.ResponseItems.Select(x =>
            context.Mapper.Map<ResponsePersistItem, ResponseViewItem>(x));
        return new MultiResponseViewItem(items)
        {
            AcceptedIndex = source.AcceptedIndex,
            InteractionId = source.InteractionId,
        };
    }

    public MultiResponsePersistItem Convert(MultiResponseViewItem source, MultiResponsePersistItem destination,
        ResolutionContext context)
    {
        return new MultiResponsePersistItem()
        {
            AcceptedIndex = source.AcceptedIndex,
            ResponseItems = source.Items.OfType<ResponseViewItem>()
                .Select(x => context.Mapper.Map<ResponseViewItem, ResponsePersistItem>(x))
                .ToArray(),
            InteractionId = source.InteractionId,
        };
    }
}