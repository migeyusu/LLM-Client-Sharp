using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DiffPlex;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;
using LLMClient.Abstraction;
using LLMClient.Component.Utility;
using LLMClient.Endpoints;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.SemanticKernel;

namespace LLMClient.ToolCall.DefaultPlugins;

/// <summary>
/// A Kernel Plugin for interacting with the local file system in a secure manner.
/// It provides functions for reading, writing, editing, and managing files and directories
/// within a set of allowed directories.
/// </summary>
public class FileSystemPlugin : KernelFunctionGroup, IBuiltInFunctionGroup
{
    public Guid Id = Guid.NewGuid();

    private ObservableCollection<string> _byPassPaths = new ObservableCollection<string>();

    /// <summary>
    /// indicates the list of directories that do not require special permissions to access.
    /// </summary>
    public ObservableCollection<string> BypassPaths
    {
        get => _byPassPaths;
        set { _byPassPaths = value; }
    }

    public void AddAllowedPath(string path)
    {
        if (string.IsNullOrEmpty(path.Trim()))
        {
            return;
        }

        var normalizedPath = ExpandAndNormalizePath(path);
        if (BypassPaths.Contains(normalizedPath)) return;
        BypassPaths.Add(normalizedPath);
    }

    public void RemoveAllowedPath(string path)
    {
        if (string.IsNullOrEmpty(path.Trim()))
        {
            return;
        }

        var normalizedPath = ExpandAndNormalizePath(path);
        if (BypassPaths.Contains(normalizedPath))
        {
            BypassPaths.Remove(normalizedPath);
        }
    }

    private readonly string _userHomeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    /// <summary>
    /// Initializes a new instance of the <see cref="FileSystemPlugin"/> class.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown if any of the allowed paths are invalid, not absolute, or do not exist.</exception>
    public FileSystemPlugin() : base("FileSystem")
    {
    }

    [KernelFunction, Description(
         "Read the complete contents of a file from the file system. Handles various text encodings and provides detailed error messages if the file cannot be read. " +
         "Use this tool when you need to examine the contents of a single file. " +
         "Use the 'head' parameter to read only the first N lines of a file, or the 'tail' parameter to read only the last N lines of a file. " +
         "Set includeLineNumbers=true when you want output suitable for precise code review and editing. " +
         "Only works within allowed directories.")]
    public async Task<string> ReadFileAsync(
        [Description("The path to the file to read.")]
        string path,
        [Description("Optional: If provided, returns only the first N lines of the file.")]
        int? head = null,
        [Description("Optional: If provided, returns only the last N lines of the file.")]
        int? tail = null,
        [Description("If true, prefixes each returned line with its 1-based line number.")]
        bool includeLineNumbers = false)
    {
        if (head.HasValue && tail.HasValue)
        {
            throw new ArgumentException("Cannot specify both 'head' and 'tail' parameters simultaneously.");
        }

        var fullPath = await ValidateAndResolvePathAsync(path);

        if (head.HasValue)
        {
            return await HeadFileAsync(fullPath, head.Value, includeLineNumbers);
        }

        if (tail.HasValue)
        {
            return await TailFileAsync(fullPath, tail.Value, includeLineNumbers);
        }

        if (!includeLineNumbers)
        {
            return await File.ReadAllTextAsync(fullPath);
        }

        var lines = await File.ReadAllLinesAsync(fullPath);
        return FormatLinesWithNumbers(lines, 1);
    }


    [KernelFunction, Description("Read a specific inclusive line range from a text file. " +
                                 "Returns lines prefixed with 1-based line numbers by default. " +
                                 "Useful for precise inspection of code regions without reading the whole file.")]
    public async Task<string> ReadFileRangeAsync(
        [Description("The path to the file to read.")]
        string path,
        [Description("The starting 1-based line number (inclusive).")]
        int startLine,
        [Description("The ending 1-based line number (inclusive).")]
        int endLine,
        [Description("If true, prefixes each returned line with its 1-based line number. Default is true.")]
        bool includeLineNumbers = true)
    {
        if (startLine <= 0)
            throw new ArgumentOutOfRangeException(nameof(startLine), "startLine must be greater than 0.");
        if (endLine < startLine)
            throw new ArgumentOutOfRangeException(nameof(endLine),
                "endLine must be greater than or equal to startLine.");

        var fullPath = await ValidateAndResolvePathAsync(path);
        var lines = await File.ReadAllLinesAsync(fullPath);

        if (lines.Length == 0)
            return string.Empty;

        int actualStart = Math.Min(startLine, lines.Length);
        int actualEnd = Math.Min(endLine, lines.Length);

        var slice = lines
            .Skip(actualStart - 1)
            .Take(actualEnd - actualStart + 1)
            .ToArray();

        return includeLineNumbers
            ? FormatLinesWithNumbers(slice, actualStart)
            : string.Join("\n", slice);
    }

    [KernelFunction, Description(
         "Find occurrences of text in a file and return matching lines with surrounding context. " +
         "This is useful before editing, to locate the exact region without reading the whole file.")]
    public async Task<string> FindTextInFileAsync(
        [Description("The path to the file to search.")]
        string path,
        [Description("The exact text to search for.")]
        string searchText,
        [Description("How many surrounding lines of context to include before and after each match.")]
        int contextLines = 2,
        [Description("Whether the search should be case-sensitive. Default is true.")]
        bool caseSensitive = true)
    {
        if (string.IsNullOrWhiteSpace(searchText))
            throw new ArgumentException("searchText cannot be null or empty.", nameof(searchText));
        if (contextLines < 0)
            throw new ArgumentOutOfRangeException(nameof(contextLines), "contextLines must be >= 0.");

        var fullPath = await ValidateAndResolvePathAsync(path);
        var lines = await File.ReadAllLinesAsync(fullPath);

        var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var sb = new StringBuilder();
        int matchCount = 0;

        for (int i = 0; i < lines.Length; i++)
        {
            if (!lines[i].Contains(searchText, comparison))
                continue;

            matchCount++;
            int start = Math.Max(0, i - contextLines);
            int end = Math.Min(lines.Length - 1, i + contextLines);

            sb.AppendLine($"--- Match {matchCount} at line {i + 1} ---");
            for (int j = start; j <= end; j++)
            {
                sb.AppendLine($"{j + 1}: {lines[j]}");
            }

            sb.AppendLine();
        }

        return matchCount == 0 ? "No matches found." : sb.ToString().TrimEnd();
    }

    [KernelFunction, Description("Preview line-based or context-based edits to a text file without saving changes. " +
                                 "Returns a git-style diff showing what would change. " +
                                 "Use this before applying edits when confirmation is needed.")]
    public async Task<string> PreviewEditAsync(
        [Description("The path of the file to edit.")]
        string path,
        [Description("A list of edit operations to preview.")]
        List<EditOperation> edits)
    {
        var fullPath = await ValidateAndResolvePathAsync(path);
        var originalContent = await File.ReadAllTextAsync(fullPath);
        var updatedContent = ApplyEditsToContent(originalContent, edits);

        return BuildDiffReport(path, originalContent, updatedContent, applied: false);
    }

    [KernelFunction, Description("Apply line-based or context-based edits to a text file and save changes. " +
                                 "Returns a git-style diff showing the applied modifications. " +
                                 "Edits require unique matching oldText anchors.")]
    public async Task<string> ApplyEditAsync(
        [Description("The path of the file to edit.")]
        string path,
        [Description("A list of edit operations to apply.")]
        List<EditOperation> edits)
    {
        var fullPath = await ValidateAndResolvePathAsync(path);
        var originalContent = await File.ReadAllTextAsync(fullPath);

        // Existing path-level permission check
        var permissionMessage = new StringBuilder();
        permissionMessage.Append("Applying edits to file at ");
        permissionMessage.AppendLine(fullPath);
        permissionMessage.AppendLine("Requested edits:");
        permissionMessage.AppendLine(JsonSerializer.Serialize(edits, Extension.DefaultJsonSerializerOptions));

        if (!await RequestPermission(fullPath, permissionMessage.ToString(), checkParentOnly: false))
        {
            throw new UnauthorizedAccessException("Permission denied to edit the specified file.");
        }

        // Build proposed content in memory
        var updatedContent = ApplyEditsToContent(originalContent, edits);

        // Generate preview diff
        var previewDiffText = BuildDiffReport(path, originalContent, updatedContent, applied: false);

        // Diff-aware confirmation through the existing interactor pipeline
        var approved = await RequestEditPermissionAsync(
            path,
            fullPath,
            originalContent,
            updatedContent,
            previewDiffText,
            edits);

        if (!approved)
        {
            throw new UnauthorizedAccessException($"User rejected applying changes to '{path}'.");
        }

        await File.WriteAllTextAsync(fullPath, updatedContent);

        return BuildDiffReport(path, originalContent, updatedContent, applied: true);
    }

    [KernelFunction, Description("Read the contents of multiple files simultaneously. " +
                                 "This is more efficient than reading files one by one when you need to analyze " +
                                 "or compare multiple files. Each file's content is returned with its " +
                                 "path as a reference. Failed reads for individual files won't stop " +
                                 "the entire operation. Set includeLineNumbers=true for code review scenarios. " +
                                 "Only works within allowed directories.")]
    public async Task<string> ReadMultipleFilesAsync(
        [Description("The list of file paths to read.")]
        List<string> paths,
        [Description("If true, prefixes each returned line with its 1-based line number.")]
        bool includeLineNumbers = false)
    {
        var results = new StringBuilder();
        foreach (var path in paths)
        {
            try
            {
                var fullPath = await ValidateAndResolvePathAsync(path);
                string content;

                if (!includeLineNumbers)
                {
                    content = await File.ReadAllTextAsync(fullPath);
                }
                else
                {
                    var lines = await File.ReadAllLinesAsync(fullPath);
                    content = FormatLinesWithNumbers(lines, 1);
                }

                results.AppendLine($"{path}:");
                results.AppendLine(content);
                results.AppendLine("---");
            }
            catch (Exception e)
            {
                results.AppendLine($"{path}: Error - {e.Message}");
                results.AppendLine("---");
            }
        }

        return results.ToString().TrimEnd('-', '\n', '\r');
    }

    [KernelFunction, Description("Create a new file or completely overwrite an existing file with new content. " +
                                 "Use with caution as it will overwrite existing files without warning. " +
                                 "Handles text content with proper encoding. Only works within allowed directories.")]
    public async Task<string> WriteFileAsync(
        [Description("The path of the file to write to.")]
        string path,
        [Description("The content to write into the file.")]
        string content)
    {
        // For writing, we validate the parent directory of the target path.
        var fullPath = await ValidateAndResolvePathAsync(path);
        var stringBuilder = new StringBuilder();
        stringBuilder.Append("Writing file at ");
        stringBuilder.AppendLine(fullPath);
        stringBuilder.AppendLine("Content:");
        stringBuilder.AppendLine(content);
        if (!await RequestPermission(fullPath, stringBuilder.ToString(), checkParentOnly: true))
        {
            throw new UnauthorizedAccessException("Permission denied to write to the specified file.");
        }

        await File.WriteAllTextAsync(fullPath, content);
        return $"Successfully wrote to {path}";
    }

    [KernelFunction, Description(
         "Make text edits to a file. If dryRun is true, preview changes only; otherwise apply them. " +
         "Prefer PreviewEditAsync and ApplyEditAsync for explicit workflows.")]
    public async Task<string> EditFileAsync(
        [Description("The path of the file to edit.")]
        string path,
        [Description("A list of edit operations to perform on the file.")]
        List<EditOperation> edits,
        [Description("If true, previews changes in a diff format without saving them.")]
        bool dryRun = false)
    {
        return dryRun
            ? await PreviewEditAsync(path, edits)
            : await ApplyEditAsync(path, edits);
    }

    [KernelFunction, Description("Create a new directory or ensure a directory exists. Can create multiple " +
                                 "nested directories in one operation. If the directory already exists, " +
                                 "this operation will succeed silently. Perfect for setting up directory " +
                                 "structures for projects or ensuring required paths exist. Only works within allowed directories.")]
    public async Task<string> CreateDirectoryAsync(
        [Description("The path of the directory to create.")]
        string path)
    {
        var fullPath = await ValidateAndResolvePathAsync(path);
        if (!await RequestPermission(fullPath, $"Creating directory at {fullPath}", checkParentOnly: true))
        {
            throw new UnauthorizedAccessException("Permission denied to create the specified directory.");
        }

        Directory.CreateDirectory(fullPath);
        return $"Successfully created directory {path}";
    }

    [KernelFunction, Description("Get a detailed listing of all files and directories in a specified path. " +
                                 "Results clearly distinguish between files and directories with [FILE] and [DIR] " +
                                 "prefixes. This tool is essential for understanding directory structure and " +
                                 "finding specific files within a directory. Only works within allowed directories.")]
    public async Task<string> ListDirectoryAsync(
        [Description("The path of the directory to list.")]
        string path)
    {
        var fullPath = await ValidateAndResolvePathAsync(path);
        var entries = Directory.EnumerateFileSystemEntries(fullPath);

        var formatted = entries.Select(entry =>
        {
            var entryInfo = new FileInfo(entry); // Works for both files and dirs
            bool isDirectory = (entryInfo.Attributes & FileAttributes.Directory) == FileAttributes.Directory;
            return $"{(isDirectory ? "[DIR]" : "[FILE]")} {Path.GetFileName(entry)}";
        });

        return string.Join("\n", formatted);
    }

    [KernelFunction, Description(
         "Get a detailed listing of all files and directories in a specified path, including sizes. " +
         "Results clearly distinguish between files and directories with [FILE] and [DIR] " +
         "prefixes. This tool is useful for understanding directory structure and " +
         "finding specific files within a directory. Only works within allowed directories.")]
    public async Task<string> ListDirectoryWithSizesAsync(
        [Description("The path of the directory to list.")]
        string path,
        [Description("Sort entries by 'name' or 'size'.")]
        string sortBy = "name")
    {
        var fullPath = await ValidateAndResolvePathAsync(path);
        var directoryInfo = new DirectoryInfo(fullPath);
        var entries = directoryInfo.EnumerateFileSystemInfos();

        var detailedEntries = entries.Select(e => new
        {
            Name = e.Name,
            IsDirectory = e is DirectoryInfo,
            Size = (e as FileInfo)?.Length ?? 0,
            LastModified = e.LastWriteTime
        }).ToList();

        // Sorting logic
        var sortedEntries = sortBy.ToLowerInvariant() switch
        {
            "size" => detailedEntries.OrderByDescending(e => e.Size).ToList(),
            _ => detailedEntries.OrderBy(e => e.Name).ToList()
        };

        var report = new StringBuilder();
        foreach (var entry in sortedEntries)
        {
            var type = entry.IsDirectory ? "[DIR]" : "[FILE]";
            var size = entry.IsDirectory ? "" : FormatSize(entry.Size).PadLeft(10);
            report.AppendLine($"{type} {entry.Name,-30} {size}");
        }

        // Summary
        var totalFiles = detailedEntries.Count(e => !e.IsDirectory);
        var totalDirs = detailedEntries.Count(e => e.IsDirectory);
        var totalSize = detailedEntries.Sum(e => e.Size);

        report.AppendLine();
        report.AppendLine($"Total: {totalFiles} files, {totalDirs} directories");
        report.AppendLine($"Combined size: {FormatSize(totalSize)}");

        return report.ToString();
    }

    [KernelFunction, Description("Get a recursive tree view of files and directories as a JSON structure. " +
                                 "Each entry includes 'name', 'type' (file/directory), and 'children' for directories. " +
                                 "Files have no children array, while directories always have a children array (which may be empty). " +
                                 "The output is formatted with 2-space indentation for readability. Only works within allowed directories.")]
    public async Task<string> GetDirectoryTreeAsync(
        [Description("The root path to generate the tree from.")]
        string path)
    {
        var fullPath = await ValidateAndResolvePathAsync(path);
        var rootEntry = await BuildTreeAsync(new DirectoryInfo(fullPath));
        return JsonSerializer.Serialize(rootEntry, Extension.DefaultJsonSerializerOptions);
    }

    [KernelFunction, Description("Move or rename files and directories. Can move files between directories " +
                                 "and rename them in a single operation. If the destination exists, the " +
                                 "operation will fail. Works across different directories and can be used " +
                                 "for simple renaming within the same directory. Both source and destination must be within allowed directories.")]
    public async Task<string> MoveFileAsync(
        [Description("The source path of the file or directory to move.")]
        string source,
        [Description("The destination path.")] string destination)
    {
        var validSource = await ValidateAndResolvePathAsync(source);
        var validDest = await ValidateAndResolvePathAsync(destination);
        if (!await RequestPermission(validSource, $"Moving file or directory from {validSource} to {validDest}",
                checkParentOnly: true))
        {
            throw new UnauthorizedAccessException("Permission denied to move the specified file or directory.");
        }

        if (File.Exists(validSource))
        {
            File.Move(validSource, validDest);
        }
        else if (Directory.Exists(validSource))
        {
            Directory.Move(validSource, validDest);
        }
        else
        {
            throw new FileNotFoundException("Source path does not exist or is not a file or directory.", source);
        }

        return $"Successfully moved {source} to {destination}";
    }

    [KernelFunction, Description("Recursively search for files and directories matching a pattern. " +
                                 "Searches through all subdirectories from the starting path. The search " +
                                 "is case-insensitive and matches partial names. Returns full paths to all " +
                                 "matching items. Great for finding files when you don't know their exact location. " +
                                 "Only searches within allowed directories.")]
    public async Task<string> SearchFilesAsync(
        [Description("The root path to start searching from.")]
        string path,
        [Description("The case-insensitive search pattern to match in file/directory names.")]
        string pattern,
        [Description("Optional list of glob patterns to exclude from the search (e.g., '**/bin', 'obj/**').")]
        List<string>? excludePatterns = null)
    {
        var fullPath = await ValidateAndResolvePathAsync(path);

        var matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
        if (excludePatterns != null && excludePatterns.Count > 0)
        {
            matcher.AddExcludePatterns(excludePatterns);
        }

        var allFiles = Directory.EnumerateFileSystemEntries(fullPath, "*", SearchOption.AllDirectories);

        var results = new List<string>();
        foreach (var entry in allFiles)
        {
            // Check for exclusion
            if (matcher.Match(Path.GetRelativePath(fullPath, entry)).HasMatches)
            {
                continue;
            }

            // Check for pattern match (case-insensitive contains)
            if (Path.GetFileName(entry).Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                results.Add(entry);
            }
        }

        return results.Count > 0 ? string.Join("\n", results) : "No matches found.";
    }

    [KernelFunction, Description("Retrieve detailed metadata about a file or directory. Returns comprehensive " +
                                 "information including size, creation time, last modified time, permissions, " +
                                 "and type. This tool is perfect for understanding file characteristics " +
                                 "without reading the actual content. Only works within allowed directories.")]
    public async Task<string> GetFileInfoAsync(
        [Description("The path to the file or directory.")]
        string path)
    {
        var fullPath = await ValidateAndResolvePathAsync(path);
        FileSystemInfo info = (File.GetAttributes(fullPath) & FileAttributes.Directory) == FileAttributes.Directory
            ? new DirectoryInfo(fullPath)
            : new FileInfo(fullPath);

        var sb = new StringBuilder();
        sb.AppendLine($"Name: {info.Name}");
        sb.AppendLine($"FullName: {info.FullName}");
        sb.AppendLine($"Type: {(info is DirectoryInfo ? "Directory" : "File")}");
        if (info is FileInfo fileInfo)
        {
            sb.AppendLine($"Size: {fileInfo.Length} bytes ({FormatSize(fileInfo.Length)})");
            sb.AppendLine($"IsReadOnly: {fileInfo.IsReadOnly}");
        }

        sb.AppendLine($"Created: {info.CreationTimeUtc:o}");
        sb.AppendLine($"LastModified: {info.LastWriteTimeUtc:o}");
        sb.AppendLine($"LastAccessed: {info.LastAccessTimeUtc:o}");
        return sb.ToString();
    }

    #region Helper DTOs

    /// <summary>
    /// Represents a single edit operation, replacing old text with new text.
    /// </summary>
    public class EditOperation
    {
        [JsonPropertyName("type")]
        [Description("Edit operation type: replace, delete, insertBefore, insertAfter. Default is replace.")]
        public string Type { get; set; } = "replace";

        [JsonPropertyName("oldText")]
        [Description(
            "The exact text to locate in the file. For replace/delete/insertBefore/insertAfter, this must be unique in the file.")]
        public string OldText { get; set; } = string.Empty;

        [JsonPropertyName("newText")]
        [Description("The replacement or inserted text. For delete, this can be empty.")]
        public string NewText { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents an entry in a directory tree structure.
    /// </summary>
    public class TreeEntry
    {
        [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;

        [JsonPropertyName("type")] public string Type { get; set; } = "file";

        [JsonPropertyName("children")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<TreeEntry>? Children { get; set; }
    }

    #endregion

    #region Private Helper Methods

    private string ExpandAndNormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;

        // Expand home directory symbol '~'
        if (path == "~" || path.StartsWith("~/"))
        {
            path = Path.Combine(_userHomeDirectory, path.Length > 1 ? path.Substring(2) : "");
        }

        // Return the fully qualified, normalized path.
        return Path.GetFullPath(path);
    }

    private async Task<string> ValidateAndResolvePathAsync(string requestedPath)
    {
        if (string.IsNullOrWhiteSpace(requestedPath))
        {
            throw new ArgumentException("Path cannot be null or empty.", nameof(requestedPath));
        }

        string absolutePath = ExpandAndNormalizePath(requestedPath);
        // Return the original, non-resolved absolute path if validation passes.
        // This is crucial for creating new files in directories that might themselves be symlinks.
        return await Task.FromResult(absolutePath);
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 0) return "N/A";
        if (bytes == 0) return "0 B";

        string[] units = { "B", "KB", "MB", "GB", "TB", "PB" };
        int i = (int)Math.Floor(Math.Log(bytes) / Math.Log(1024));
        return $"{bytes / Math.Pow(1024, i):F2} {units[i]}";
    }

    private static async Task<string> HeadFileAsync(string path, int lineCount, bool includeLineNumbers = false)
    {
        if (lineCount <= 0) return string.Empty;

        var lines = new List<string>(lineCount);
        using var reader = new StreamReader(path);

        for (int i = 0; i < lineCount; i++)
        {
            var line = await reader.ReadLineAsync();
            if (line == null) break;
            lines.Add(line);
        }

        return includeLineNumbers
            ? FormatLinesWithNumbers(lines, 1)
            : string.Join("\n", lines);
    }

    private static async Task<string> TailFileAsync(string path, int lineCount, bool includeLineNumbers = false)
    {
        if (lineCount <= 0) return string.Empty;

        var lines = await File.ReadAllLinesAsync(path);
        if (lines.Length == 0) return string.Empty;

        int startLine = Math.Max(1, lines.Length - lineCount + 1);
        var tailLines = lines.Skip(startLine - 1).ToArray();

        return includeLineNumbers
            ? FormatLinesWithNumbers(tailLines, startLine)
            : string.Join("\n", tailLines);
    }

    private static string FormatLinesWithNumbers(IEnumerable<string> lines, int startingLineNumber)
    {
        var sb = new StringBuilder();
        int lineNumber = startingLineNumber;

        foreach (var line in lines)
        {
            sb.Append(lineNumber);
            sb.Append(": ");
            sb.AppendLine(line);
            lineNumber++;
        }

        return sb.ToString().TrimEnd();
    }

    private static string ApplyEditsToContent(string originalContent, List<EditOperation> edits)
    {
        if (edits == null || edits.Count == 0)
            throw new ArgumentException("At least one edit operation must be provided.", nameof(edits));

        var workingContent = NormalizeLineEndings(originalContent);

        foreach (var edit in edits)
        {
            if (edit == null)
                throw new InvalidOperationException("Edit operation cannot be null.");

            var type = (edit.Type ?? "replace").Trim();
            var oldText = NormalizeLineEndings(edit.OldText ?? string.Empty);
            var newText = NormalizeLineEndings(edit.NewText ?? string.Empty);

            if (string.IsNullOrWhiteSpace(oldText))
                throw new InvalidOperationException("oldText cannot be empty. It is required as a unique anchor.");

            workingContent = ApplySingleEdit(workingContent, type, oldText, newText);
        }

        return RestoreOriginalDominantLineEnding(originalContent, workingContent);
    }

    private static FileEditRequestViewModel BuildFileEditRequestViewModel(
        string path,
        string absolutePath,
        string originalContent,
        string updatedContent,
        string diffText,
        IEnumerable<EditOperation> edits)
    {
        var vm = new FileEditRequestViewModel
        {
            Title = $"Apply changes to {System.IO.Path.GetFileName(path)}",
            Description = $"Review the proposed file changes before applying them.",
            Path = path,
            AbsolutePath = absolutePath,
            OriginalContent = originalContent,
            UpdatedContent = updatedContent,
            DiffText = diffText,
            IsReadOnly = true
        };

        foreach (var edit in edits)
        {
            vm.Operations.Add(new FileEditOperationItemViewModel
            {
                Type = edit.Type,
                OldText = edit.OldText,
                NewText = edit.NewText
            });
        }

        return vm;
    }

    private async Task<bool> RequestEditPermissionAsync(
        string path,
        string absolutePath,
        string originalContent,
        string updatedContent,
        string diffText,
        IEnumerable<EditOperation> edits)
    {
        var interactor = AsyncContextStore<ChatContext>.Current?.Interactor;
        if (interactor == null)
        {
            return true;
        }

        var vm = BuildFileEditRequestViewModel(
            path,
            absolutePath,
            originalContent,
            updatedContent,
            diffText,
            edits);

        return await interactor.WaitForPermission(vm);
    }

    private static string ApplySingleEdit(string content, string type, string oldText, string newText)
    {
        var matches = FindExactOccurrences(content, oldText);

        if (matches.Count == 0)
        {
            throw new InvalidOperationException(
                $"Could not find the target text to edit. " +
                $"Provide more exact surrounding context in oldText.\nTarget:\n{oldText}");
        }

        if (matches.Count > 1)
        {
            throw new InvalidOperationException(
                $"The target text is ambiguous and appears {matches.Count} times. " +
                $"Provide more surrounding context in oldText so it matches uniquely.");
        }

        var match = matches[0];

        return type.ToLowerInvariant() switch
        {
            "replace" => ReplaceAt(content, match.Start, match.Length, newText),
            "delete" => ReplaceAt(content, match.Start, match.Length, string.Empty),
            "insertbefore" => ReplaceAt(content, match.Start, 0, newText),
            "insertafter" => ReplaceAt(content, match.Start + match.Length, 0, newText),
            _ => throw new InvalidOperationException(
                $"Unsupported edit type '{type}'. Supported types: replace, delete, insertBefore, insertAfter.")
        };
    }

    private readonly record struct TextMatch(int Start, int Length);

    private static List<TextMatch> FindExactOccurrences(string text, string value)
    {
        var results = new List<TextMatch>();
        if (string.IsNullOrEmpty(value))
            return results;

        int index = 0;
        while (true)
        {
            index = text.IndexOf(value, index, StringComparison.Ordinal);
            if (index < 0)
                break;

            results.Add(new TextMatch(index, value.Length));
            index += value.Length;
        }

        return results;
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static string ReplaceAt(string content, int startIndex, int length, string replacement)
    {
        return content[..startIndex] + replacement + content[(startIndex + length)..];
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static string NormalizeLineEndings(string text)
    {
        return text.Replace("\r\n", "\n").Replace("\r", "\n");
    }

    private static string RestoreOriginalDominantLineEnding(string originalContent, string updatedContent)
    {
        // 如果原文件明显以 CRLF 为主，则恢复为 CRLF
        if (originalContent.Contains("\r\n"))
        {
            return updatedContent.Replace("\n", "\r\n");
        }

        return updatedContent;
    }

    private static string BuildDiffReport(string path, string originalContent, string updatedContent, bool applied)
    {
        var diffBuilder = new InlineDiffBuilder(new Differ());
        var diff = diffBuilder.BuildDiffModel(originalContent, updatedContent);

        var sb = new StringBuilder();
        sb.AppendLine($"--- a/{path.Replace('\\', '/')}");
        sb.AppendLine($"+++ b/{path.Replace('\\', '/')}");
        sb.AppendLine("```diff");

        foreach (var line in diff.Lines)
        {
            switch (line.Type)
            {
                case ChangeType.Inserted:
                    sb.AppendLine($"+ {line.Text}");
                    break;
                case ChangeType.Deleted:
                    sb.AppendLine($"- {line.Text}");
                    break;
                case ChangeType.Modified:
                    sb.AppendLine($"~ {line.Text}");
                    break;
                case ChangeType.Unchanged:
                    sb.AppendLine($"  {line.Text}");
                    break;
                case ChangeType.Imaginary:
                    break;
            }
        }

        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine(applied
            ? $"File '{path}' has been updated."
            : $"Preview only. File '{path}' was not modified.");

        return sb.ToString();
    }

    private async Task<List<TreeEntry>> BuildTreeAsync(DirectoryInfo dirInfo)
    {
        var treeEntries = new List<TreeEntry>();
        foreach (var fileSystemInfo in dirInfo.EnumerateFileSystemInfos())
        {
            var treeEntry = new TreeEntry()
            {
                Name = fileSystemInfo.Name,
            };
            if (fileSystemInfo is DirectoryInfo directoryInfo)
            {
                treeEntry.Type = "directory";
                treeEntry.Children = await BuildTreeAsync(directoryInfo);
            }
            else if (fileSystemInfo is FileInfo fileInfo)
            {
                treeEntry.Type = "file";
            }

            // Security check for each subdirectory before recursing
            await ValidateAndResolvePathAsync(fileSystemInfo.FullName);
            treeEntries.Add(treeEntry);
        }

        return treeEntries;
    }

    #endregion

    public override string? AdditionPrompt =>
        """
        This tool group provides structured access to the local file system.

        Use this tool group for:
        - reading files and directories
        - locating text in files
        - inspecting code with line numbers
        - previewing file edits
        - applying confirmed file edits

        Best practices:
        1. Prefer structured file tools over shell commands for reading and editing files.
        2. For precise code inspection, use ReadFileRangeAsync or ReadFileAsync with includeLineNumbers=true.
        3. Before editing, first inspect the relevant region with ReadFileRangeAsync or FindTextInFileAsync.
        4. Before writing changes, use PreviewEditAsync to generate a preview diff.
        5. Use ApplyEditAsync only after the intended change is clear.

        Editing rules:
        - In each edit operation, oldText must contain enough surrounding context to uniquely match exactly one location in the file.
        - Do not rely on absolute line numbers as the edit key.
        - When line-numbered output is used for inspection, do not include the line number prefixes in oldText/newText.
        - Edit operations must contain raw file text only.

        On this platform, file edits may be shown to the user in a visual diff UI before they are applied.
        """;

    public override object Clone()
    {
        return new FileSystemPlugin()
        {
            BypassPaths = new ObservableCollection<string>(BypassPaths),
        };
    }

    private Task<bool> RequestPermission(string absolutePath, string message,
        bool checkParentOnly = false, [CallerMemberName] string? callerName = null)
    {
        if (_byPassPaths.Any())
        {
            string pathToValidate = checkParentOnly ? Path.GetDirectoryName(absolutePath) ?? "" : absolutePath;
            if (string.IsNullOrEmpty(pathToValidate))
            {
                throw new UnauthorizedAccessException($"Cannot determine parent directory for path: {absolutePath}");
            }

            // 1. Check if the path is within one of the allowed directories.
            var isBypass = _byPassPaths.Any(dir => pathToValidate.StartsWith(dir, StringComparison.OrdinalIgnoreCase));
            if (isBypass)
            {
                return Task.FromResult(true);
            }

            // 2. Handle symbolic links to prevent escaping the allowed directories.
            // We check the real path of the final target.
            string realPath;
            try
            {
                // For existing files/directories, resolve the final physical path.
                realPath = new DirectoryInfo(pathToValidate).ResolveLinkTarget(true)?.FullName ?? pathToValidate;
            }
            catch (IOException) // Handles cases where path does not exist yet
            {
                // For new files/dirs, trust the absolute path after the initial check.
                realPath = pathToValidate;
            }

            var isRealPathBypass =
                _byPassPaths.Any(dir => realPath.StartsWith(dir, StringComparison.OrdinalIgnoreCase));
            if (isRealPathBypass)
            {
                return Task.FromResult(true);
            }
        }

        var interactor = AsyncContextStore<ChatContext>.Current?.Interactor;
        if (interactor == null)
        {
            return Task.FromResult(true);
        }

        var toolCallInfo = new ToolCallRequestViewModel()
        {
            CallerClassName = nameof(FileSystemPlugin),
            CallerMethodName = callerName,
            Message = message,
        };
        return interactor.WaitForPermission(toolCallInfo);
    }
}