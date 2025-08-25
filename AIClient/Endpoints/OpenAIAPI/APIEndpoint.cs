using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Media;
using LLMClient.Abstraction;
using LLMClient.UI;
using Microsoft.Extensions.Logging;
using Microsoft.Xaml.Behaviors.Core;

namespace LLMClient.Endpoints.OpenAIAPI;

public class APIEndPoint : NotifyDataErrorInfoViewModelBase, ILLMEndpoint
{
    public const string KeyName = "OpenAIAPICompatible";

    public APIEndPointOption Option { get; }

    public string DisplayName => Option.DisplayName;
    public virtual bool IsInbuilt => false;

    public bool IsEnabled => true;

    public string Name => Option.Name;

    public ImageSource Icon => Option.Icon;

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

    public ICommand AddNewCommand => new ActionCommand((o =>
    {
        int v = 0;
        var newModelName = NewModelName + v;
        while (Models.Any(info => info.Name == newModelName))
        {
            v++;
            newModelName = NewModelName + v;
        }

        var apiModelInfo = new APIModelInfo() { Name = newModelName, Endpoint = this };
        if (SelectedModelIndex > -1)
        {
            Models.Insert(SelectedModelIndex + 1, apiModelInfo);
        }
        else
        {
            Models.Add(apiModelInfo);
        }

        SelectedModelIndex = Models.IndexOf(apiModelInfo);
        OnPropertyChanged(nameof(AvailableModels));
    }));

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
        this._loggerFactory = loggerFactory;
    }


    public IReadOnlyCollection<ILLMChatModel> AvailableModels
    {
        get { return this.Option.Models; }
    }

    public ILLMChatClient? NewChatClient(string modelName)
    {
        var apiModelInfo = Option.Models.FirstOrDefault(x => x.Name == modelName);
        if (apiModelInfo == null)
            return null;
        return new APIClient(this, apiModelInfo, Option.ConfigOption, _loggerFactory);
    }

    public ILLMChatClient? NewChatClient(ILLMChatModel model)
    {
        if (model is APIModelInfo apiModelInfo)
        {
            return new APIClient(this, apiModelInfo, Option.ConfigOption, _loggerFactory);
        }

        return null;
    }

    public ILLMChatModel? GetModel(string modelName)
    {
        return Option.Models.FirstOrDefault(x => x.Name == modelName);
    }

    public Task InitializeAsync()
    {
        foreach (var model in Option.Models)
        {
            model.Endpoint = this;
        }

        Option.UpdateIcon();
        return Task.CompletedTask;
        /*var path = Path.GetFullPath(Path.Combine("EndPoints", "Compatible", "Models", "models.json"));

        new FileInfo(path);*/
    }
}