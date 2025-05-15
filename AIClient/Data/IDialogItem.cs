using System.Text.Json.Serialization;
using LLMClient.UI;

namespace LLMClient.Data;

[JsonDerivedType(typeof(EraseViewItem), "erase")]
[JsonDerivedType(typeof(RequestViewItem), "request")]
[JsonDerivedType(typeof(ResponsePersistItem), "response")]
[JsonDerivedType(typeof(MultiResponsePersistItem), "multiResponse")]
public interface IDialogItem
{
    
}