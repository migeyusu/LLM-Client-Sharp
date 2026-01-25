using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using LLMClient.Abstraction;
using LLMClient.Component.ViewModel;

namespace LLMClient.Component;

public partial class DataTemplateDictionary : ResourceDictionary
{
    private void Hyperlink_OnRequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        // for .NET Core you need to add UseShellExecute = true
        // see https://learn.microsoft.com/dotnet/api/system.diagnostics.processstartinfo.useshellexecute#property-value
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }
    

    private void ResearchClientsSelector_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // BaseViewModel.ServiceLocator.GetService<>()
    }

    private void TreeView_OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        var newValue = e.NewValue;
        if (newValue is IEndpointModel modelInfo)
        {
            ((BaseModelSelectionViewModel)((TreeView)sender).DataContext).SelectedModel = modelInfo;
        }
    }
}