using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using Microsoft.Xaml.Behaviors.Core;

namespace LLMClient.ToolCall;

public partial class KeyValueConfigWindow : Window, INotifyPropertyChanged
{
    public ObservableCollection<VariableItem> UserVariables
    {
        get => _userVariables;
        set
        {
            if (Equals(value, _userVariables)) return;
            _userVariables = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(AddUserVariableCommand));
            OnPropertyChanged(nameof(RemoveUserVariableCommand));
            OnPropertyChanged(nameof(PasteUserVariableCommand));
        }
    }

    private VariableItem? _selectedUserVariable;
    private ObservableCollection<VariableItem> _userVariables = [];

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

    public ICommand AddUserVariableCommand => new ActionCommand(o =>
    {
        var newItem = new VariableItem { Name = "NEW_VARIABLE", Value = "Value" };
        UserVariables.Add(newItem);
        SelectedUserVariable = newItem; // 添加后自动选中
    });

    public ICommand RemoveUserVariableCommand => new ActionCommand(o =>
    {
        if (SelectedUserVariable != null)
        {
            UserVariables.Remove(SelectedUserVariable);
        }
    });

    public ICommand PasteUserVariableCommand => new ActionCommand((o) =>
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

    public KeyValueConfigWindow()
    {
        DataContext = this;
        InitializeComponent();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void OK_OnClick(object sender, RoutedEventArgs e)
    {
        if (UserVariables.DistinctBy((item => item.Name)).Count() < UserVariables.Count())
        {
            MessageBox.Show("Variable must have unique names.", "Error", MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        if (UserVariables.Any((item => string.IsNullOrWhiteSpace(item.Name))))
        {
            MessageBox.Show("Variable must have non-empty names.", "Error", MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        this.DialogResult = true;
        this.Close();
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        this.DialogResult = false;
        this.Close();
    }
}