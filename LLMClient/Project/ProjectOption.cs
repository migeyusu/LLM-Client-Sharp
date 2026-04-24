using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using LLMClient.Component.CustomControl;
using LLMClient.Component.Utility;
using LLMClient.Component.ViewModel.Base;
using LLMClient.Dialog;
using MaterialDesignThemes.Wpf;
using Microsoft.Xaml.Behaviors.Core;

namespace LLMClient.Project;

public class ProjectOption : NotifyDataErrorInfoViewModelBase, ICloneable
{
    public string? Name
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    }


    /// <summary>
    /// sample:this is a *** project
    /// </summary>
    public string? Description
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    } = string.Empty;

    /// <summary>
    /// 会自动搜索skills
    /// </summary>
    public bool EnableSkills
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    } = true;

    public ICommand SelectProjectFolderCommand => new RelayCommand(() =>
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "请选择项目文件夹",
            SelectedPath = string.IsNullOrEmpty(RootPath)
                ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                : RootPath
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            RootPath = dialog.SelectedPath;
        }
    });

    public ICommand AddAllowedFolderPathsCommand => new RelayCommand(() =>
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "请选择允许的文件夹路径",
            SelectedPath = string.IsNullOrEmpty(RootPath)
                ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                : RootPath
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

    /// <summary>
    /// 项目路径，项目所在文件夹路径
    /// </summary>
    public string? RootPath
    {
        get;
        set
        {
            if (value == field) return;
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

            if (!string.IsNullOrEmpty(field))
            {
                AllowedFolderPaths.Remove(field);
            }

            field = value;
            OnPropertyChanged();
            if (!AllowedFolderPaths.Contains(value))
            {
                AllowedFolderPaths.Add(value);
            }

            OnPropertyChanged(nameof(PromptInjector));
        }
    }

    public bool TypeEditable { get; set; } = false;

    private ProjectType _type = ProjectType.CSharp;

    public bool IncludeAgentsMd
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    } = true;

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

    public bool IncludeCopilotPrompt
    {
        get;
        set
        {
            if (value == field) return;
            OnPropertyChanged();
            field = value;
            OnPropertyChanged(nameof(PromptInjector));
        }
    } = true;


    public OpenSpecPromptInjector? PromptInjector
    {
        get
        {
            if (!IncludeCopilotPrompt || string.IsNullOrEmpty(RootPath))
            {
                return null;
            }

            if (field?.ProjectRoot != RootPath)
            {
                field = new OpenSpecPromptInjector(RootPath);
            }

            return field;
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

    [MemberNotNullWhen(true, nameof(Name), nameof(RootPath))]
    public bool Check()
    {
        if (this.HasErrors)
        {
            return false;
        }

        if (string.IsNullOrEmpty(Name))
        {
            this.AddError("Name cannot be null or empty.", nameof(Name));
            return false;
        }

        if (string.IsNullOrEmpty(RootPath))
        {
            this.AddError("FolderPath cannot be null or empty.", nameof(RootPath));
            return false;
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
            RootPath = this.RootPath,
            AllowedFolderPaths = new ObservableCollection<string>(this.AllowedFolderPaths.ToArray()),
            Name = this.Name,
            Type = this.Type,
            IncludeAgentsMd = this.IncludeAgentsMd,
        };
    }
}