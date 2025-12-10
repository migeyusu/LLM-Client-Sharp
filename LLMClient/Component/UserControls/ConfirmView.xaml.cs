namespace LLMClient.Component.UserControls;

public partial class ConfirmView
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