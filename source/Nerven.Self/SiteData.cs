using System.Collections.Generic;

namespace Nerven.Self
{
    public sealed class SiteData
    {
        public SiteData(IReadOnlyList<ProjectInfo> projects)
        {
            Projects = projects;
        }

        public IReadOnlyList<ProjectInfo> Projects { get;  }
    }
}
