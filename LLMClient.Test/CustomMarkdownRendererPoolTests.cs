using System.Collections.Concurrent;
using System.Reflection;
using System.Windows.Documents;
using LLMClient.Component.Render;

namespace LLMClient.Test;

public class CustomMarkdownRendererPoolTests
{
    [Fact]
    public void NewRenderer_AfterReturn_ReusesRendererAndResetsFlags()
    {
        TestFixture.RunInStaThread(() =>
        {
            ClearPool();
            try
            {
                var firstDocument = new FlowDocument();
                var renderer = CustomMarkdownRenderer.NewRenderer(firstDocument, enableTextMate: false, editMode: true);

                Assert.False(renderer.EnableTextMateHighlighting);
                Assert.True(renderer.EditMode);

                CustomMarkdownRenderer.Return(renderer);

                var secondDocument = new FlowDocument();
                var reusedRenderer = CustomMarkdownRenderer.Rent(secondDocument);
                try
                {
                    Assert.Same(renderer, reusedRenderer);
                    Assert.True(reusedRenderer.EnableTextMateHighlighting);
                    Assert.False(reusedRenderer.EditMode);

                    reusedRenderer.RenderRaw("second document", secondDocument);

                    Assert.Equal("second document", ReadDocumentText(secondDocument));
                    Assert.Equal(string.Empty, ReadDocumentText(firstDocument));
                }
                finally
                {
                    CustomMarkdownRenderer.Return(reusedRenderer);
                }
            }
            finally
            {
                ClearPool();
            }
        });
    }

    [Fact]
    public void ReturnedRenderer_CanBeReusedAcrossMultipleDocuments()
    {
        TestFixture.RunInStaThread(() =>
        {
            ClearPool();
            try
            {
                var firstDocument = new FlowDocument();
                var renderer = CustomMarkdownRenderer.Rent(firstDocument);
                renderer.RenderRaw("first document", firstDocument);
                CustomMarkdownRenderer.Return(renderer);

                var secondDocument = new FlowDocument();
                var reusedRenderer = CustomMarkdownRenderer.Rent(secondDocument);
                try
                {
                    reusedRenderer.RenderRaw("second document", secondDocument);

                    Assert.Equal("first document", ReadDocumentText(firstDocument));
                    Assert.Equal("second document", ReadDocumentText(secondDocument));
                }
                finally
                {
                    CustomMarkdownRenderer.Return(reusedRenderer);
                }
            }
            finally
            {
                ClearPool();
            }
        });
    }

    [Fact]
    public void Return_DoesNotPoolEditRenderer()
    {
        TestFixture.RunInStaThread(() =>
        {
            ClearPool();
            try
            {
                var editRenderer = CustomMarkdownRenderer.EditRenderer(new FlowDocument());
                CustomMarkdownRenderer.Return(editRenderer);

                var renderer = CustomMarkdownRenderer.Rent(new FlowDocument());
                try
                {
                    Assert.NotSame(editRenderer, renderer);
                    Assert.False(renderer.EditMode);
                    Assert.Same(CustomMarkdownRenderer.DefaultPipeline, renderer.Pipeline);
                }
                finally
                {
                    CustomMarkdownRenderer.Return(renderer);
                }
            }
            finally
            {
                ClearPool();
            }
        });
    }

    private static string ReadDocumentText(FlowDocument document)
    {
        return new TextRange(document.ContentStart, document.ContentEnd).Text.Trim();
    }

    private static void ClearPool()
    {
        while (GetPool().TryTake(out _))
        {
        }
    }

    private static ConcurrentBag<CustomMarkdownRenderer> GetPool()
    {
        var field = typeof(CustomMarkdownRenderer)
            .GetField("Pool", BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(field);

        return (ConcurrentBag<CustomMarkdownRenderer>)field.GetValue(null)!;
    }
}

