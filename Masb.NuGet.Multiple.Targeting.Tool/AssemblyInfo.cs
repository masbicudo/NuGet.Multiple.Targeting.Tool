using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Masb.NuGet.Multiple.Targeting.Tool
{
    public class AssemblyInfo
    {
        public AssemblyInfo(string hintFile, AssemblyName assemblyName, IEnumerable<TypeSymbolInfo> allTypes)
        {
            this.HintFile = hintFile;
            this.AssemblyName = assemblyName;
            this.NamedTypes = allTypes.GroupBy(x => x.TypeName).ToImmutableDictionary(x => x.Key, x => x.First());
            this.HasVariance = GetTypesWithVariance(this.NamedTypes).Any();
        }

        private static IEnumerable<TypeSymbolInfo> GetTypesWithVariance(IEnumerable<KeyValuePair<string, TypeSymbolInfo>> frmkInfo)
        {
            return frmkInfo
                .Select(x => x.Value)
                .Where(x => x.Variance != null);
        }

        public bool HasVariance { get; private set; }

        public string HintFile { get; private set; }

        public AssemblyName AssemblyName { get; private set; }

        public ImmutableDictionary<string, TypeSymbolInfo> NamedTypes { get; private set; }

        public static AssemblyInfo GetAssemblyInfo(string assemblyFile)
        {
            var compilation = CSharpCompilation.Create(
                "__Extractor__",
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary),
                syntaxTrees: new CSharpSyntaxTree[0],
                references: new[] { MetadataReference.CreateFromFile(assemblyFile) }
                );

            var allTypes = Enumerable.Empty<INamedTypeSymbol>();
            var rootSymbols = compilation.GlobalNamespace.GetMembers();
            AssemblyName assemblyName = null;
            if (rootSymbols != null)
            {
                foreach (var rootSymbol in rootSymbols)
                {
                    assemblyName = assemblyName ?? new AssemblyName(rootSymbol.ContainingAssembly.ToString());
                    allTypes = allTypes.Concat(LocateTypeSymbols(rootSymbol));
                }
            }

            var allTypes2 = allTypes
                .Where(
                    t => t.DeclaredAccessibility == Accessibility.Public
                         || t.DeclaredAccessibility == Accessibility.Protected)
                .Select(TypeSymbolInfo.Create).ToImmutableHashSet();

            return new AssemblyInfo(assemblyFile, assemblyName, allTypes2);
        }

        private static IEnumerable<INamedTypeSymbol> LocateTypeSymbols(INamespaceOrTypeSymbol rootSymbol)
        {
            var a = rootSymbol.GetTypeMembers();
            var ns = rootSymbol as INamespaceSymbol;
            if (ns == null)
                return a;

            var b = ns.GetNamespaceMembers().SelectMany(LocateTypeSymbols);
            return a.Concat(b);
        }

        public override string ToString()
        {
            return this.AssemblyName.ToString();
        }
    }
}