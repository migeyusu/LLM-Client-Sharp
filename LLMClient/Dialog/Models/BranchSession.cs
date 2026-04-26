using LLMClient.Abstraction;
using LLMClient.Agent.MiniSWE;
using Microsoft.Agents.AI;

namespace LLMClient.Dialog.Models;

/// <summary>
/// 為父對話項產生的分支
/// </summary>
public class BranchSession : ISession
{
    protected ISession ParentSession { get; }

    protected BranchSession(ISession parentSession, IResponseItem responseItem)
    {
        this.ParentSession = parentSession;
        WorkingResponse = responseItem;
    }

    public static BranchSession CreateFromResponse(IResponseItem responseItem)
    {
        switch (responseItem.Session)
        {
            case IProjectSession projectSession:
                return new BranchProject(projectSession, responseItem);
            case ISession session:
                return new BranchSession(session, responseItem);
            default:
                throw new ArgumentException($"Unknown session type: {responseItem.Session}");
        }
    }

    public Guid ID => ParentSession.ID;

    public IResponseItem WorkingResponse { get; }

    public AIContextProvider[]? ContextProviders => ParentSession.ContextProviders;

    public string? SystemPrompt => ParentSession.SystemPrompt;
}

public class BranchProject : BranchSession, IProjectSession
{
    private readonly IProjectSession _parentProject;

    public BranchProject(IProjectSession parentSession, IResponseItem responseItem) : base(parentSession, responseItem)
    {
        _parentProject = parentSession;
    }

    public string? WorkingDirectory
    {
        get { return _parentProject.WorkingDirectory; }
    }

    public RunPlatform Platform
    {
        get { return _parentProject.Platform; }
    }

    public string ProjectInformationPrompt
    {
        get
        {
            return _parentProject.ProjectInformationPrompt;
        }
    }

    public IAIFunctionGroup[] ProjectTools
    {
        get
        {
            return _parentProject.ProjectTools;
        }
    }
}