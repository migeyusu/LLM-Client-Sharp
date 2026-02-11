using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using AutoMapper;
using LLMClient.Abstraction;
using LLMClient.Component.ViewModel;

namespace LLMClient.Research;

public class ResearchSession : FileBasedSessionBase, ILLMSessionLoader<ResearchSession>
{
    public ResearchClient ResearchClient { get; set; }

    public ResearchSession(ResearchClient researchClient) : base()
    {
        ResearchClient = researchClient;
    }

    public override bool IsDataChanged { get; set; }

    public override bool IsBusy
    {
        get { return ResearchClient.IsResponding; }
    }

    public const string SaveFolder = "Research";

    private static readonly Lazy<string> SaveFolderPathLazy = new Lazy<string>((() => Path.GetFullPath(SaveFolder)));

    public static string SaveFolderPath => SaveFolderPathLazy.Value;

    protected override string DefaultSaveFolderPath
    {
        get { return SaveFolderPathLazy.Value; }
    }

    public override object Clone()
    {
        throw new NotImplementedException();
    }

    public override ILLMSession CloneHeader()
    {
        throw new NotImplementedException();
    }

    protected override Task SaveToStream(Stream stream)
    {
        throw new NotImplementedException();
    }

    public static Task<ResearchSession?> LoadFromStream(Stream stream, IMapper mapper)
    {
        //stream to json node
        /*var async = JsonDocument.ParseAsync();
        JsonNode node;*/
        throw new NotImplementedException();
    }
}