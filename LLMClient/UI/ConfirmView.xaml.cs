using System.Windows.Controls;

namespace LLMClient.UI;

public partial class ConfirmView : UserControl
{
    public ConfirmView()
    {
        InitializeComponent();
    }

    public string Header
    {
        get { return HeaderTextBlock.Text; }
        set => HeaderTextBlock.Text = value;
    }
}