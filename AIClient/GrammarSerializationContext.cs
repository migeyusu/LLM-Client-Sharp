using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using TextMateSharp.Grammars;

namespace LLMClient;

[JsonSerializable(typeof(GrammarDefinition))]
[JsonSerializable(typeof(LanguageSnippets))]
[JsonSerializable(typeof(LanguageSnippet))]
[JsonSerializable(typeof(LanguageConfiguration))]
[JsonSerializable(typeof(EnterRule))]
[JsonSerializable(typeof(AutoPair))]
[JsonSerializable(typeof(IList<string>))]
public sealed partial class GrammarSerializationContext : JsonSerializerContext
{
}