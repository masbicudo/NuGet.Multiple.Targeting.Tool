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

        internal TypeSymbolInfo(string typeName, string[] variance)
        {
            this.TypeName = typeName;
            this.Variance = variance;
        }

        public static TypeSymbolInfo Create(INamedTypeSymbol arg)
        {
            return new TypeSymbolInfo(arg.GetTypeFullName(), arg.GetVariance());
        }
    }
}