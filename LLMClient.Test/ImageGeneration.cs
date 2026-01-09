using System.ClientModel;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using Xunit.Abstractions;

namespace LLMClient.Test;

public class ImageGeneration
{
    private ITestOutputHelper _output;

    public ImageGeneration(ITestOutputHelper output)
    {
        this._output = output;
    }

    [Fact]
    public async Task CompletionMethod()
    {
        var openAiClient = new OpenAIClient(new ApiKeyCredential("sk-xxxxxx"),
            new OpenAIClientOptions() { Endpoint = new Uri("https://api.openai.com/") });
        var chatClient = openAiClient.GetChatClient("gemini-3-pro-image-preview");
        var result = await chatClient.CompleteChatAsync(new []{new UserChatMessage("绘制一幅虎王克坦的解剖图片，色彩鲜艳，细节丰富")});
        var chatCompletion = result.Value;
        var chatCompletionContent = chatCompletion.Content; 
        
        /*var imageClient = openAiClient.GetImageClient("jimeng-4.0");
        // var asIImageGenerator = imageClient.AsIImageGenerator();
        var generateImageAsync = await imageClient.GenerateImageAsync("A cute baby sea otter wearing a beret and glasses, sitting at a desk with a laptop and a cup of coffee, digital art");
        var generatedImage = generateImageAsync.Value;
        using FileStream stream = File.OpenWrite($"{Guid.NewGuid()}.png");
        await generatedImage.ImageBytes.ToStream().CopyToAsync(stream);*/
        // Generate an image from a text prompt
        /*var options = new ImageGenerationOptions
        {
            MediaType = "image/png"
        };
        string prompt = "A tennis court in a jungle";
        var response = await generator.GenerateImagesAsync(prompt, options);

// Save the image to a file.
        var dataContent = response.Contents.OfType<DataContent>().First();
        string fileName = SaveImage(dataContent, "jungle-tennis.png");
        Console.WriteLine($"Image saved to file: {fileName}");*/

        
    }
    static string SaveImage(DataContent content, string fileName)
    {
        string userDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var path = Path.Combine(userDirectory, fileName);
        File.WriteAllBytes(path, content.Data.ToArray());
        return Path.GetFullPath(path);
    }
}