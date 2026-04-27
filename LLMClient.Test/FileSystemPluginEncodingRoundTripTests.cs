﻿using System.Text;
using LLMClient.ToolCall.DefaultPlugins;

namespace LLMClient.Test;

public class FileSystemPluginEncodingRoundTripTests
{
    [Fact]
    public async Task ApplyEditAsync_Utf8WithoutBom_ShouldRemainWithoutBom()
    {
        var tempDir = CreateTempDirectory();

        try
        {
            var bytes = await ApplyReplaceAndReadBytesAsync(
                tempDir,
                fileName: "utf8-no-bom.txt",
                originalContent: "你好，世界\n第二行",
                writeEncoding: new UTF8Encoding(false));

            var utf8Bom = new UTF8Encoding(true).GetPreamble();
            Assert.False(StartsWithPreamble(bytes, utf8Bom));

            var text = new UTF8Encoding(false).GetString(bytes);
            Assert.DoesNotContain('\uFEFF', text);
            Assert.Contains("世界!", text);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task ApplyEditAsync_Utf8WithBom_ShouldKeepSingleBomOnly()
    {
        var tempDir = CreateTempDirectory();

        try
        {
            var encoding = new UTF8Encoding(true);
            var bytes = await ApplyReplaceAndReadBytesAsync(
                tempDir,
                fileName: "utf8-with-bom.txt",
                originalContent: "你好，世界\n第二行",
                writeEncoding: encoding);

            var preamble = encoding.GetPreamble();
            Assert.True(StartsWithPreamble(bytes, preamble));
            Assert.False(StartsWithDoublePreamble(bytes, preamble));

            var textWithoutFirstBom = DecodeWithoutLeadingPreamble(bytes, encoding);
            Assert.False(textWithoutFirstBom.StartsWith('\uFEFF'));
            Assert.Contains("世界!", textWithoutFirstBom);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task ApplyEditAsync_Utf16LeWithBom_ShouldKeepSingleBomOnly()
    {
        var tempDir = CreateTempDirectory();

        try
        {
            var encoding = new UnicodeEncoding(bigEndian: false, byteOrderMark: true);
            var bytes = await ApplyReplaceAndReadBytesAsync(
                tempDir,
                fileName: "utf16-le-with-bom.txt",
                originalContent: "你好，世界\n第二行",
                writeEncoding: encoding);

            var preamble = encoding.GetPreamble();
            Assert.True(StartsWithPreamble(bytes, preamble));
            Assert.False(StartsWithDoublePreamble(bytes, preamble));

            var textWithoutFirstBom = DecodeWithoutLeadingPreamble(bytes, encoding);
            Assert.False(textWithoutFirstBom.StartsWith('\uFEFF'));
            Assert.Contains("世界!", textWithoutFirstBom);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task ApplyEditAsync_Utf16BeWithBom_ShouldKeepSingleBomOnly()
    {
        var tempDir = CreateTempDirectory();

        try
        {
            var encoding = new UnicodeEncoding(bigEndian: true, byteOrderMark: true);
            var bytes = await ApplyReplaceAndReadBytesAsync(
                tempDir,
                fileName: "utf16-be-with-bom.txt",
                originalContent: "你好，世界\n第二行",
                writeEncoding: encoding);

            var preamble = encoding.GetPreamble();
            Assert.True(StartsWithPreamble(bytes, preamble));
            Assert.False(StartsWithDoublePreamble(bytes, preamble));

            var textWithoutFirstBom = DecodeWithoutLeadingPreamble(bytes, encoding);
            Assert.False(textWithoutFirstBom.StartsWith('\uFEFF'));
            Assert.Contains("世界!", textWithoutFirstBom);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task ApplyEditAsync_Utf16LeWithoutBom_ShouldRemainWithoutBom()
    {
        var tempDir = CreateTempDirectory();

        try
        {
            var encoding = new UnicodeEncoding(bigEndian: false, byteOrderMark: false);
            var bytes = await ApplyReplaceAndReadBytesAsync(
                tempDir,
                fileName: "utf16-le-no-bom.txt",
                originalContent: "hello world\nsecond line",
                writeEncoding: encoding,
                oldText: "world",
                newText: "world!");

            var bomPreamble = new UnicodeEncoding(bigEndian: false, byteOrderMark: true).GetPreamble();
            Assert.False(StartsWithPreamble(bytes, bomPreamble));

            var text = encoding.GetString(bytes);
            Assert.False(text.StartsWith('\uFEFF'));
            Assert.Contains("world!", text);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task ApplyEditAsync_Utf16BeWithoutBom_ShouldRemainWithoutBom()
    {
        var tempDir = CreateTempDirectory();

        try
        {
            var encoding = new UnicodeEncoding(bigEndian: true, byteOrderMark: false);
            var bytes = await ApplyReplaceAndReadBytesAsync(
                tempDir,
                fileName: "utf16-be-no-bom.txt",
                originalContent: "hello world\nsecond line",
                writeEncoding: encoding,
                oldText: "world",
                newText: "world!");

            var bomPreamble = new UnicodeEncoding(bigEndian: true, byteOrderMark: true).GetPreamble();
            Assert.False(StartsWithPreamble(bytes, bomPreamble));

            var text = encoding.GetString(bytes);
            Assert.False(text.StartsWith('\uFEFF'));
            Assert.Contains("world!", text);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task ApplyEditAsync_Utf16LeWithoutBom_MixedCjkAscii_ShouldRemainWithoutBom()
    {
        var tempDir = CreateTempDirectory();

        try
        {
            var encoding = new UnicodeEncoding(bigEndian: false, byteOrderMark: false);
            var bytes = await ApplyReplaceAndReadBytesAsync(
                tempDir,
                fileName: "utf16-le-no-bom-mixed.txt",
                originalContent: "hello 世界, mixed 内容\nsecond 行 with ascii",
                writeEncoding: encoding,
                oldText: "世界",
                newText: "世界!");

            var bomPreamble = new UnicodeEncoding(bigEndian: false, byteOrderMark: true).GetPreamble();
            Assert.False(StartsWithPreamble(bytes, bomPreamble));

            var text = encoding.GetString(bytes);
            Assert.False(text.StartsWith('\uFEFF'));
            Assert.Contains("世界!", text);
            Assert.Contains("内容", text);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task ApplyEditAsync_Utf16BeWithoutBom_MixedCjkAscii_ShouldRemainWithoutBom()
    {
        var tempDir = CreateTempDirectory();

        try
        {
            var encoding = new UnicodeEncoding(bigEndian: true, byteOrderMark: false);
            var bytes = await ApplyReplaceAndReadBytesAsync(
                tempDir,
                fileName: "utf16-be-no-bom-mixed.txt",
                originalContent: "hello 世界, mixed 内容\nsecond 行 with ascii",
                writeEncoding: encoding,
                oldText: "世界",
                newText: "世界!");

            var bomPreamble = new UnicodeEncoding(bigEndian: true, byteOrderMark: true).GetPreamble();
            Assert.False(StartsWithPreamble(bytes, bomPreamble));

            var text = encoding.GetString(bytes);
            Assert.False(text.StartsWith('\uFEFF'));
            Assert.Contains("世界!", text);
            Assert.Contains("内容", text);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    private static async Task<byte[]> ApplyReplaceAndReadBytesAsync(
        string tempDir,
        string fileName,
        string originalContent,
        Encoding writeEncoding,
        string oldText = "世界",
        string newText = "世界!")
    {
        var filePath = Path.Combine(tempDir, fileName);
        await File.WriteAllTextAsync(filePath, originalContent, writeEncoding);

        var plugin = new FileSystemPlugin();
        plugin.AddAllowedPath(tempDir);

        await plugin.ApplyEditAsync(filePath, [
            new FileSystemPlugin.EditOperation
            {
                Type = "replace",
                OldText = oldText,
                NewText = newText
            }
        ]);

        return await File.ReadAllBytesAsync(filePath);
    }

    private static string CreateTempDirectory()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "FileSystemPluginEncoding_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        return tempDir;
    }

    private static string DecodeWithoutLeadingPreamble(byte[] bytes, Encoding encoding)
    {
        var preamble = encoding.GetPreamble();
        if (preamble.Length > 0 && StartsWithPreamble(bytes, preamble))
        {
            return encoding.GetString(bytes, preamble.Length, bytes.Length - preamble.Length);
        }

        return encoding.GetString(bytes);
    }

    private static bool StartsWithPreamble(byte[] bytes, byte[] preamble)
    {
        if (preamble.Length == 0 || bytes.Length < preamble.Length)
        {
            return false;
        }

        for (var i = 0; i < preamble.Length; i++)
        {
            if (bytes[i] != preamble[i])
            {
                return false;
            }
        }

        return true;
    }

    private static bool StartsWithDoublePreamble(byte[] bytes, byte[] preamble)
    {
        if (preamble.Length == 0 || bytes.Length < preamble.Length * 2)
        {
            return false;
        }

        for (var i = 0; i < preamble.Length; i++)
        {
            if (bytes[i] != preamble[i] || bytes[preamble.Length + i] != preamble[i])
            {
                return false;
            }
        }

        return true;
    }
}
