namespace LLMClient.Test;

public class UITest
{
    [Fact]
    public void Run()
    {
        var thread = new Thread(o =>
        {
            var app = new App();
            app.InitializeComponent();
            app.Run(new TestWindow());    
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
    } 
}