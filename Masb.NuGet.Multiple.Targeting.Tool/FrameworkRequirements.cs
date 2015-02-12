using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis;

namespace Masb.NuGet.Multiple.Targeting.Tool
{
    public class FrameworkRequirements
    {
        /// <summary>
        /// Analyses a framework to tell whether it support or not the requirements.
        /// </summary>
        /// <param name="frameworkInfo">The framework to test.</param>
        /// <returns>True if the framework support the requirements, otherwise False.</returns>
        public async Task<bool> SupportedBy([NotNull] FrameworkInfo frameworkInfo)
        {
            if (frameworkInfo == null)
                throw new ArgumentNullException("frameworkInfo");

            var allTypes = frameworkInfo.AssemblyInfos
                .SelectMany(assembly => assembly.NamedTypes.Select(y => new { assembly, type = y.Value }))
                .GroupBy(x => x.type.TypeName)
                .ToDictionary(x => x.Key, x => x.ToArray());

            var supportedTypes = this.Types
                .Where(tn => tn != "")
                .Where(allTypes.ContainsKey)
                .Select(tn => allTypes[tn])
                .ToList();

            var unsupportedTypes = this.Types
                .Where(tn => tn != "")
                .Where(tn => !allTypes.ContainsKey(tn))
                .ToList();

            var neededAssemblies = supportedTypes
                .SelectMany(x => x.Select(y => y.assembly))
                .Select(a => a.RelativePath)
                .Distinct()
                .ToArray();

            if (unsupportedTypes.Count > 0)
                return false;

            // trying to compile
            // TODO: Set debug symbols according to the used framework: net40; portable; sl50; etc.
            // TODO: This is needed because the user might already be using these symbols
            // TODO: to compensate incompatibilities between frameworks.
            var recompilation = this.Compilation.RecompileWithReferences(frameworkInfo, neededAssemblies);
            var diag = recompilation.GetDiagnostics();

            foreach (var d in diag)
            {
                if (!d.IsWarningAsError)
                    continue;

                if (!d.Location.IsInSource)
                    return false;

                var semantic = recompilation.GetSemanticModel(d.Location.SourceTree);
                var root = await d.Location.SourceTree.GetRootAsync();
                var node = root.FindNode(d.Location.SourceSpan);
                var typeInfo = semantic.GetTypeInfo(node);
                if (typeInfo.Type.TypeKind == TypeKind.Error)
                {
                    // TODO: see if the type is in the ignoreList
                    //if (!isInIgnoreList)
                    //    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Gets or sets information
        /// </summary>
        public string[] Types { get; set; }

        public Compilation Compilation { get; set; }

        public async Task<SupportGraph[]> GetSupportGraphAsync(HierarchyGraph hierarchyGraph)
        {
            var supportedFrameworks = await hierarchyGraph.VisitAsync<SupportGraph[]>(this.SupportedFrameworkNodesAsync);
            return supportedFrameworks;
        }

        private async Task<SupportGraph[]> SupportedFrameworkNodesAsync(ImmutableStack<HierarchyGraph> path, IEnumerable<SupportGraph[]> children)
        {
            var childrenArray = children.SelectMany(x => x).ToImmutableArray();

            if (childrenArray.Length != 1)
            {
                var node = path.Peek();
                var isSupported = node.FrameworkInfo != null && (await this.SupportedBy(node.FrameworkInfo));
                if (isSupported)
                    return new[] { new SupportGraph(node, childrenArray) };
            }

            return childrenArray.ToArray();
        }
    }
}