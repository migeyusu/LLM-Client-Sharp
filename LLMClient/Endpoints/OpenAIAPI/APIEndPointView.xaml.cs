using System.Windows.Controls;
using System.Windows.Input;

namespace LLMClient.Endpoints.OpenAIAPI;

public partial class APIEndPointView : UserControl
{
    public APIEndPointView()
    {
        InitializeComponent();
    }

    private void MoveUp_OnExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        var apiEndPoint = this.DataContext as APIEndPoint;
        apiEndPoint?.MoveUp(e.Parameter as APIModelInfo ?? throw new InvalidOperationException());
    }

    private void PastModels_OnExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        var apiEndPoint = (APIEndPoint?)DataContext;
        apiEndPoint?.PastFromClipboard();
    }
}