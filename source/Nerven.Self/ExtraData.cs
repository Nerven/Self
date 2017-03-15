using System.Collections.Generic;

namespace Nerven.Self
{
    public sealed class ExtraData
    {
        public ExtraData(IReadOnlyList<ExtraProjectInfo> projects)
        {
            Projects = projects;
        }

        public IReadOnlyList<ExtraProjectInfo> Projects { get; }
    }
}
