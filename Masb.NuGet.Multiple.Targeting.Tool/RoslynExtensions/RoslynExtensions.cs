using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using JetBrains.Annotations;
using Masb.NuGet.Multiple.Targeting.Tool.Helpers;
using Masb.NuGet.Multiple.Targeting.Tool.InfoModel;
using Microsoft.CodeAnalysis;

namespace Masb.NuGet.Multiple.Targeting.Tool.RoslynExtensions
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

        public static async Task<FrameworkName> GetFrameworkName(this Project project)
        {
            var xdoc = await XmlHelpers.ReadXDocumentAsync(project.FilePath);
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
            var frameworkName = await project.GetFrameworkName();
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

            // some lazy things
            var lazyTargetFrmkInfo = new Lazy<Task<FrameworkInfo>>(
                async () =>
                {
                    var frameworkInfo = await FrameworkInfo.GetOrCreateAsync(frameworkName);
                    if (frameworkInfo == null)
                        throw new Exception("Invalid framework name");
                    return frameworkInfo;
                },
                LazyThreadSafetyMode.None);

            var lazyTargetLibraries = new Lazy<Task<Dictionary<string, string>>>(
                async () =>
                {
                    var targetFrameworkInfo = await lazyTargetFrmkInfo.Value;
                    return targetFrameworkInfo.AssemblyInfos
                        .ToDictionary(
                            x => x.RelativePath,
                            targetFrameworkInfo.GetAssemblyPath,
                            StringComparer.InvariantCultureIgnoreCase);
                },
                LazyThreadSafetyMode.None);

            // The new list of references, that will replace the current project references.
            var newRefsList = new List<MetadataReference>(project.MetadataReferences.Count);
            bool anyChanges = false;

            // first we need to process already existing references:
            //  - if it's a framework reference:
            //      - if source and target framework differs, replace the reference
            //      - if the target framework doesn't contain the reference, remove it
            //  - if it's not a frmk ref, leave it there
            var replacements = from item in project.MetadataReferences
                               let itemFrmkName = PathHelper.GetFrameworkName(item.Display)
                               select GetReferenceReplacementAsync(
                                   frameworkName,
                                   item,
                                   itemFrmkName,
                                   lazyTargetFrmkInfo,
                                   lazyTargetLibraries);

            foreach (var replacement in replacements)
            {
                var repl = await replacement;
                if (repl.NewItem != null)
                {
                    newRefsList.Add(repl.NewItem);
                    if (!repl.OldItem.Equals(repl.NewItem))
                        anyChanges = true;
                }
            }

            // second, we need to open the project file and scan for references that may be missing
            if (project.FilePath != null)
            {
                string[] pathsToInclude = null;

                {
                    var xdoc = XDocument.Load(project.FilePath);
                    XNamespace msbuild = "http://schemas.microsoft.com/developer/msbuild/2003";

                    var references = xdoc.Descendants(msbuild + "ItemGroup").Descendants(msbuild + "Reference");
                    pathsToInclude = (
                        from re in references
                        let include = new AssemblyName(re.Attribute("Include").Value)
                        let hintPath = re.Element("HintPath").With(e => e == null ? null : e.Value)
                        select GetAssemblyPath(project, hintPath, include)
                        )
                        .Where(x => x != null)
                        .Concat(new[] { "~\\mscorlib.dll" })
                        .Distinct()
                        .ToArray();
                }

                var alreadyAdded = new HashSet<string>(
                    newRefsList.Select(x => x.Display),
                    StringComparer.InvariantCultureIgnoreCase);

                foreach (var path in pathsToInclude)
                {
                    if (path == null)
                        continue;

                    if (path.StartsWith("~\\"))
                    {
                        var frameworkInfo = await lazyTargetFrmkInfo.Value;
                        var newAssembly = await frameworkInfo.GetAssemblyInfoByRelativePathAsync(path);
                        var libraries = await lazyTargetLibraries.Value;
                        if (newAssembly != null && libraries.ContainsKey(path))
                        {
                            var newAssemblyPath = frameworkInfo.GetAssemblyPath(newAssembly);
                            if (!alreadyAdded.Contains(newAssemblyPath))
                            {
                                var newFrmkRef = MetadataReference.CreateFromFile(newAssemblyPath);
                                newRefsList.Add(newFrmkRef);
                                anyChanges = true;
                            }
                        }
                    }
                    else
                    {
                        var newAssemblyPath = PathHelper.Combine(project.FilePath, path);
                        if (!alreadyAdded.Contains(newAssemblyPath) && File.Exists(newAssemblyPath))
                        {
                            var newFrmkRef = MetadataReference.CreateFromFile(newAssemblyPath);
                            newRefsList.Add(newFrmkRef);
                            anyChanges = true;
                        }
                    }
                }
            }

            // finally replacing all references, if needed
            if (anyChanges)
                project = project.WithMetadataReferences(newRefsList);

            return project;
        }

        private static string GetAssemblyPath(Project project, string hintPath, AssemblyName include)
        {
            if (hintPath != null)
                return PathHelper.Combine(project.FilePath, hintPath);

            if (include.Version != null && include.GetPublicKeyToken().Length > 0)
            {
                try
                {
                    return Assembly.ReflectionOnlyLoad(include.FullName).Location;
                }
                catch
                {
                    return null;
                }
            }

            return string.Format("~\\{0}.dll", include.Name);
        }

        private static async Task<DiffItem<MetadataReference>> GetReferenceReplacementAsync(
            FrameworkName frameworkName,
            MetadataReference item,
            FrameworkName itemFrmkName,
            Lazy<Task<FrameworkInfo>> lazyTargetFrameworkInfo,
            Lazy<Task<Dictionary<string, string>>> lazyTargetLibraries)
        {
            if (item == null || itemFrmkName.Equals(frameworkName))
                return new DiffItem<MetadataReference>(item, item);

            string relativePath;
            if (PathHelper.TryGetFrameworkRelativePath(item.Display, out relativePath))
            {
                var frameworkInfo = await lazyTargetFrameworkInfo.Value;
                var newAssembly = await frameworkInfo.GetAssemblyInfoByRelativePathAsync(relativePath);
                var libraries = await lazyTargetLibraries.Value;
                if (newAssembly != null && libraries.ContainsKey(relativePath))
                {
                    var newAssemblyPath = frameworkInfo.GetAssemblyPath(newAssembly);
                    var newFrmkRef = MetadataReference.CreateFromFile(newAssemblyPath);

                    return new DiffItem<MetadataReference>(item, newFrmkRef);
                }
            }

            return new DiffItem<MetadataReference>(item, null);
        }

        /// <summary>
        /// Determines whether a metadata reference is a framework reference or not.
        /// </summary>
        /// <param name="reference">Metadata reference to test.</param>
        /// <returns>True if the metadata reference refers to a framework assembly.</returns>
        public static bool IsFrameworkReference(this MetadataReference reference)
        {
            var path = reference.Display;
            return IsFrameworkReference(path);
        }

        private static bool IsFrameworkReference(string path)
        {
            var frmkName = PathHelper.GetFrameworkName(path);
            return frmkName != null;
        }

        /// <summary>
        /// Recompiles a compilation with a new target framework.
        /// </summary>
        /// <param name="compilation">Compilation to recompile.</param>
        /// <param name="frameworkInfo">New target framework.</param>
        /// <param name="relativeReferences">Framework references that need to be added.</param>
        /// <returns>A new compilation targeting the passed framework.</returns>
        public static async Task<Compilation> RecompileWithReferencesAsync(
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
            includesRelPaths.RemoveWhere(string.IsNullOrWhiteSpace);

            // getting current framework references and then
            //  - if present in the new target framework: replace by the new reference
            //  - otherwise: remove the reference
            var currentFrmkRefs = compilation.References
                .Where(IsFrameworkReference)
                .ToArray();

            var refsToRemove = new List<MetadataReference>(currentFrmkRefs.Length);
            foreach (var eachFrmkRef in currentFrmkRefs)
            {
                string relativePath;
                if (PathHelper.TryGetFrameworkRelativePath(eachFrmkRef.Display, out relativePath))
                {
                    var newAssembly = await frameworkInfo.GetAssemblyInfoByRelativePathAsync(relativePath);
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
            var includesTasks = includesRelPaths
                .OrderBy(x => x)
                .Select(frameworkInfo.GetAssemblyInfoByRelativePathAsync)
                .ThenSelect(frameworkInfo.GetAssemblyPath)
                .ThenSelect(x => MetadataReference.CreateFromFile(x));

            var includes = (await Task.WhenAll(includesTasks))
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

    public struct DiffItem<T>
    {
        public readonly T OldItem;

        public readonly T NewItem;

        public DiffItem(T oldItem, T newItem)
        {
            this.OldItem = oldItem;
            this.NewItem = newItem;
        }
    }

    internal static class ObjectExtensions
    {
        public static TOut With<T, TOut>(this T obj, [NotNull] Func<T, TOut> func)
        {
            if (func == null)
                throw new ArgumentNullException("func");

            return func(obj);
        }
    }
}