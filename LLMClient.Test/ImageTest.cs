using Xunit.Abstractions;

namespace LLMClient.Test;

public class ImageTest
{
    private ITestOutputHelper _output;

    public ImageTest(ITestOutputHelper output)
    {
        this._output = output;
    }

    [Fact]
    public void Uri()
    {
        //base64:image/png;base64,iVBORw0KGgoAAAANSUhEUgAA...
    }
}