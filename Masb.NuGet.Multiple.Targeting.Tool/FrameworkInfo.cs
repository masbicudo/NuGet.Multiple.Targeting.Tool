using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
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

        public FrameworkName FrameworkName { get; private set; }

        public ImmutableArray<AssemblyInfo> AssemblyInfos { get; private set; }

        public static async Task<FrameworkInfo> CreateAsync(FrameworkName frameworkName)
        {
            var directory = MultiDirectoryInfo(frameworkName);
            var assemblyInfos = GetFrameworkLibraries(directory, frameworkName)
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

        private static string[] GetFrameworkLibraries(MultiDirectoryInfo directory, FrameworkName frameworkName)
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
                .Where(x => x.Attribute("InGac") != null && x.Attribute("InGac").Value == "true")
                .Select(x => x.Attribute("AssemblyName").Value + ".dll")
                .ToDictionary(x => x, x => dlls.SingleOrDefault(y => StringComparer.InvariantCultureIgnoreCase.Equals(y.Name, x)));

            var filteredDllsNotInGac = xdoc
                .Descendants("File")
                .Where(x => x.Attribute("InGac") == null || x.Attribute("InGac").Value != "true")
                .Select(x => x.Attribute("AssemblyName").Value + ".dll")
                .ToDictionary(x => x, x => dlls.SingleOrDefault(y => StringComparer.InvariantCultureIgnoreCase.Equals(y.Name, x)));

            if (filteredDllsNotInGac.Any(x => x.Value == null))
                throw new Exception("Required file was not found.");

            return filteredDllsInGac.Where(x => x.Value != null).Select(x => x.Value.FullName)
                .Concat(filteredDllsNotInGac.Select(x => x.Value.FullName))
                .ToArray();
        }

        private static MultiDirectoryInfo MultiDirectoryInfo(FrameworkName frameworkName)
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

        public bool IsSupersetOf(FrameworkInfo other)
        {
            var supportsAllFrmks = this.SupportedFrameworks.Contains(other.SupportedFrameworks);

            if (supportsAllFrmks == false)
                return false;

            var thisTypes = this.AssemblyInfos
                .SelectMany(x => x.NamedTypes)
                .Select(x => x.Key)
                .ToImmutableHashSet(StringComparer.InvariantCultureIgnoreCase);

            var otherTypes = other.AssemblyInfos
                .SelectMany(x => x.NamedTypes)
                .Select(x => x.Key);

            if (!otherTypes.All(thisTypes.Contains))
                return false;

            return true;
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
}