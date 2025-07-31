using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;

namespace LLMClient.UI;

public partial class DataTemplateDictionary : ResourceDictionary
{
    private void Hyperlink_OnRequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        // for .NET Core you need to add UseShellExecute = true
        // see https://learn.microsoft.com/dotnet/api/system.diagnostics.processstartinfo.useshellexecute#property-value
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }
}