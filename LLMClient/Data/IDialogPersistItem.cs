using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace LLMClient.Data;

//为了保证兼容性，禁止更改名称
[JsonDerivedType(typeof(ErasePersistItem), "erase")]
[JsonDerivedType(typeof(RequestPersistItem), "request")]
[JsonDerivedType(typeof(ParallelResponsePersisItem), "multiResponse")]
[JsonDerivedType(typeof(LinearHistoryResponsePersistItem), "linearHistoryResponse")]
[JsonDerivedType(typeof(SummaryRequestPersistItem), "summaryRequest")]
public interface IDialogPersistItem
{
    Guid Id { get; set; }

    Guid? PreviousItemId { get; set; }

    [MemberNotNullWhen(true, nameof(PreviousItemId))]
    bool HasPreviousItem()
    {
        return PreviousItemId.HasValue && PreviousItemId != Guid.Empty;
    }
}