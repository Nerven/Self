using System.Collections.Generic;

namespace Nerven.Self
{
    public sealed class ExtraProjectInfo
    {
        public ExtraProjectInfo(
            string name,
            string logoText, 
            IReadOnlyList<string> dotNetPlatforms, 
            IReadOnlyList<string> nugetPackages,
            ProjectDevelopmentStatus? developmentStatus, 
            bool? recommended)
        {
            Name = name;
            LogoText = logoText;
            DotNetPlatforms = dotNetPlatforms;
            NugetPackages = nugetPackages;
            DevelopmentStatus = developmentStatus;
            Recommended = recommended;
        }

        public string Name { get; }

        public string LogoText { get; }

        public IReadOnlyList<string> DotNetPlatforms { get; }

        public IReadOnlyList<string> NugetPackages { get; }

        public ProjectDevelopmentStatus? DevelopmentStatus { get; }

        public bool? Recommended { get; }
    }
}
