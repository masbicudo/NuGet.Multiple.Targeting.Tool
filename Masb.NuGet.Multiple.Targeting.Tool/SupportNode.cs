using System.Collections.Immutable;

namespace Masb.NuGet.Multiple.Targeting.Tool
{
    public class SupportNode
    {
        public FrameworksGraph Framework { get; private set; }
        public ImmutableArray<SupportNode> Children { get; private set; }

        public SupportNode(FrameworksGraph framework, ImmutableArray<SupportNode> children)
        {
            this.Framework = framework;
            this.Children = children;
        }
    }
}