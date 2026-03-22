#nullable enable

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LLMClient.ToolCall;

/// <summary>
/// ViewModel passed to <see cref="IInvokeInteractor.WaitForPermission(object)"/>
/// to preview and confirm file edits before applying them.
/// </summary>
public class FileEditRequestViewModel : INotifyPropertyChanged
{
    /// <summary>
    /// Dialog title or UI header.
    /// </summary>
    public string Title
    {
        get;
        set => SetField(ref field, value);
    } = "Confirm File Edit";

    /// <summary>
    /// Additional description for the UI.
    /// </summary>
    public string Description
    {
        get;
        set => SetField(ref field, value);
    } = string.Empty;

    /// <summary>
    /// Logical/user-facing path.
    /// </summary>
    public string Path
    {
        get;
        set => SetField(ref field, value);
    } = string.Empty;

    /// <summary>
    /// Fully resolved absolute path.
    /// </summary>
    public string AbsolutePath
    {
        get;
        set => SetField(ref field, value);
    } = string.Empty;

    /// <summary>
    /// Original file content before the proposed edit.
    /// </summary>
    public string OriginalContent
    {
        get;
        set => SetField(ref field, value);
    } = string.Empty;

    /// <summary>
    /// Updated file content after in-memory edit application.
    /// </summary>
    public string UpdatedContent
    {
        get;
        set => SetField(ref field, value);
    } = string.Empty;

    /// <summary>
    /// Textual diff representation for display or fallback rendering.
    /// </summary>
    public string DiffText
    {
        get;
        set => SetField(ref field, value);
    } = string.Empty;

    /// <summary>
    /// Whether the UI should allow editing the proposed updated content.
    /// Default true means preview/confirmation only.
    /// </summary>
    public bool IsReadOnly
    {
        get;
        set => SetField(ref field, value);
    } = true;

    /// <summary>
    /// Optional structured preview of edit operations.
    /// </summary>
    public ObservableCollection<FileEditOperationItemViewModel> Operations { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}

/// <summary>
/// UI-friendly representation of a single requested edit operation.
/// </summary>
public class FileEditOperationItemViewModel
{
    public string Type { get; set; } = string.Empty;

    public string OldText { get; set; } = string.Empty;

    public string NewText { get; set; } = string.Empty;
}