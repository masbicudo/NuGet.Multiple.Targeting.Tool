using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Masb.NuGet.Multiple.Targeting.Tool.RoslynExtensions
{
    public static class UsedTypeLocator
    {
        /// <summary>
        /// Gets a set of types used in the given compilation.
        /// </summary>
        /// <param name="compilation">The compilation to analyze.</param>
        /// <param name="stepAction">Action that is called for each <see cref="SyntaxTree"/> that is being analyzed.</param>
        /// <returns>A <see cref="Task{T}"/> that returns a collection of types used by a <see cref="Compilation"/>.</returns>
        public static async Task<HashSet<INamedTypeSymbol>> FindUsedTypes(
            this Compilation compilation,
            Action<SyntaxTree> stepAction = null)
        {
            var usedTypes = new HashSet<INamedTypeSymbol>();
            foreach (var syntaxTree in compilation.SyntaxTrees)
            {
                if (stepAction != null)
                    stepAction(syntaxTree);

                var rootNode = await syntaxTree.GetRootAsync();
                var semanticModel = compilation.GetSemanticModel(syntaxTree);

                var nodes = rootNode.DescendantNodes(n => true);
                if (nodes != null)
                {
                    var syntaxNodes = nodes as SyntaxNode[] ?? nodes.ToArray();

                    // IdentifierNameSyntax:
                    //  - var keyword
                    //  - identifiers of any kind (including type names)
                    var namedTypes = syntaxNodes
                        .OfType<IdentifierNameSyntax>()
                        .Select(id => semanticModel.GetSymbolInfo(id).Symbol)
                        .OfType<INamedTypeSymbol>()
                        .ToArray();

                    foreach (var namedTypeSymbol in namedTypes)
                        usedTypes.AddTypeAndParams(namedTypeSymbol);

                    // ExpressionSyntax:
                    //  - method calls
                    //  - property uses
                    //  - field uses
                    //  - all kinds of composite expressions
                    var expressionTypes = syntaxNodes
                        .OfType<ExpressionSyntax>()
                        .Select(ma => semanticModel.GetTypeInfo(ma).Type)
                        .OfType<INamedTypeSymbol>()
                        .ToArray();

                    foreach (var namedTypeSymbol in expressionTypes)
                        usedTypes.AddTypeAndParams(namedTypeSymbol);
                }
            }

            return usedTypes;
        }

        private static void AddTypeAndParams(this HashSet<INamedTypeSymbol> usedTypes, INamedTypeSymbol namedTypeSymbol)
        {
            if (namedTypeSymbol == null)
                throw new Exception("Cannot be null.");

            if (namedTypeSymbol.Locations == null
                || namedTypeSymbol.Locations.Length == 0
                || namedTypeSymbol.Locations.All(x => x.Kind != LocationKind.SourceFile))
            {
                usedTypes.Add(namedTypeSymbol);
            }

            if (namedTypeSymbol.TypeArguments != null)
                foreach (var namedTypeSymbol2 in namedTypeSymbol.TypeArguments.OfType<INamedTypeSymbol>())
                    usedTypes.AddTypeAndParams(namedTypeSymbol2);
        }
    }
}