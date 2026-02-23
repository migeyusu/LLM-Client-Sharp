namespace LLMClient.ContextEngineering.Tools;

public interface IProjectAwarenessService
{
    SolutionInfoView GetSolutionInfoView();
    ProjectMetadataView GetProjectMetadataView(string nameOrId);
    string GetFileTree(string relativeRootPath, int maxDepth, ICollection<string>? excludePatterns);
    FileMetadataView GetFileMetadata(string pathInput);
    ConventionView DetectConventions();
    List<RecentFileView> GetRecentlyModifiedFiles(DateTime? sinceUtc = null, int count = 30);
}
