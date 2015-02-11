using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis;

namespace Masb.NuGet.Multiple.Targeting.Tool
{
    public class FrameworkRequirements
    {
        public bool SupportedBy([NotNull] FrameworkInfo frameworkInfo)
        {
            if (frameworkInfo == null)
                throw new ArgumentNullException("frameworkInfo");

            var allTypes = frameworkInfo.AssemblyInfos
                .SelectMany(x => x.NamedTypes)
                .Select(x => x.Key)
                .ToImmutableHashSet();

            var unsupportedTypes = this.Types
                .Where(tn => tn.TypeName != "")
                .Where(tn => !allTypes.Contains(tn.TypeName))
                .ToList();

            // trying to compile
            // TODO: Set debug symbols according to the used framework: net40; portable; sl50; etc.
            // TODO: This is needed because the user might already be using these symbols
            // TODO: to compensate incompatibilities between frameworks.
            var recompilation = this.Compilation.RecompileWithNetReferences(frameworkInfo.FrameworkName).Result;
            var diag = recompilation.GetDiagnostics();

            return unsupportedTypes.Count == 0 && diag.Length == 0;
        }

        public TypeSymbolInfo[] Types { get; set; }

        public Compilation Compilation { get; set; }

        public SupportNode[] GetSupportGraph(FrameworksGraph frameworkGraph)
        {
            var supportedFrameworks = frameworkGraph.Visit<SupportNode[]>(this.SupportedFrameworkNodes);
            return supportedFrameworks;
        }

        private SupportNode[] SupportedFrameworkNodes(ImmutableStack<FrameworksGraph> path, IEnumerable<SupportNode[]> children)
        {
            var childrenArray = children.SelectMany(x => x).ToImmutableArray();

            if (childrenArray.Length != 1)
            {
                var node = path.Peek();
                var isSupported = node.FrameworkInfo != null && this.SupportedBy(node.FrameworkInfo);
                if (isSupported)
                    return new[] { new SupportNode(node, childrenArray) };
            }

            return childrenArray.ToArray();
        }
    }
}