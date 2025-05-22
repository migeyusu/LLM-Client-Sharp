using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Plugins.Document.OpenXml;
using SkiaSharp;

namespace LLMClient;

public class TestClass
{
    public static async void TestDocumentLoad()
    {
        using (WordprocessingDocument wordprocessingDocument =
               WordprocessingDocument.Open("E:\\PCCT原型机文档\\PCCT项目DAS与DPB接口定义_1_6.docx", false))
        {
            
        }
    }
    
}