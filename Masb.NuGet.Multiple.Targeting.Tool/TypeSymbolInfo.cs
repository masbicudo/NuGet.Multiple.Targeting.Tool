using System.Linq;
using Microsoft.CodeAnalysis;

namespace Masb.NuGet.Multiple.Targeting.Tool
{
    public class TypeSymbolInfo
    {
        public string TypeName { get; private set; }
        public string[] Variance { get; private set; }

        private TypeSymbolInfo(string typeName, string[] variance)
        {
            this.TypeName = typeName;
            this.Variance = variance;
        }

        public static TypeSymbolInfo Create(INamedTypeSymbol arg)
        {
            return new TypeSymbolInfo(GetTypeName(arg), GetVariance(arg));
        }

        private static string GetTypeName(INamespaceOrTypeSymbol symbol)
        {
            var ns = symbol.ContainingNamespace;
            return ns == null || !ns.IsGlobalNamespace
                ? GetTypeName((INamespaceOrTypeSymbol)symbol.ContainingSymbol) + "." + symbol.Name
                : symbol.Name;
        }

        private static string[] GetVariance(INamedTypeSymbol typeSymbol)
        {
            if (typeSymbol.TypeParameters.Any(tp => tp.Variance != VarianceKind.None))
                return typeSymbol.TypeParameters
                    .Select(tp => tp.Variance.ToString().ToLowerInvariant())
                    .ToArray();

            return null;
        }
    }
}