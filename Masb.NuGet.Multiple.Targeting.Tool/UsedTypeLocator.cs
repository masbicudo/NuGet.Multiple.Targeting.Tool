﻿using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Masb.NuGet.Multiple.Targeting.Tool
{
    public class UsedTypeLocator
    {
        private readonly Compilation compilation;
        private readonly HashSet<INamedTypeSymbol> usedTypes = new HashSet<INamedTypeSymbol>();

        public UsedTypeLocator(Compilation compilation)
        {
            this.compilation = compilation;
        }

        public INamedTypeSymbol[] UsedTypes
        {
            get { return this.usedTypes.ToArray(); }
        }

        private void Add(INamedTypeSymbol namedTypeSymbol)
        {
            if (namedTypeSymbol == null)
                throw new Exception("Cannot be null.");

            if (namedTypeSymbol.Locations == null
                || namedTypeSymbol.Locations.Length == 0
                || namedTypeSymbol.Locations.All(x => x.Kind != LocationKind.SourceFile))
            {
                this.usedTypes.Add(namedTypeSymbol);
            }

            if (namedTypeSymbol.TypeArguments != null)
                this.Add(namedTypeSymbol.TypeArguments.OfType<INamedTypeSymbol>());
        }

        private void Add(ITypeSymbol typeSymbol)
        {
            var namedTypeSymbol = typeSymbol as INamedTypeSymbol;
            if (namedTypeSymbol != null)
                this.Add(namedTypeSymbol);
        }

        private void Add(IEnumerable<INamedTypeSymbol> namedTypeSymbols)
        {
            foreach (var namedTypeSymbol in namedTypeSymbols)
                this.Add(namedTypeSymbol);
        }

        public void VisitSyntax(SyntaxNode node)
        {
            var nodes = node.DescendantNodes(n => true);

            var st = node.SyntaxTree;
            var sm = this.compilation.GetSemanticModel(st);

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

                this.Add(namedTypes);

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

                this.Add(expressionTypes);
            }
        }
    }
}