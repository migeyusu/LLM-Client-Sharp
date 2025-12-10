using Markdig;
using Markdig.Extensions.JiraLinks;
using Markdig.Syntax;
using Xunit.Abstractions;

namespace LLMClient.Test;

public class MarkdownTest
{
    private ITestOutputHelper _output;

    public MarkdownTest(ITestOutputHelper output)
    {
        this._output = output;
    }

    [Fact]
    public void JiraLink()
    {
        var text =
            "This is a ABCD-123 issue\n.\n<p>This is a <a href=\"http://your.company.abc/browse/ABCD-123\" target=\"_blank\">ABCD-123</a> issue</p>";
        var pipeline = new MarkdownPipelineBuilder()
            .UseJiraLinks(new JiraLinkOptions("http://your.company.abc"))
            .Build();
        var markdownDocument = Markdown.Parse(text,pipeline);
        var array = markdownDocument.Descendants().ToArray();
    }

    [Fact]
    public void CustomMath()
    {
        var text = "This is a math block:\n\\[\n\\boxed{ Rf(\\theta, s) = \\int_{L_{\\theta,s}} f(x, y) \\, ds }\n\\]";
        var pipeline = new MarkdownPipelineBuilder()
            .UseMathematics()
            .Build();
        var markdownDocument = Markdown.Parse(text, pipeline);
        var array = markdownDocument.Descendants().ToArray();
    }
}