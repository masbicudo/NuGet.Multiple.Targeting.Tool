using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using System.Xml.Linq;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis;

namespace Masb.NuGet.Multiple.Targeting.Tool
{
    public static class RoslynExtensions
    {
        private const string ImportPortableCSharp =
            @"$(MSBuildExtensionsPath32)\Microsoft\Portable\$(TargetFrameworkVersion)\Microsoft.Portable.CSharp.targets";

        private const string ImportPortableVisualBasic =
            @"$(MSBuildExtensionsPath32)\Microsoft\Portable\$(TargetFrameworkVersion)\Microsoft.Portable.VisualBasic.targets";

        private const string ImportNuget =
            @"$(SolutionDir)\.nuget\NuGet.targets";

        private const string ImportVisualBasic =
            @"$(MSBuildToolsPath)\Microsoft.VisualBasic.targets";

        private const string ImportCSharp =
            @"$(MSBuildToolsPath)\Microsoft.CSharp.targets";

        private static readonly HashSet<string> frmkNetPortable = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase)
            {
                ImportPortableVisualBasic,
                ImportPortableCSharp,
            };

        private static readonly HashSet<string> frmkNet = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase)
            {
                ImportVisualBasic,
                ImportCSharp,
            };

        public static FrameworkName GetFrameworkName(this Project project)
        {
            var xdoc = XDocument.Load(project.FilePath);
            XNamespace msbuild = "http://schemas.microsoft.com/developer/msbuild/2003";
            var version = xdoc.Descendants(msbuild + "TargetFrameworkVersion").Single().Value;
            var profile = xdoc.Descendants(msbuild + "TargetFrameworkProfile").Single().Value;
            var imports = xdoc.Descendants(msbuild + "Import").Select(x => x.Attribute("Project").Value).ToArray();

            var identifier = imports.Any(x => frmkNetPortable.Contains(x))
                ? ".NETPortable"
                : imports.Any(x => frmkNet.Contains(x))
                    ? ".NETFramework"
                    : null;

            version = version.StartsWith("v") ? version.Substring(1) : version;

            if (identifier != null)
                return new FrameworkName(identifier, new Version(version), profile);

            return null;
        }

        public static async Task<Project> GetProjectWithReferencesAsync(this Project project)
        {
            var frameworkName = project.GetFrameworkName();
            var compilation = await project.GetProjectWithReferencesAsync(frameworkName);
            return compilation;
        }

        public static async Task<Project> GetProjectWithReferencesAsync(
            [NotNull] this Project project,
            [NotNull] FrameworkName frameworkName)
        {
            if (project == null)
                throw new ArgumentNullException("project");

            if (frameworkName == null)
                throw new ArgumentNullException("frameworkName");

            var frameworkInfo = await FrameworkInfo.GetOrCreateAsync(frameworkName);

            if (frameworkInfo == null)
                throw new Exception("Invalid framework name");

            var libraries = frameworkInfo.AssemblyInfos
                .ToDictionary(x => x.RelativePath, frameworkInfo.GetAssemblyPath);

            var xdoc = XDocument.Load(project.FilePath);
            XNamespace msbuild = "http://schemas.microsoft.com/developer/msbuild/2003";

            var references = xdoc.Descendants(msbuild + "ItemGroup").Descendants(msbuild + "Reference");
            var includesRelPaths = references
                .Select(x => x.Attribute("Include").Value)
                .Concat(new[] { "mscorlib" })
                .Distinct(StringComparer.InvariantCultureIgnoreCase)
                .Select(x => string.Format("~\\{0}.dll", x))
                .ToArray();

            var includes = includesRelPaths
                .Where(libraries.ContainsKey)
                .Select(i => libraries[i])
                .Select(fname => MetadataReference.CreateFromFile(fname))
                .ToArray();

            var project2 = project.AddMetadataReferences(includes);

            return project2;
        }

        /// <summary>
        /// Determines whether a metadata reference is a framework reference or not.
        /// </summary>
        /// <param name="reference">Metadata reference to test.</param>
        /// <returns>True if the metadata reference refers to a framework assembly.</returns>
        public static bool IsFrameworkReference(this MetadataReference reference)
        {
            var frmkName = PathHelper.GetFrameworkName(reference.Display);
            return frmkName != null;
        }

        /// <summary>
        /// Recompiles a compilation with a new target framework.
        /// </summary>
        /// <param name="compilation">Compilation to recompile.</param>
        /// <param name="frameworkInfo">New target framework.</param>
        /// <param name="relativeReferences">Framework references that need to be added.</param>
        /// <returns>A new compilation targeting the passed framework.</returns>
        public static Compilation RecompileWithReferences(
            [NotNull] this Compilation compilation,
            [NotNull] FrameworkInfo frameworkInfo,
            [NotNull] IEnumerable<string> relativeReferences)
        {
            if (compilation == null)
                throw new ArgumentNullException("compilation");

            if (frameworkInfo == null)
                throw new ArgumentNullException("frameworkInfo");

            if (relativeReferences == null)
                throw new ArgumentNullException("relativeReferences");

            var includesRelPaths = new HashSet<string>(relativeReferences, StringComparer.InvariantCultureIgnoreCase);
            includesRelPaths.RemoveWhere(String.IsNullOrWhiteSpace);

            // getting current framework references and then
            //  - if present in the new target framework: replace by the new reference
            //  - otherwise: remove the reference
            var currentFrmkRefs = compilation.References
                .Where(IsFrameworkReference)
                .ToArray();

            var refsToRemove = new List<MetadataReference>(currentFrmkRefs.Length);
            foreach (var eachFrmkRef in currentFrmkRefs)
            {
                var relativePath = eachFrmkRef.Display;
                if (PathHelper.TryGetFrameworkRelativePath(ref relativePath))
                {
                    var newAssembly = frameworkInfo.GetAssemblyInfoByRelativePath(relativePath);
                    if (newAssembly != null)
                    {
                        var newAssemblyPath = frameworkInfo.GetAssemblyPath(newAssembly);
                        var newFrmkRef = MetadataReference.CreateFromFile(newAssemblyPath);
                        compilation = compilation.ReplaceReference(eachFrmkRef, newFrmkRef);
                    }
                    else
                    {
                        refsToRemove.Add(eachFrmkRef);
                    }

                    // removing already existing references from the include list
                    includesRelPaths.Remove(relativePath);
                }
            }

            compilation = compilation.RemoveReferences(refsToRemove);

            // creating the new inclusion list
            var includes = includesRelPaths
                .OrderBy(x => x)
                .Select(frameworkInfo.GetAssemblyInfoByRelativePath)
                .Select(frameworkInfo.GetAssemblyPath)
                .Select(x => MetadataReference.CreateFromFile(x))
                .OfType<MetadataReference>()
                .ToArray();

            compilation = compilation.AddReferences(includes);

            return compilation;
        }

        /// <summary>
        /// Gets the full name of a type or namespace symbol, including the owning namespace names separated by '.' character.
        /// </summary>
        /// <param name="symbol"></param>
        /// <returns></returns>
        public static string GetTypeFullName(this INamespaceOrTypeSymbol symbol)
        {
            var ns = symbol.ContainingNamespace;
            return ns == null || !ns.IsGlobalNamespace
                ? GetTypeFullName((INamespaceOrTypeSymbol)symbol.ContainingSymbol) + "." + GetSymbolName(symbol)
                : GetSymbolName(symbol);
        }

        public static string GetSymbolName(this INamespaceOrTypeSymbol symbol)
        {
            var type = symbol as INamedTypeSymbol;
            if (type != null && type.Arity > 0)
                return type.Name + "`" + type.Arity;
            return symbol.Name;
        }

        public static string[] GetVariance(this INamedTypeSymbol typeSymbol)
        {
            if (typeSymbol.TypeParameters.Any(tp => tp.Variance != VarianceKind.None))
                return typeSymbol.TypeParameters
                    .Select(tp => tp.Variance.ToString().ToLowerInvariant())
                    .ToArray();

            return null;
        }
    }
}