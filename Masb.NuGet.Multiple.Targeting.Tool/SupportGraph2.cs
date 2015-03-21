using System;
using System.Collections.Immutable;
using System.Linq;
using JetBrains.Annotations;

namespace Masb.NuGet.Multiple.Targeting.Tool
{
    /// <summary>
    /// Represents a graph or a node in a graph of frameworks that support something.
    /// The support graph makes reference to another framework graph: the framework hierarchy graph.
    /// </summary>
    public class SupportGraph2 : HierarchyGraph
    {
        [NotNull]
        public HierarchyGraph ShadowGraph { get; private set; }

        internal SupportGraph2([NotNull] HierarchyGraph shadowGraph, ImmutableArray<SupportGraph2> children)
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