using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace LLMClient.Data;

[JsonDerivedType(typeof(ErasePersistItem), "erase")]
[JsonDerivedType(typeof(RequestPersistItem), "request")]
[JsonDerivedType(typeof(MultiResponsePersistItem), "multiResponse")]
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