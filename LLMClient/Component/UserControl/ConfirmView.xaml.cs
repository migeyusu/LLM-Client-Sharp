namespace LLMClient.Component.UserControl;

public partial class ConfirmView : System.Windows.Controls.UserControl
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