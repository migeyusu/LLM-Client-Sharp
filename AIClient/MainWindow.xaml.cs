// #define TESTMODE

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Azure;
using Azure.AI.Inference;
using LLMClient.Azure;
using Markdig;
using Markdig.Renderers;
using Markdig.Wpf;
using MaterialDesignThemes.Wpf;
using Microsoft.Xaml.Behaviors.Core;
using SkiaSharp;
using Svg.Skia;


namespace LLMClient;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _mainViewModel = new();

    public MainWindow()
    {
        this.DataContext = _mainViewModel;
        InitializeComponent();
    }

    private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        _mainViewModel.Initialize();
    }
}

public class MainViewModel : BaseViewModel
{
    public ICommand SelectModelCommand => new ActionCommand((async o =>
    {
        var selectionViewModel = new ModelSelectionViewModel() { AvailableEndpoints = _endpoints };
        if (await DialogHost.Show(selectionViewModel) is true)
        {
            var model = selectionViewModel.SelectedEndpoint!.GetModel(selectionViewModel.SelectedModelId!);
            if (model == null)
            {
                MessageBox.Show("No model created!");
                return;
            }

            var dialogViewModel = new DialogViewModel(model);
            PreDialog = dialogViewModel;
            this.DialogViewModels.Add(dialogViewModel);
        }
    }));

    public ObservableCollection<DialogViewModel> DialogViewModels { get; set; } =
        new ObservableCollection<DialogViewModel>();

    public DialogViewModel? PreDialog
    {
        get => _preDialog;
        set
        {
            if (Equals(value, _preDialog)) return;
            _preDialog = value;
            OnPropertyChanged();
        }
    }

    readonly List<ILLMEndpoint> _endpoints = new();
    private DialogViewModel? _preDialog;

    public async void Initialize()
    {
        var azureOption = new AzureOption();
        await azureOption.Initialize();
        _endpoints.Add(azureOption);
    }
}