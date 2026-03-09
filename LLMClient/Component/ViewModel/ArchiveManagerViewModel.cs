using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input; // Added namespace
using LLMClient.Component.Utility;
using LLMClient.Component.ViewModel.Base;
using LLMClient.Dialog;
using LLMClient.Project;
using MaterialDesignThemes.Wpf;

namespace LLMClient.Component.ViewModel;

public class ArchiveManagerViewModel : BaseViewModel
{
    private const string DialogArchive = "DialogArchive";
    private const string ProjectArchive = "ProjectArchive";

    public ObservableCollection<ArchiveItem> DialogArchives { get; } = new();
    public ObservableCollection<ArchiveItem> ProjectArchives { get; } = new();

    private ArchiveItem? _selectedArchive;
    public ArchiveItem? SelectedArchive
    {
        get => _selectedArchive;
        set
        {
            if (_selectedArchive == value) return;
            _selectedArchive = value;
            OnPropertyChanged();
            LoadPreview();
        }
    }

    private string _previewContent = string.Empty;
    public string PreviewContent
    {
        get => _previewContent;
        set
        {
            if (_previewContent == value) return;
            _previewContent = value;
            OnPropertyChanged();
        }
    }

    public ICommand RestoreCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand RefreshCommand { get; }

    public ArchiveManagerViewModel()
    {
        RestoreCommand = new RelayCommand(RestoreAction);
        DeleteCommand = new RelayCommand(DeleteAction);
        RefreshCommand = new RelayCommand(Refresh);
        Refresh();
    }

    private void RestoreAction()
    {
        if (SelectedArchive == null) return;

        try
        {
            string targetFolder;
            if (SelectedArchive.Type == ArchiveType.Dialog)
            {
                targetFolder = DialogFileViewModel.SaveFolderPath;
            }
            else
            {
                targetFolder = ProjectViewModel.SaveFolderPath;
            }

            // Filename format from backup: Name_yyyyMMdd_HHmmss.ext
            
            var fileName = Path.GetFileNameWithoutExtension(SelectedArchive.FullPath);
            var extension = Path.GetExtension(SelectedArchive.FullPath);
            
            string targetFileName = fileName;
            
            // Try to strip timestamp if present
            if (fileName.Length > 16)
            {
                 string potentialTimestamp = fileName.Substring(fileName.Length - 15); // yyyyMMdd_HHmmss
                 if (potentialTimestamp[8] == '_') 
                 {
                     targetFileName = fileName.Substring(0, fileName.Length - 16);
                 }
            }
            // Also backup can handle repeated backup if name matches exactly?
            // Just basic stripping for now.

            string targetPath = Path.Combine(targetFolder, targetFileName + extension);
            
            File.Copy(SelectedArchive.FullPath, targetPath, true);
            MessageEventBus.Publish($"已还原 {targetFileName}");
        }
        catch(Exception e)
        {
             MessageEventBus.Publish($"还原失败: {e.Message}");
        }
    }

    private void DeleteAction()
    {
        if (SelectedArchive == null) return;
        try
        {
            File.Delete(SelectedArchive.FullPath);
            if (SelectedArchive.Type == ArchiveType.Dialog)
                DialogArchives.Remove(SelectedArchive);
            else
                ProjectArchives.Remove(SelectedArchive);
            
            SelectedArchive = null;
            PreviewContent = "";
             MessageEventBus.Publish("已删除存档");
        }
        catch(Exception e)
        {
             MessageEventBus.Publish($"删除失败: {e.Message}");
        }
    }

    private void LoadPreview()
    {
        if (SelectedArchive == null)
        {
            PreviewContent = "";
            return;
        }

        try
        {
            // Just read text. Maybe format JSON if possible?
            PreviewContent = File.ReadAllText(SelectedArchive.FullPath);
        }
        catch(Exception e)
        {
            PreviewContent = $"Error reading file: {e.Message}";
        }
    }

    private void Refresh()
    {
        DialogArchives.Clear();
        ProjectArchives.Clear();

        LoadArchives(Path.Combine("Archive", DialogArchive), ArchiveType.Dialog, DialogArchives);
        LoadArchives(Path.Combine("Archive", ProjectArchive), ArchiveType.Project, ProjectArchives);
    }

    private void LoadArchives(string path, ArchiveType type, ObservableCollection<ArchiveItem> collection)
    {
        try
        {
            var dir = Path.GetFullPath(path);
            if (!Directory.Exists(dir)) return;

            foreach (var file in Directory.GetFiles(dir))
            {
                var fi = new FileInfo(file);
                collection.Add(new ArchiveItem
                {
                    Name = fi.Name,
                    FullPath = fi.FullName,
                    Date = fi.CreationTime,
                    Type = type
                });
            }
        }
        catch
        {
            // ignore
        }
    }
}

public class ArchiveItem
{
    public string Name { get; set; } = "";
    public string FullPath { get; set; } = "";
    public DateTime Date { get; set; }
    public ArchiveType Type { get; set; }
}

public enum ArchiveType
{
    Dialog,
    Project
}
