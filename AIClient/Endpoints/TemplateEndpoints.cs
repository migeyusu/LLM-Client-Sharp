using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LLMClient.Abstraction;
using LLMClient.Endpoints.OpenAIAPI;
using LLMClient.UI;
using Microsoft.Xaml.Behaviors.Core;

namespace LLMClient.Endpoints;

public class TemplateEndpoints : BaseViewModel, ILLMEndpoint
{
    public const string KeyName = "TemplateEndpoints";

    [JsonIgnore]
    public string DisplayName
    {
        get { return Name; }
    }

    [JsonIgnore] public string Name { get; } = "Templates";


    private static readonly Lazy<ImageSource> Source = new Lazy<ImageSource>((() =>
    {
        var bitmapImage = new BitmapImage(new Uri(
            @"pack://application:,,,/LLMClient;component/Resources/Images/Document-New-32.png",
            UriKind.Absolute));
        bitmapImage.Freeze();
        DebugEx.PrintThreadId();
        return bitmapImage;
    }));

    [JsonIgnore]
    public ImageSource Icon
    {
        get { return Source.Value; }
    }

    public ObservableCollection<APIModelInfo> TemplateModels { get; set; } = new ObservableCollection<APIModelInfo>();

    [JsonIgnore]
    public IList<string> AvailableModelNames
    {
        get { return TemplateModels.Select((info => info.Name)).ToArray(); }
    }

    private const string NewModelName = "测试模板";

    [JsonIgnore]
    public ICommand AddNewCommand =>
        new ActionCommand((o =>
        {
            int v = 0;
            var newModelName = NewModelName + v;
            while (TemplateModels.Any((info => info.Name == newModelName)))
            {
                v++;
                newModelName = NewModelName + v;
            }

            TemplateModels.Add(new APIModelInfo() { Name = newModelName, Endpoint = this });
        }));

    [JsonIgnore] public ICommand RemoveCommand => new ActionCommand((o => { TemplateModels.Remove((APIModelInfo)o); }));

    public ILLMModelClient? NewClient(string modelName)
    {
        throw new NotSupportedException("Template endpoints does not support creating new clients.");
    }

    public ILLMModel? GetModel(string modelName)
    {
        throw new NotSupportedException();
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public static TemplateEndpoints LoadOrCreate(JsonObject document)
    {
        if (document.TryGetPropertyValue(KeyName, out var jsonNode))
        {
            var endpoints = jsonNode.Deserialize<TemplateEndpoints>();
            if (endpoints != null)
            {
                return endpoints;
            }
        }

        return new TemplateEndpoints();
    }
}