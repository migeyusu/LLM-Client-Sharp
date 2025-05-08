using System.Reflection;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LLMClient.UI;

namespace LLMClient.Endpoints.Azure;

public abstract class AzureEndPointBase : BaseViewModel, ILLMEndpoint
{
    private AzureOption _option = new AzureOption();
    
    public AzureOption Option
    {
        get => _option;
        protected set
        {
            if (Equals(value, _option)) return;
            _option = value;
            OnPropertyChanged();
        }
    }

    public virtual string DisplayName
    {
        get { return Name; }
    }

    public abstract string Name { get; }

    private static readonly Lazy<ImageSource> _source = new Lazy<ImageSource>((() =>
    {
        var bitmapImage = new BitmapImage(new Uri(@"pack://application:,,,/"
                                                  + Assembly.GetExecutingAssembly().GetName().Name
                                                  + ";component/"
                                                  + "Resources/Images/azure-icon.png", UriKind.Absolute));
        bitmapImage.Freeze();
        return bitmapImage;
    }));

    public virtual ImageSource? Icon
    {
        get { return _source.Value; }
    }

    public abstract IList<string> AvailableModelNames { get; }

    public abstract ILLMModelClient? NewClient(string modelName);

    public abstract Task InitializeAsync();
}