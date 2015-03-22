using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;

namespace Masb.NuGet.Multiple.Targeting.Tool.Graphs
{
    public static class HierarchyGraphExtensions
    {
        public static async Task<SupportGraph[]> GetSupportGraphAsync(
            this HierarchyGraph hierarchyGraph,
            FrameworkRequirements requirements,
            bool hideUnsupported)
        {
            var supportedFrameworks = await hierarchyGraph.VisitAsync<SupportGraph[]>(
                (path, children) => SupportedFrameworkNodesAsync(path, children, requirements, hideUnsupported));

            return supportedFrameworks;
        }

        private static async Task<SupportGraph[]> SupportedFrameworkNodesAsync(
            ImmutableStack<HierarchyGraph> path,
            IEnumerable<SupportGraph[]> children,
            FrameworkRequirements requirements,
            bool hideUnsupported)
        {
            var childrenArray = children.SelectMany(x => x).ToImmutableArray();

            var node = path.Peek();
            var isRootGroup = node.FrameworkInfo == null;
            if (!isRootGroup)
            {
                var supportResult = await requirements.SupportedBy(node.FrameworkInfo);
                var isSupported = supportResult.IsOk;
                if (isSupported || !hideUnsupported)
                    return new[] { new SupportGraph(node, childrenArray, supportResult) };
            }

            return childrenArray.ToArray();
        }

        public static SimplifiedGraph[] GetSimplifiedGraph(this HierarchyGraph hierarchyGraph, FrameworkRequirements requirements)
        {
            var supportedFrameworks = hierarchyGraph.Visit<SimplifiedGraph[]>(SimplifiedFrameworkNodesAsync);
            return supportedFrameworks;
        }

        private static SimplifiedGraph[] SimplifiedFrameworkNodesAsync(
            ImmutableStack<HierarchyGraph> path,
            IEnumerable<SimplifiedGraph[]> children)
        {
            var childrenArray = children.SelectMany(x => x).ToImmutableArray();

            if (childrenArray.Length != 1)
            {
                var node = path.Peek();
                var isRootGroup = node.FrameworkInfo == null;
                if (!isRootGroup)
                    return new[] { new SimplifiedGraph(node, childrenArray) };
            }

            return childrenArray.ToArray();
        }
    }
}