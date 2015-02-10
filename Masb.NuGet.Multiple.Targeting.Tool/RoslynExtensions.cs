using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using System.Xml.Linq;
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

        public static async Task<Compilation> GetCompilationWithNetReferences(this Project project)
        {
            var frameworkName = project.GetFrameworkName();
            var compilation = await project.GetCompilationWithNetReferences(frameworkName);
            return compilation;
        }

        public static async Task<Compilation> GetCompilationWithNetReferences(this Project project, FrameworkName frameworkName)
        {
            var frameworkInfo = await FrameworkInfo.CreateAsync(frameworkName);

            if (frameworkInfo == null)
                throw new Exception("Invalid framework name");

            var libraries = frameworkInfo.AssemblyInfos
                .Select(x => x.HintFile)
                .ToDictionary(Path.GetFileNameWithoutExtension);

            var xdoc = XDocument.Load(project.FilePath);
            XNamespace msbuild = "http://schemas.microsoft.com/developer/msbuild/2003";

            var references = xdoc.Descendants(msbuild + "ItemGroup").Descendants(msbuild + "Reference");
            var includes = references
                .Select(x => x.Attribute("Include").Value)
                .Concat(new[] { "mscorlib" })
                .Where(libraries.ContainsKey)
                .Select(i => libraries[i])
                .Select(fname => MetadataReference.CreateFromFile(fname))
                .ToArray();

            var project2 = project.AddMetadataReferences(includes);

            var compilation = await project2.GetCompilationAsync();
            return compilation;
        }
    }
}