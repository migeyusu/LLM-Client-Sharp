using System.Collections;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LLMClient.UI.ViewModel.Base;

/// <summary>
/// 用于异常检查的viewmodel
/// </summary>
public class NotifyDataErrorInfoViewModelBase : BaseViewModel, INotifyDataErrorInfo
{
    public IEnumerable GetErrors(string? propertyName)
    {
        if (string.IsNullOrEmpty(propertyName))
        {
            return Enumerable.Empty<string>();
        }

        if (_errorDictionary.TryGetValue(propertyName, out var value))
        {
            return value.ToArray();
        }

        return Enumerable.Empty<string>();
    }


    public bool HasErrors => _errorDictionary.Any();

    private readonly IDictionary<string, List<string>> _errorDictionary = new Dictionary<string, List<string>>();
    
    public void ClearErrors()
    {
        var propertyNames = _errorDictionary.Keys.ToArray();
        foreach (var propertyName in propertyNames)
        {
            _errorDictionary.Remove(propertyName);
            OnErrorsChanged(propertyName);
        }
    }

    public void ClearError([CallerMemberName] string? propertyName = null)
    {
        if (propertyName == null)
        {
            return;
        }

        _errorDictionary.Remove(propertyName);
        OnErrorsChanged(propertyName);
    }

    public virtual void AddError(string error, [CallerMemberName] string? propertyName = null)
    {
        if (string.IsNullOrEmpty(propertyName))
        {
            return;
        }

        if (!_errorDictionary.TryGetValue(propertyName, out var list))
        {
            list = new List<string>();
            _errorDictionary.Add(propertyName, list);
        }

        list.Add(error);
        OnErrorsChanged(propertyName);
    }

    public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

    protected virtual void OnErrorsChanged(string propertyName)
    {
        ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
    }
    
}