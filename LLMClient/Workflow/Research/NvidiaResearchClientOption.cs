using LLMClient.Abstraction;
using LLMClient.Component.CustomControl;
using LLMClient.Component.ViewModel.Base;
using LLMClient.Configuration;
using LLMClient.Data;
using LLMClient.Endpoints;

namespace LLMClient.Workflow.Research;

public class NvidiaResearchClientOption : BaseViewModel, IResearchCreationOption
{
    public IParameterizedLLMModel? PromptModel { get; set; }

    public IParameterizedLLMModel? ReportModel { get; set; }

    private readonly GlobalOptions _options;

    public NvidiaResearchClientOption(GlobalOptions options)
    {
        _options = options;
    }

    public string DisplayName { get; } = "Nvidia Research";

    public ThemedIcon Icon { get; } = ImageExtensions.GetIcon(ModelIconType.Nvidia);

    public ResearchClient CreateResearchClient()
    {
        if (PromptModel == null)
        {
            throw new InvalidOperationException("Prompt model is not set.");
        }

        if (ReportModel == null)
        {
            throw new InvalidOperationException("Report model is not set.");
        }

        return new NvidiaResearchClient(PromptModel, ReportModel, _options);
    }
}