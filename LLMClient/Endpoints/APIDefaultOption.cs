using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using LLMClient.Component.UserControls;
using LLMClient.Component.ViewModel.Base;
using LLMClient.Configuration;
using LLMClient.ToolCall;

namespace LLMClient.Endpoints;

public class APIDefaultOption : BaseViewModel<APIDefaultOption>
{
    public string APIToken
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    } = string.Empty;

    public string URL
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    } = string.Empty;

    public ProxySetting ProxySetting { get; set; } = new();

    public bool IsOpenAICompatible
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    } = true;

    public bool TreatNullChoicesAsEmptyResponse
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    }
    
    public Dictionary<string, string>? AdditionalHeaders
    {
        get;
        set
        {
            if (Equals(value, field)) return;
            field = value;
            OnPropertyChanged();
        }
    }
    
    public ICommand ConfigHeadersCommand => new RelayCommand(() =>
    {
        var envWindow = new KeyValueConfigWindow()
        {
            Title = "Http Headers",
            UserVariables = new ObservableCollection<VariableItem>(this.AdditionalHeaders?.Select(item =>
                new VariableItem()
                {
                    Name = item.Key,
                    Value = item.Value
                }) ?? []),
        };
        if (envWindow.ShowDialog() == true)
        {
            var userVariables = envWindow.UserVariables;
            if (userVariables.Any())
            {
                this.AdditionalHeaders = userVariables.ToDictionary(
                    item => item.Name!,
                    item => item.Value ?? string.Empty,
                    StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                this.AdditionalHeaders = null;
            }
        }
    });
}