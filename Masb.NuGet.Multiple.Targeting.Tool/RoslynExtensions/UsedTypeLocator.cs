using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Masb.NuGet.Multiple.Targeting.Tool.RoslynExtensions
{
    public class UsedTypeLocator
    {
        public static async Task<HashSet<INamedTypeSymbol>> FindUsedTypesInCompilation(
            Compilation compilation,
            Action<SyntaxTree> stepAction = null)
        {
            var usedTypes = new HashSet<INamedTypeSymbol>();
            foreach (var syntaxTree in compilation.SyntaxTrees)
            {
                if (stepAction != null)
                    stepAction(syntaxTree);
                var root = await syntaxTree.GetRootAsync();
                FindUsedTypesInSyntaxNode(compilation, usedTypes, root);
            }

            return usedTypes;
        }

        private static void FindUsedTypesInSyntaxNode(
            Compilation compilation,
            HashSet<INamedTypeSymbol> usedTypes,
            SyntaxNode node)
        {
            var nodes = node.DescendantNodes(n => true);

            var st = node.SyntaxTree;
            var sm = compilation.GetSemanticModel(st);

            if (nodes != null)
            {
                var syntaxNodes = nodes as SyntaxNode[] ?? nodes.ToArray();

                // IdentifierNameSyntax:
                //  - var keyword
                //  - identifiers of any kind (including type names)
                var namedTypes = syntaxNodes
                    .OfType<IdentifierNameSyntax>()
                    .Select(id => sm.GetSymbolInfo(id).Symbol)
                    .OfType<INamedTypeSymbol>()
                    .ToArray();

                foreach (var namedTypeSymbol in namedTypes)
                    Add(usedTypes, namedTypeSymbol);

                // ExpressionSyntax:
                //  - method calls
                //  - property uses
                //  - field uses
                //  - all kinds of composite expressions
                var expressionTypes = syntaxNodes
                    .OfType<ExpressionSyntax>()
                    .Select(ma => sm.GetTypeInfo(ma).Type)
                    .OfType<INamedTypeSymbol>()
                    .ToArray();

                foreach (var namedTypeSymbol in expressionTypes)
                    Add(usedTypes, namedTypeSymbol);
            }
        }

        private static void Add(HashSet<INamedTypeSymbol> usedTypes, INamedTypeSymbol namedTypeSymbol)
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
                    Add(usedTypes, namedTypeSymbol2);
        }
    }
}