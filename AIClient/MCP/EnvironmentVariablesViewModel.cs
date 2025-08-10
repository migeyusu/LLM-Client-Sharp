using System.Collections;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Xaml.Behaviors.Core;

namespace LLMClient.UI.MCP;

public class EnvironmentVariablesViewModel : BaseViewModel
{
    private bool _isSystemVariablesIncluded = true;

    // 用户环境变量列表 (可观察集合，用于UI自动更新)
    public ObservableCollection<VariableItem> UserVariables { get; }

    private IList<VariableItem>? _systemVariables;
    // 系统环境变量列表 (加载一次后不变)
    public IList<VariableItem>? SystemVariables
    {
        get => _systemVariables;
        set
        {
            if (Equals(value, _systemVariables)) return;
            _systemVariables = value;
            OnPropertyChangedAsync();
        }
    }

    private VariableItem? _selectedUserVariable;
    // 当前选中的用户变量
    public VariableItem? SelectedUserVariable
    {
        get => _selectedUserVariable;
        set
        {
            if (_selectedUserVariable != value)
            {
                _selectedUserVariable = value;
                OnPropertyChanged();
            }
        }
    }

    // 控制是否显示/启用系统环境变量
    public bool IsSystemVariablesIncluded
    {
        get => _isSystemVariablesIncluded;
        set
        {
            if (_isSystemVariablesIncluded != value)
            {
                _isSystemVariablesIncluded = value;
                OnPropertyChanged();
            }
        }
    }

    // 命令定义
    public ICommand AddUserVariableCommand { get; }
    public ICommand RemoveUserVariableCommand { get; }

    public ICommand PasteUserVariableCommand => new RelayCommand(() =>
    {
        var text = Clipboard.GetText();
        if (string.IsNullOrEmpty(text))
            return;
        // 假设剪贴板每项是 "Name=Value" 格式，使用分号分隔多项
        var items = text.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var item in items)
        {
            var parts = item.Split(new[] { '=' }, 2);
            if (parts.Length == 2)
            {
                var name = parts[0].Trim();
                var value = parts[1].Trim();
                if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(value))
                {
                    UserVariables.Add(new VariableItem { Name = name, Value = value });
                }
            }
        }
    });

    public ICommand CopyVariableCommand => new ActionCommand((o) =>
    {
        if (o is IList list)
        {
            var stringBuilder = new StringBuilder();
            foreach (var item in list.Cast<VariableItem>())
            {
                stringBuilder.AppendLine($"{item.Name}={item.Value}");
            }

            Clipboard.SetText(stringBuilder.ToString());
        }
    });

    public ICommand OkCommand { get; }
    public ICommand CancelCommand { get; }

    public EnvironmentVariablesViewModel()
    {
        // 初始化集合
        UserVariables = [];
        SystemVariables = [];

        // 初始化命令
        AddUserVariableCommand = new RelayCommand(AddUserVariable);
        RemoveUserVariableCommand = new RelayCommand(RemoveUserVariable);

        // 示例：为OK和Cancel命令绑定一个关闭窗口的委托
        // 在实际应用中，这通常由View层处理，或通过更高级的MVVM框架（如Prism的IDialogService）实现
        OkCommand = new ActionCommand(p => CloseWindow(p, true));
        CancelCommand = new ActionCommand(p => CloseWindow(p, false));
        // 加载数据
        LoadSystemVariables();
    }

    private async void LoadSystemVariables()
    {
        await Task.Run((() =>
        {
            var variables = Environment.GetEnvironmentVariables(EnvironmentVariableTarget.Machine)
                .Cast<DictionaryEntry>()
                .Concat(Environment.GetEnvironmentVariables(EnvironmentVariableTarget.User).Cast<DictionaryEntry>())
                .GroupBy(de => de.Key.ToString(), StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First());
            SystemVariables = variables.OrderBy(de => de.Key.ToString()).Select(entry => new VariableItem
                { Name = entry.Key.ToString(), Value = entry.Value?.ToString() }).ToArray();
        }));
    }

    private void AddUserVariable()
    {
        var newItem = new VariableItem { Name = "NEW_VARIABLE", Value = "Value" };
        UserVariables.Add(newItem);
        SelectedUserVariable = newItem; // 添加后自动选中
    }

    private void RemoveUserVariable()
    {
        if (SelectedUserVariable != null)
        {
            UserVariables.Remove(SelectedUserVariable);
        }
    }

    // 这个方法演示了如何通过命令关闭窗口
    private void CloseWindow(object parameter, bool dialogResult)
    {
        if (parameter is Window window)
        {
            try
            {
                window.DialogResult = dialogResult;
                window.Close();
            }
            catch (InvalidOperationException)
            {
                // 如果窗口不是以ShowDialog方式打开的，设置DialogResult会报错
                window.Close();
            }
        }
    }
}