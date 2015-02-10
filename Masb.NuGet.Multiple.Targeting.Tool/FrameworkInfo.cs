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
            IEnumerable<IUndeterminedSet<FrameworkName>> supportedFrameworks)
        {
            this.SupportedFrameworks = new IntersectionSet<FrameworkName>(supportedFrameworks);
            this.FrameworkName = frameworkName;
            this.AssemblyInfos = assemblyInfos.ToImmutableArray();
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
        /// Creates a FrameworkInfo given a FrameworkName asynchronously.
        /// </summary>
        /// <param name="frameworkName">The framework to get information to.</param>
        /// <returns>A Task that returns a FrameworkInfo object when all information is ready.</returns>
        public static async Task<FrameworkInfo> CreateAsync(FrameworkName frameworkName)
        {
            return await CreateAsync(frameworkName, true);
        }

        /// <summary>
        /// Creates a FrameworkInfo given a FrameworkName asynchronously.
        /// </summary>
        /// <param name="frameworkName">The framework to get information to.</param>
        /// <param name="ignoreMissing">Whether to ignore or not frameworks with missing assemblies.</param>
        /// <returns>A Task that returns a FrameworkInfo object when all information is ready.</returns>
        public static async Task<FrameworkInfo> CreateAsync(FrameworkName frameworkName, bool ignoreMissing)
        {
            var directory = GetPolyDirectoryInfoFor(frameworkName);
            var dlls = GetFrameworkLibraries(directory, frameworkName, ignoreMissing);

            if (dlls == null)
                return null;

            var assemblyInfos = dlls
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

            return new FrameworkInfo(frameworkName, assemblyInfos, supportedFrameworkInfos);
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
            [CanBeNull] FrameworkNameBuiler name)
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
            if (name == null || name.Identifier == null)
            {
                foreach (var subDirInfo in dirs)
                {
                    var match = Regex.Match(subDirInfo.Name, @"^v(\d+(?:\.\d+(?:\.\d+(?:\.\d+)?)?)?)$");
                    if (match.Success)
                    {
                        result = result.Concat(
                            GetFrameworkNames(
                                subDirInfo,
                                new FrameworkNameBuiler(".NETFramework", match.Groups[1].Value)));
                    }
                    else
                    {
                        result = result.Concat(
                            GetFrameworkNames(
                                subDirInfo,
                                new FrameworkNameBuiler(subDirInfo.Name)));
                    }
                }
            }
            else if (name.Version == null)
            {
                foreach (var subDirInfo in dirs)
                {
                    var match = Regex.Match(subDirInfo.Name, @"^v(\d+(?:\.\d+(?:\.\d+(?:\.\d+)?)?)?)$");
                    if (match.Success)
                    {
                        result = result.Concat(
                            GetFrameworkNames(
                                subDirInfo,
                                new FrameworkNameBuiler(name.Identifier, match.Groups[1].Value)));
                    }
                }
            }
            else if (name.Profile == null)
            {
                if (dirs.Any(d => d.Name == "RedistList"))
                    result = result.Concat(new[] { name.ToFrameworkName() });

                var dirSubsetList = dirs.SingleOrDefault(d => d.Name == "SubsetList");
                if (dirSubsetList != null)
                {
                    result = result.Concat(
                        dirSubsetList.GetFiles("*.xml")
                            .Select(f => Path.GetFileNameWithoutExtension(f.FullName))
                            .Select(n => new FrameworkName(name.Identifier, new Version(name.Version), n))
                            .ToArray());
                }

                var dirProfile = dirs.SingleOrDefault(d => d.Name == "Profile");

                if (dirProfile != null)
                {
                    foreach (var profileDirInfo in dirProfile.GetDirectories())
                        if (profileDirInfo.GetDirectories().Any(d => d.Name == "RedistList"))
                            result = result.Concat(new[] { new FrameworkName(name.Identifier, new Version(name.Version), profileDirInfo.Name) });
                }
            }

            return result;
        }

        [CanBeNull]
        private static string[] GetFrameworkLibraries(MultiDirectoryInfo directory, FrameworkName frameworkName, bool ignoreMissing)
        {
            // getting all assemblies
            var dlls = directory.GetFiles("*.dll");
            var subsetDir = directory.GetDirectories("SubsetList").SingleOrDefault();
            var profilesXml = subsetDir == null ? null : subsetDir.GetFiles(frameworkName.Profile + ".xml").SingleOrDefault();
            var redistDir = directory.GetDirectories("RedistList").SingleOrDefault();
            var frameworkXml = redistDir == null ? null : redistDir.GetFiles("FrameworkList.xml").SingleOrDefault();
            var xml = frameworkXml ?? profilesXml;

            if (xml == null || xml.Length <= 0)
                return dlls.Select(x => x.FullName).ToArray();

            var xdoc = XDocument.Load(xml.FullName);
            var filteredDllsInGac = xdoc
                .Descendants("File")
                .Where(x => x.Attribute("InGac", StringComparer.InvariantCultureIgnoreCase) != null && x.Attribute("InGac", StringComparer.InvariantCultureIgnoreCase).Value == "true")
                .Select(x => x.Attribute("AssemblyName", StringComparer.InvariantCultureIgnoreCase).Value + ".dll")
                .ToDictionary(x => x, x => dlls.SingleOrDefault(y => StringComparer.InvariantCultureIgnoreCase.Equals(y.Name, x)));

            var filteredDllsNotInGac = xdoc
                .Descendants("File")
                .Where(x => x.Attribute("InGac", StringComparer.InvariantCultureIgnoreCase) == null || x.Attribute("InGac", StringComparer.InvariantCultureIgnoreCase).Value != "true")
                .Select(x => x.Attribute("AssemblyName", StringComparer.InvariantCultureIgnoreCase).Value + ".dll")
                .ToDictionary(x => x, x => dlls.SingleOrDefault(y => StringComparer.InvariantCultureIgnoreCase.Equals(y.Name, x)));

            foreach (var kvInGac in filteredDllsInGac)
                filteredDllsNotInGac.Remove(kvInGac.Key);

            if (filteredDllsNotInGac.Any(x => x.Value == null))
            {
                if (ignoreMissing)
                    return null;

                throw new Exception("Required file was not found.");
            }

            return filteredDllsInGac.Where(x => x.Value != null).Select(x => x.Value.FullName)
                .Concat(filteredDllsNotInGac.Select(x => x.Value.FullName))
                .ToArray();
        }

        private static MultiDirectoryInfo GetPolyDirectoryInfoFor(FrameworkName frameworkName)
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
            var dir = new MultiDirectoryInfo(
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

        public static XElement Element(this XElement element, string name, IEqualityComparer<string> comparer)
        {
            return element.Elements().SingleOrDefault(
                xa => comparer.Equals(xa.Name.LocalName, name));
        }
    }
}