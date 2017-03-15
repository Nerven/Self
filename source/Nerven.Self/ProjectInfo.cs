namespace Nerven.Self
{
    public sealed class ProjectInfo
    {
        public ProjectInfo(
            ExtraProjectInfo extra,
            GitHubRepositoryInfo gitHub)
        {
            Extra = extra;
            GitHub = gitHub;
        }

        public ExtraProjectInfo Extra { get; }
        public GitHubRepositoryInfo GitHub { get; }
    }
}
