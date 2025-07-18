﻿using System.Text.Json.Serialization;
using LLMClient.UI;
using LLMClient.UI.Dialog;

namespace LLMClient.Data;

[JsonDerivedType(typeof(EraseViewItem), "erase")]
[JsonDerivedType(typeof(RequestViewItem), "request")]
[JsonDerivedType(typeof(MultiResponsePersistItem), "multiResponse")]
[JsonDerivedType(typeof(SummaryRequestViewItem),"summaryRequest")]
public interface IDialogPersistItem
{
}