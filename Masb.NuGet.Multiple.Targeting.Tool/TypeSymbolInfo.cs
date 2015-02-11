using System.Linq;
using Microsoft.CodeAnalysis;

namespace Masb.NuGet.Multiple.Targeting.Tool
{
    public class TypeSymbolInfo
    {
        /// <summary>
        /// Gets the full type name, consisting of namespace names and the own name, separated by '.'.
        /// </summary>
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
                ? GetTypeName((INamespaceOrTypeSymbol)symbol.ContainingSymbol) + "." + GetSymbolName(symbol)
                : GetSymbolName(symbol);
        }

        private static string GetSymbolName(INamespaceOrTypeSymbol symbol)
        {
            var type = symbol as INamedTypeSymbol;
            if (type != null && type.Arity > 0)
                return type.Name + "`" + type.Arity;
            return symbol.Name;
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