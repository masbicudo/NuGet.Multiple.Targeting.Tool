using System;
using System.Collections.Immutable;
using System.Linq;
using JetBrains.Annotations;

namespace Masb.NuGet.Multiple.Targeting.Tool.Graphs
{
    public class SimplifiedGraph : HierarchyGraph
    {
        [NotNull]
        public HierarchyGraph ShadowGraph { get; private set; }

        internal SimplifiedGraph([NotNull] HierarchyGraph shadowGraph, ImmutableArray<SimplifiedGraph> children)
            : base(shadowGraph.FrameworkInfo, children)
        {
            if (shadowGraph == null)
                throw new ArgumentNullException("shadowGraph");

            if (children.Any(x => x == null))
                throw new ArgumentNullException("children", "No child may be null.");

            this.ShadowGraph = shadowGraph;
        }
    }
}