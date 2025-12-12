using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using LLMClient.Abstraction;
using LLMClient.Component.CustomControl;
using LLMClient.Component.ViewModel.Base;
using Microsoft.Extensions.Logging;
using Microsoft.Xaml.Behaviors.Core;

namespace LLMClient.Endpoints.OpenAIAPI;

public class APIEndPoint : NotifyDataErrorInfoViewModelBase, ILLMAPIEndpoint
{
    public const string KeyName = "OpenAIAPICompatible";

    public APIEndPointOption Option { get; }

    public string DisplayName => Option.DisplayName;
    public virtual bool IsInbuilt => false;

    public bool IsEnabled => true;

    public string Name => Option.Name;

    public ThemedIcon Icon => Option.Icon;

    private int _selectedModelIndex = -1;

    public int SelectedModelIndex
    {
        get => _selectedModelIndex;
        set
        {
            if (value == _selectedModelIndex) return;
            _selectedModelIndex = value;
            OnPropertyChanged();
        }
    }

    private ObservableCollection<APIModelInfo> Models => Option.Models;

    private const string NewModelName = "测试名称";

    public ICommand AddNewCommand => new ActionCommand(o => { AddNewModel(); });

    private void AddNewModel(APIModelInfo? modelInfo = null)
    {
        string newModelName;
        int v = 0;
        if (modelInfo == null)
        {
            modelInfo = new APIModelInfo();
            newModelName = NewModelName + v;
        }
        else
        {
            newModelName = modelInfo.Name;
        }

        while (Models.Any(info => info.Name == newModelName))
        {
            v++;
            newModelName = modelInfo.Name + v;
        }

        modelInfo.Name = newModelName;
        modelInfo.Endpoint = this;

        if (SelectedModelIndex > -1)
        {
            Models.Insert(SelectedModelIndex + 1, modelInfo);
        }
        else
        {
            Models.Add(modelInfo);
        }

        SelectedModelIndex = Models.IndexOf(modelInfo);
        OnPropertyChanged(nameof(AvailableModels));
    }

    public const string CopyFormat = "LMClient.APIEndPoint.Model";

    public static void CopyToClipboard(APIModelInfo modelInfo)
    {
        string serialize = JsonSerializer.Serialize(modelInfo, Extension.DefaultJsonSerializerOptions);
        var dataObject = new DataObject();
        dataObject.SetData(CopyFormat, serialize);
        Clipboard.SetDataObject(dataObject, true);
    }

    public ICommand PastCommand => new ActionCommand(o =>
    {
        try
        {
            PastFromClipboard();
        }
        catch (Exception e)
        {
            MessageBox.Show("无法粘贴，出现错误:" + e.Message);
        }
    });

    public void PastFromClipboard()
    {
        if (Clipboard.GetDataObject() is DataObject dataObject)
        {
            if (dataObject.GetDataPresent(CopyFormat))
            {
                var serialize = dataObject.GetData(CopyFormat) as string;
                if (!string.IsNullOrEmpty(serialize))
                {
                    try
                    {
                        var modelInfo =
                            JsonSerializer.Deserialize<APIModelInfo>(serialize, Extension.DefaultJsonSerializerOptions);
                        AddNewModel(modelInfo);
                    }
                    catch (Exception e)
                    {
                        MessageBox.Show("无法粘贴，数据格式错误:" + e.Message);
                    }
                }
            }
        }
    }

    public ICommand RemoveCommand => new ActionCommand(o =>
    {
        Models.Remove((APIModelInfo)o);
        OnPropertyChanged(nameof(AvailableModels));
    });

    public void MoveUp(APIModelInfo modelInfo)
    {
        int index = Models.IndexOf(modelInfo);
        if (index > 0)
        {
            Models.Move(index, index - 1);
            SelectedModelIndex = index - 1;
        }
    }

    private readonly ILoggerFactory _loggerFactory;

    public APIEndPoint(APIEndPointOption option, ILoggerFactory loggerFactory)
    {
        Option = option;
        option.PropertyChanged += OptionOnPropertyChanged;
        this._loggerFactory = loggerFactory;
    }

    private void OptionOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Option.DisplayName))
        {
            OnPropertyChanged(nameof(DisplayName));
        }
    }

    public IReadOnlyCollection<ILLMModel> AvailableModels
    {
        get { return this.Option.Models; }
    }

    public ILLMChatClient? NewChatClient(ILLMModel model)
    {
        if (model is APIModelInfo apiModelInfo)
        {
            return new APIClient(this, apiModelInfo, Option.ConfigOption, _loggerFactory);
        }

        return null;
    }

    public ILLMModel? GetModel(string modelName)
    {
        return Option.Models.FirstOrDefault(x => x.Name == modelName);
    }

    public Task InitializeAsync()
    {
        foreach (var model in Option.Models)
        {
            model.Endpoint = this;
        }

        return Task.CompletedTask;
        /*var path = Path.GetFullPath(Path.Combine("EndPoints", "Compatible", "Models", "models.json"));

        new FileInfo(path);*/
    }
}