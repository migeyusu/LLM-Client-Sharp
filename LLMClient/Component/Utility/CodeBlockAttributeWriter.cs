namespace LLMClient.Component.Utility;

using System.Text;
using Markdig.Renderers.Html;

public static class CodeBlockAttributeWriter
{
    public static string ToCodeBlockAttributes(this HtmlAttributes? attributes)
    {
        if (attributes == null)
            return string.Empty;

        var sb = new StringBuilder();
        var hasAny = false;

        sb.Append('{');

        if (!string.IsNullOrEmpty(attributes.Id))
        {
            sb.Append('#');
            sb.Append(attributes.Id);
            hasAny = true;
        }

        if (attributes.Classes != null)
        {
            foreach (var cls in attributes.Classes)
            {
                if (hasAny) sb.Append(' ');
                sb.Append('.');
                sb.Append(cls);
                hasAny = true;
            }
        }

        if (attributes.Properties != null)
        {
            foreach (var prop in attributes.Properties)
            {
                if (hasAny) sb.Append(' ');
                sb.Append(EscapeKey(prop.Key));
                sb.Append('=');
                sb.Append('"');
                sb.Append(EscapeAttributeValue(prop.Value));
                sb.Append('"');
                hasAny = true;
            }
        }

        sb.Append('}');

        return hasAny ? sb.ToString() : string.Empty;
    }

    private static string EscapeKey(string key)
    {
        if (string.IsNullOrEmpty(key)) return "_";
        return new string(key.Select(c =>
            char.IsLetterOrDigit(c) || c is '_' or '-' or ':' or '.' ? c : '_').ToArray());
    }

    private static string EscapeAttributeValue(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value.Replace("\"", "\\\"");
    }
}