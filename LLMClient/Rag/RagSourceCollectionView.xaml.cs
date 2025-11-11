using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace LLMClient.Rag;

public partial class RagSourceCollectionView : UserControl
{
    public RagSourceCollectionView()
    {
        InitializeComponent();
    }

    private void Hyperlink_OnRequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }
}