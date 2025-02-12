namespace LLMClient.Render;

internal static class StringExtensions
{
    internal static string SubstringAtIndexes(this string str, int startIndex, int endIndex)
    {
        return str.Substring(startIndex, endIndex - startIndex);
    }
}