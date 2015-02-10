using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml.Serialization;
using JetBrains.Annotations;

namespace Masb.NuGet.Multiple.Targeting.Tool
{
    public class FrameworkInfo
    {
        public FrameworkInfo(
            FrameworkName frameworkName,
            IEnumerable<AssemblyInfo> assemblyInfos,
            IEnumerable<IUndeterminedSet<FrameworkName>> supportedFrameworks,
            IEnumerable<string> missngDlls)
        {
            this.SupportedFrameworks = new IntersectionSet<FrameworkName>(supportedFrameworks);
            this.FrameworkName = frameworkName;
            this.AssemblyInfos = assemblyInfos.ToImmutableArray();
            this.MissingAssemblies = missngDlls.ToImmutableArray();
        }

        /// <summary>
        /// Gets the list of frameworks that are supported by this profile, all at the same time,
        /// that is, an intersection, not an union.
        /// </summary>
        public IntersectionSet<FrameworkName> SupportedFrameworks { get; private set; }

        /// <summary>
        /// Gets the name of the framework to which the information refers to.
        /// </summary>
        public FrameworkName FrameworkName { get; private set; }

        /// <summary>
        /// Gets a collection of information objects about the assemblies contained in this framework.
        /// </summary>
        public ImmutableArray<AssemblyInfo> AssemblyInfos { get; private set; }

        /// <summary>
        /// Gets a collection of assembly names that are missing from framework directory.
        /// </summary>
        public ImmutableArray<string> MissingAssemblies { get; private set; }

        /// <summary>
        /// Creates a FrameworkInfo given a FrameworkName asynchronously.
        /// </summary>
        /// <param name="frameworkName">The framework to get information to.</param>
        /// <returns>A Task that returns a FrameworkInfo object when all information is ready.</returns>
        public static async Task<FrameworkInfo> CreateAsync(FrameworkName frameworkName)
        {
            var directory = GetPolyDirectoryInfoFor(frameworkName);
            var dlls = GetFrameworkLibraries(directory, frameworkName);

            var assemblyInfos = dlls.Item1
                .Select(AssemblyInfo.GetAssemblyInfo)
                .ToArray();

            // reading framework metadata
            var supportDir = directory.GetDirectory("SupportedFrameworks");

            var supportedFrameworkInfos = new List<FrameworkFilter>();
            if (supportDir != null)
            {
                var supportXml = supportDir.GetFiles("*.xml");
                foreach (var fileInfo in supportXml)
                {
                    var data = await XmlHelpers.DesserializeAsync<SupportedFrameworkItem>(fileInfo.FullName);
                    var xpto = new FrameworkFilter(data);
                    supportedFrameworkInfos.Add(xpto);
                }
            }

            return new FrameworkInfo(frameworkName, assemblyInfos, supportedFrameworkInfos, dlls.Item2);
        }

        public static IEnumerable<FrameworkName> GetFrameworkNames()
        {
            return GetFrameworkNames(null, null)
                .Distinct()
                .OrderBy(x => x.Identifier)
                .ThenBy(x => x.Version)
                .ThenBy(x => x.Profile)
                .ToImmutableArray();
        }

        public static IEnumerable<FrameworkName> GetFrameworkNames(string path)
        {
            return GetFrameworkNames(new DirectoryInfo(path), null)
                .Distinct()
                .OrderBy(x => x.Identifier)
                .ThenBy(x => x.Version)
                .ThenBy(x => x.Profile)
                .ToImmutableArray();
        }

        private static IEnumerable<FrameworkName> GetFrameworkNames(
            [CanBeNull] DirectoryInfo baseDirInfo,
            [CanBeNull] string identifier,
            [CanBeNull] string version = null)
        {
            if (baseDirInfo == null)
            {
                var basePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                    @"Reference Assemblies\Microsoft\Framework");

                return GetFrameworkNames(new DirectoryInfo(basePath), null);
            }

            IEnumerable<FrameworkName> result = Enumerable.Empty<FrameworkName>();
            var dirs = baseDirInfo.GetDirectories();
            if (identifier == null)
            {
                foreach (var subDirInfo in dirs)
                {
                    var match = Regex.Match(subDirInfo.Name, @"^v(\d+(?:\.\d+(?:\.\d+(?:\.\d+)?)?)?)$");
                    if (match.Success)
                    {
                        result = result.Concat(
                            GetFrameworkNames(
                                subDirInfo,
                                ".NETFramework",
                                match.Groups[1].Value));
                    }
                    else
                    {
                        result = result.Concat(
                            GetFrameworkNames(
                                subDirInfo,
                                subDirInfo.Name));
                    }
                }
            }
            else if (version == null)
            {
                foreach (var subDirInfo in dirs)
                {
                    var match = Regex.Match(subDirInfo.Name, @"^v(\d+(?:\.\d+(?:\.\d+(?:\.\d+)?)?)?)$");
                    if (match.Success)
                    {
                        result = result.Concat(
                            GetFrameworkNames(
                                subDirInfo,
                                identifier,
                                match.Groups[1].Value));
                    }
                }
            }
            else
            {
                if (dirs.Any(d => d.Name == "RedistList"))
                    result = result.Concat(new[] { new FrameworkName(identifier, new Version(version)) });

                var dirSubsetList = dirs.SingleOrDefault(d => d.Name == "SubsetList");
                if (dirSubsetList != null)
                {
                    result = result.Concat(
                        dirSubsetList.GetFiles("*.xml")
                            .Select(f => Path.GetFileNameWithoutExtension(f.FullName))
                            .Select(n => new FrameworkName(identifier, new Version(version), n))
                            .ToArray());
                }

                var dirProfile = dirs.SingleOrDefault(d => d.Name == "Profile");

                if (dirProfile != null)
                {
                    foreach (var profileDirInfo in dirProfile.GetDirectories())
                        if (profileDirInfo.GetDirectories().Any(d => d.Name == "RedistList"))
                            result = result.Concat(new[] { new FrameworkName(identifier, new Version(version), profileDirInfo.Name) });
                }
            }

            return result;
        }

        [NotNull]
        private static Tuple<string[], string[]> GetFrameworkLibraries(
            DirectoryChain directory,
            FrameworkName frameworkName)
        {
            // NOTES:
            //          DirectoryChain
            //
            //  Represents multiple directories at the same time, like an inheritance chain.
            //  When files are searched, only one file with each name will be returned.
            //  If the same file exists in more than one node in the chain, the foremost one is returned.
            //  When directories are searched, if multiple are found, they will form a new DirectoryChain,
            //      ordered in correspondence with the parents.
            //
            //  This class allows profiles of frameworks, inheriting the parent framework DLLs,
            //  and other files, but also allowing it to override any file that overlaps.

            // getting all assemblies
            var dlls = directory.GetFiles("*.dll");

            // getting list of XML files representing profiles
            var subsetDir = directory.GetDirectories("SubsetList").SingleOrDefault();
            var profilesXml = subsetDir == null
                ? null
                : subsetDir.GetFiles(frameworkName.Profile + ".xml").SingleOrDefault();

            // getting XML that indicates a distribution
            var redistDir = directory.GetDirectories("RedistList").SingleOrDefault();
            var frameworkXml = redistDir == null ? null : redistDir.GetFiles("FrameworkList.xml").SingleOrDefault();

            // selecting either framework or profile XML
            var xml = frameworkXml ?? profilesXml;

            if (xml == null || xml.Length <= 0)
                return Tuple.Create(dlls.Select(x => x.FullName).ToArray(), new string[0]);

            var xdoc = XDocument.Load(xml.FullName);
            var filteredDllsInGac = xdoc
                .Descendants("File")
                .Where(x => x.AttributeI("InGac") != null && x.AttributeI("InGac").Value == "true")
                .Select(x => x.AttributeI("AssemblyName").Value + ".dll")
                .GroupBy(x => x)
                .ToDictionary(
                    x => x.Key,
                    x => dlls.SingleOrDefault(y => StringComparer.InvariantCultureIgnoreCase.Equals(y.Name, x.First())));

            var filteredDllsNotInGac = xdoc
                .Descendants("File")
                .Where(x => x.AttributeI("InGac") == null || x.AttributeI("InGac").Value != "true")
                .Select(x => x.AttributeI("AssemblyName").Value + ".dll")
                .GroupBy(x => x)
                .ToDictionary(
                    x => x.Key,
                    x => dlls.SingleOrDefault(y => StringComparer.InvariantCultureIgnoreCase.Equals(y.Name, x.First())));

            foreach (var kv in filteredDllsInGac)
                filteredDllsNotInGac.Remove(kv.Key);

            var missing = filteredDllsNotInGac.Where(x => x.Value == null).Select(x => x.Key).ToArray();

            foreach (var k in missing)
                filteredDllsNotInGac.Remove(k);

            var nonMissing = filteredDllsInGac
                .Where(x => x.Value != null)
                .Select(x => x.Value.FullName)
                .Concat(filteredDllsNotInGac.Select(x => x.Value.FullName))
                .ToArray();

            return Tuple.Create(nonMissing, missing);
        }

        private static DirectoryChain GetPolyDirectoryInfoFor(FrameworkName frameworkName)
        {
            var versionNum = "v" + frameworkName.Version;

            var basePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                @"Reference Assemblies\Microsoft\Framework",
                frameworkName.Identifier,
                versionNum);

            var basePathOld = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                @"Reference Assemblies\Microsoft\Framework",
                versionNum);

            var hasProfile = !string.IsNullOrEmpty(frameworkName.Profile);

            var profilePath = !hasProfile
                ? null
                : Path.Combine(
                    basePath,
                    @"Profile",
                    frameworkName.Profile);

            var profilePathOld = !hasProfile
                ? null
                : Path.Combine(
                    basePathOld,
                    @"Profile",
                    frameworkName.Profile);

            // listing all assemblies from base directories
            var dir = new DirectoryChain(
                new[] { basePath, basePathOld, profilePath, profilePathOld }
                    .Where(x => !string.IsNullOrEmpty(x))
                    .Where(Directory.Exists)
                    .ToArray());

            return dir;
        }

        /// <summary>
        /// Determines whether the current framework is a superset of another framework,
        /// that is, if the other framework can be swapped with the current one.
        /// </summary>
        /// <param name="other">The framework to test.</param>
        /// <returns></returns>
        public bool? IsSupersetOf(FrameworkInfo other)
        {
            var supportsAllFrmks = this.SupportedFrameworks.Contains(other.SupportedFrameworks);

            if (supportsAllFrmks != null)
                return supportsAllFrmks.Value;

            var thisTypes = this.AssemblyInfos
                .SelectMany(x => x.NamedTypes)
                .Select(x => x.Key)
                .ToImmutableHashSet(StringComparer.InvariantCultureIgnoreCase);

            var otherTypes = other.AssemblyInfos
                .SelectMany(x => x.NamedTypes)
                .Select(x => x.Key);

            if (!otherTypes.All(thisTypes.Contains))
                return false;

            return null;
        }

        public override string ToString()
        {
            return this.FrameworkName.ToString();
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlRoot("Framework")]
        public class SupportedFrameworkItem
        {
            [XmlAttribute]
            public string Identifier { get; set; }

            [XmlAttribute]
            public string Profile { get; set; }

            [XmlAttribute]
            public string MinimumVersion { get; set; }

            [XmlAttribute]
            public string Family { get; set; }

            [XmlAttribute]
            public string MinimumVisualStudioVersion { get; set; }

            [XmlAttribute]
            public string MaximumVisualStudioVersion { get; set; }

            [XmlAttribute]
            public string DisplayName { get; set; }

            [XmlAttribute]
            public string MinimumVersionDisplayName { get; set; }

            [XmlAttribute]
            public string PlatformArchitectures { get; set; }

            [CanBeNull]
            [XmlElement("Platform")]
            public PlatformItem Platform { get; set; }

            [EditorBrowsable(EditorBrowsableState.Never)]
            public class PlatformItem
            {
                [XmlAttribute]
                public string Identifier { get; set; }

                [XmlAttribute]
                public string MinimumVersion { get; set; }
            }
        }
    }

    public static class XDocExtensions
    {
        public static XAttribute Attribute(this XElement element, string name, IEqualityComparer<string> comparer)
        {
            return element.Attributes().SingleOrDefault(
                xa => comparer.Equals(xa.Name.LocalName, name));
        }

        public static XAttribute AttributeI(this XElement element, string name)
        {
            return element.Attributes().SingleOrDefault(
                xa => StringComparer.InvariantCultureIgnoreCase.Equals(xa.Name.LocalName, name));
        }

        public static XElement Element(this XElement element, string name, IEqualityComparer<string> comparer)
        {
            return element.Elements().SingleOrDefault(
                xa => comparer.Equals(xa.Name.LocalName, name));
        }
    }
}