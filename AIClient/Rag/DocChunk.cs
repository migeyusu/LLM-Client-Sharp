using System.Diagnostics;
using System.Windows.Media;
using LLMClient.Data;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Data;

namespace LLMClient.Rag;

public class DocChunk
{
    [VectorStoreKey] public string Key { get; set; } = string.Empty;

    /// <summary>
    /// raw data
    /// </summary>
    [VectorStoreData(IsFullTextIndexed = true)]
    [TextSearchResultValue]
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// 文档的唯一标识符
    /// </summary>
    [VectorStoreData]
    public string DocumentId { get; set; } = string.Empty;

    [VectorStoreData(IsFullTextIndexed = true)]
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// pdf:bookmark
    /// </summary>
    [VectorStoreData(IsFullTextIndexed = true)]
    public string Title { get; set; } = string.Empty;

    [VectorStoreData] public int Level { get; set; }

    [VectorStoreData] public string ParentKey { get; set; } = string.Empty;

    /// <summary>
    /// index in the level
    /// </summary>
    [VectorStoreData]
    public int Index { get; set; } = 0;

    /// <summary>
    /// indicate whether has child nodes. only used for chunk type 1 (node).
    /// </summary>
    [VectorStoreData]
    public bool HasChildNode { get; set; }

    /// <summary>
    /// see definition of <see cref="ChunkType"/>
    /// </summary>
    [VectorStoreData]
    public int Type { get; set; }

    [VectorStoreVector(SemanticKernelStore.ChunkDimension)]
    public string TextEmbedding => Text;

    [VectorStoreVector(SemanticKernelStore.ChunkDimension)]
    public string SummaryEmbedding => Summary;

    /// <summary>
    /// base64 encoded attachment data, such as pdf, image, etc.
    /// spilt by \n, each line is a base64 encoded data.
    /// <para>support image now</para>
    /// </summary>
    [VectorStoreData(IsFullTextIndexed = true)]
    public string Attachment { get; set; } = string.Empty;

    public void SetImages(IList<string> base64Images)
    {
        if (base64Images.Count == 0)
        {
            Attachment = string.Empty;
            return;
        }

        // Attachment = base64Images[0].Substring(0, 100);
        Attachment = string.Join('\n', base64Images);
    }

    private IList<ImageSource>? _attachmentImages;

    public IList<ImageSource> AttachmentImages
    {
        get
        {
            if (_attachmentImages == null)
            {
                if (!string.IsNullOrEmpty(Attachment))
                {
                    try
                    {
                        _attachmentImages = Attachment.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                            .Select(ImageExtensions.GetImageSourceFromBase64).ToArray();
                    }
                    catch (Exception extension)
                    {
                        Trace.TraceError("Failed to decode attachment images: " + extension.Message);
                    }
                }

                _attachmentImages ??= Array.Empty<ImageSource>();
            }

            return _attachmentImages;
        }
    }
}

public enum ChunkType : int
{
    /// <summary>
    /// pdf bookmark
    /// </summary>
    Bookmark = 1,

    /// <summary>
    /// pdf page
    /// </summary>
    Page = 2,
}