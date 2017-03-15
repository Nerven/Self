//// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Nerven.Self
{
    public sealed class GitHubRepositoryInfo
    {
        public GitHubRepositoryInfo(string ownerName, string fullName, string name, string description, string htmlUrl, string cloneUrl, string gitUrl, string sshUrl, string readmeMarkdown, string readmeHtml, string licenseText, string licenseName)
        {
            OwnerName = ownerName;
            FullName = fullName;
            Name = name;
            Description = description;
            HtmlUrl = htmlUrl;
            CloneUrl = cloneUrl;
            GitUrl = gitUrl;
            SshUrl = sshUrl;
            ReadmeMarkdown = readmeMarkdown;
            ReadmeHtml = readmeHtml;
            LicenseText = licenseText;
            LicenseName = licenseName;
        }

        public string OwnerName { get; }
        public string FullName { get; }
        public string Name { get; }
        public string Description { get; }
        public string HtmlUrl { get; }
        public string CloneUrl { get; }
        public string GitUrl { get; }
        public string SshUrl { get; }
        public string ReadmeMarkdown { get; }
        public string ReadmeHtml { get; }
        public string LicenseText { get; }
        public string LicenseName { get; }
    }
}
