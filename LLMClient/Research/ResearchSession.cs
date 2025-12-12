using System.IO;
using LLMClient.Component.ViewModel;

namespace LLMClient.Research;

public class ResearchSession : FileBasedSessionBase
{
    public ResearchClient ResearchClient { get; set; }
    
    public ResearchSession(ResearchClient researchClient)
        : base()
    {
        ResearchClient = researchClient;
    }

    public override bool IsDataChanged { get; set; }
    public override bool IsBusy { get; }
    protected override string DefaultSaveFolderPath { get; }
    
    public override object Clone()
    {
        throw new NotImplementedException();
    }

    protected override Task SaveToStream(Stream stream)
    {
        throw new NotImplementedException();
    }
}