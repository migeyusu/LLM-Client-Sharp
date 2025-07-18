﻿using System.Windows.Media;
using System.Windows.Media.Imaging;
using LLMClient.Abstraction;
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

    public abstract bool IsDefault { get; }
    
    public bool IsEnabled { get; } = true;

    public abstract string Name { get; }

    private static readonly Lazy<ImageSource> Source = new Lazy<ImageSource>((() =>
    {
        var bitmapImage = new BitmapImage(new Uri(
            @"pack://application:,,,/LLMClient;component/Resources/Images/azure-icon.png"
            , UriKind.Absolute));
        bitmapImage.Freeze();
        return bitmapImage;
    }));

    public virtual ImageSource Icon
    {
        get { return Source.Value; }
    }

    public abstract IReadOnlyCollection<string> AvailableModelNames { get; }

    public abstract IReadOnlyCollection<ILLMModel> AvailableModels { get; }

    public abstract ILLMClient? NewClient(string modelName);
    public abstract ILLMClient? NewClient(ILLMModel model);

    public abstract ILLMModel? GetModel(string modelName);

    public abstract Task InitializeAsync();
}