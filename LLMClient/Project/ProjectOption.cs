using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using LLMClient.Component.CustomControl;
using LLMClient.Component.Utility;
using LLMClient.Component.ViewModel.Base;
using MaterialDesignThemes.Wpf;
using Microsoft.Xaml.Behaviors.Core;

namespace LLMClient.Project;

public class ProjectOption : NotifyDataErrorInfoViewModelBase, ICloneable
{
    private string? _name;

    public string? Name
    {
        get => _name;
        set
        {
            if (value == _name) return;
            _name = value;
            OnPropertyChanged();
        }
    }


    private string? _description = string.Empty;

    /// <summary>
    /// sample:this is a *** project
    /// </summary>
    public string? Description
    {
        get => _description;
        set
        {
            if (value == _description) return;
            this.ClearError();
            if (string.IsNullOrEmpty(value))
            {
                this.AddError("Description cannot be null or empty.");
                return;
            }

            _description = value;
            OnPropertyChanged();
        }
    }


    public ICommand SelectProjectFolderCommand => new RelayCommand(() =>
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "请选择项目文件夹",
            SelectedPath = string.IsNullOrEmpty(FolderPath)
                ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                : FolderPath
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            FolderPath = dialog.SelectedPath;
        }
    });

    public ICommand AddAllowedFolderPathsCommand => new RelayCommand(() =>
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "请选择允许的文件夹路径",
            SelectedPath = string.IsNullOrEmpty(FolderPath)
                ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                : FolderPath
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            var selectedPath = dialog.SelectedPath;
            if (!AllowedFolderPaths.Contains(selectedPath))
            {
                AllowedFolderPaths.Add(selectedPath);
            }
        }
    });

    public ICommand RemoveAllowedFolderPathCommand => new ActionCommand((o) =>
    {
        if (o is string s)
        {
            AllowedFolderPaths.Remove(s);
        }
    });

    public ObservableCollection<string> AllowedFolderPaths { get; set; } = new();

    private string? _folderPath;

    /// <summary>
    /// 项目路径，项目所在文件夹路径
    /// </summary>
    public string? FolderPath
    {
        get => _folderPath;
        set
        {
            if (value == _folderPath) return;
            this.ClearError();
            if (string.IsNullOrEmpty(value))
            {
                this.AddError("FolderPath cannot be null or empty.");
                return;
            }

            if (!Directory.Exists(value))
            {
                this.AddError("FolderPath does not exist.");
            }

            if (!string.IsNullOrEmpty(_folderPath))
            {
                AllowedFolderPaths.Remove(_folderPath);
            }

            _folderPath = value;
            if (!AllowedFolderPaths.Contains(value))
            {
                AllowedFolderPaths.Add(value);
            }
            OnPropertyChanged();
        }
    }

    public bool TypeEditable { get; set; } = false;

    private ProjectType _type;

    public ProjectType Type
    {
        get => _type;
        set
        {
            if (value == _type) return;
            _type = value;
            OnPropertyChanged();
        }
    }


    private static ThemedIcon CSharpIcon => LocalThemedIcon.FromPackIcon(PackIconKind.LanguageCsharp);

    private static ThemedIcon CppIcon => LocalThemedIcon.FromPackIcon(PackIconKind.LanguageCpp);

    private static ThemedIcon DefaultIcon => LocalThemedIcon.FromPackIcon(PackIconKind.CodeTags);

    public ThemedIcon Icon
    {
        get => _type switch
        {
            ProjectType.CSharp => CSharpIcon,
            ProjectType.Cpp => CppIcon,
            _ => DefaultIcon,
        };
    }

    [MemberNotNullWhen(true, nameof(Name), nameof(FolderPath), nameof(Description))]
    public bool Check()
    {
        if (this.HasErrors)
        {
            return false;
        }

        if (string.IsNullOrEmpty(Name))
        {
            this.AddError("Name cannot be null or empty.", nameof(Name));
        }

        if (string.IsNullOrEmpty(FolderPath))
        {
            this.AddError("FolderPath cannot be null or empty.", nameof(FolderPath));
        }

        if (string.IsNullOrEmpty(Description))
        {
            this.AddError("Description cannot be null or empty.", nameof(Description));
        }

        if (!AllowedFolderPaths.Any())
        {
            MessageEventBus.Publish("AllowedFolderPaths cannot be empty.");
            return false;
        }

        return true;
    }

    public object Clone()
    {
        return new ProjectOption()
        {
            Description = this.Description,
            FolderPath = this.FolderPath,
            AllowedFolderPaths = new ObservableCollection<string>(this.AllowedFolderPaths.ToArray()),
            Name = this.Name,
            Type = this.Type,
        };
    }
}