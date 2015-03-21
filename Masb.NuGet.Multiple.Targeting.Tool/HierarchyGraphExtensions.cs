using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;

namespace Masb.NuGet.Multiple.Targeting.Tool
{
    public static class HierarchyGraphExtensions
    {
        public static async Task<SupportGraph2[]> GetSupportGraphAsync(this HierarchyGraph hierarchyGraph, FrameworkRequirements requirements)
        {
            var supportedFrameworks = await hierarchyGraph.VisitAsync<SupportGraph2[]>(
                (path, children) => SupportedFrameworkNodesAsync(path, children, requirements));

            return supportedFrameworks;
        }

        private static async Task<SupportGraph2[]> SupportedFrameworkNodesAsync(
            ImmutableStack<HierarchyGraph> path,
            IEnumerable<SupportGraph2[]> children,
            FrameworkRequirements requirements)
        {
            var childrenArray = children.SelectMany(x => x).ToImmutableArray();

            var node = path.Peek();
            var isRootGroup = node.FrameworkInfo == null;
            var isSupported = !isRootGroup && (await requirements.SupportedBy(node.FrameworkInfo));
            if (isSupported)
                return new[] { new SupportGraph2(node, childrenArray) };

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