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
    public class SupportGraph
    {
        public HierarchyGraph HierarchyGraph { get; private set; }

        public ImmutableArray<SupportGraph> Children { get; private set; }

        public SupportGraph([NotNull] HierarchyGraph hierarchyGraph, ImmutableArray<SupportGraph> children)
        {
            if (hierarchyGraph == null)
                throw new ArgumentNullException("hierarchyGraph");

            if (children.Any(x => x == null))
                throw new ArgumentNullException("children", "No child may be null.");

            this.HierarchyGraph = hierarchyGraph;
            this.Children = children;
        }

        public override string ToString()
        {
            return this.HierarchyGraph.ToString();
        }
    }
}