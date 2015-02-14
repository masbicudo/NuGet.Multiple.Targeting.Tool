using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics;
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
    public class FrameworkInfo : IEquatable<FrameworkInfo>
    {
        private readonly IFrameworkInfoCache cache;

        public FrameworkInfo(
            [NotNull] FrameworkName frameworkName,
            [NotNull] IEnumerable<AssemblyInfo> assemblyInfos,
            [CanBeNull] IEnumerable<IUndeterminedSet<FrameworkName>> supportedFrameworks,
            [CanBeNull] IEnumerable<string> missingDlls)
        {
            if (frameworkName == null)
                throw new ArgumentNullException("frameworkName");

            if (assemblyInfos == null)
                throw new ArgumentNullException("assemblyInfos");

            missingDlls = missingDlls ?? Enumerable.Empty<string>();

            this.SupportedFrameworks = supportedFrameworks == null
                ? new FrameworkNameSet(frameworkName)
                : new IntersectionSet<FrameworkName>(supportedFrameworks) as IUndeterminedSet<FrameworkName>;

            this.FrameworkName = frameworkName;
            this.AssemblyInfos = assemblyInfos.ToImmutableArray();
            this.MissingAssemblies = missingDlls.ToImmutableArray();
        }

        /// <summary>
        /// Gets the list of frameworks that are supported by this profile, all at the same time,
        /// that is, an intersection, not an union.
        /// </summary>
        [NotNull]
        public IUndeterminedSet<FrameworkName> SupportedFrameworks { get; private set; }

        /// <summary>
        /// Gets the name of the framework to which the information refers to.
        /// </summary>
        [NotNull]
        public FrameworkName FrameworkName { get; private set; }

        /// <summary>
        /// Gets a collection of information objects about the assemblies contained in this framework.
        /// </summary>
        public ImmutableArray<AssemblyInfo> AssemblyInfos { get; private set; }

        /// <summary>
        /// Gets a collection of assembly names that are missing from framework directory.
        /// </summary>
        public ImmutableArray<string> MissingAssemblies { get; private set; }

        private readonly AsyncLock lockerGetAssemblyInfoByRelativePath = new AsyncLock();

        private Dictionary<string, AssemblyInfo> assembliesByRelPath;

        [NotNull]
        public async Task<AssemblyInfo> GetAssemblyInfoByRelativePathAsync([NotNull] string relativePath)
        {
            if (relativePath == null)
                throw new ArgumentNullException("relativePath");

            if (!relativePath.StartsWith("~\\"))
                throw new ArgumentException(@"Relative paths must start with ""~\""", "relativePath");

            if (this.assembliesByRelPath == null)
                using (await this.lockerGetAssemblyInfoByRelativePath)
                    this.assembliesByRelPath = this.assembliesByRelPath
                                               ?? this.AssemblyInfos.ToDictionary(
                                                   a => a.RelativePath,
                                                   StringComparer.InvariantCultureIgnoreCase);

            AssemblyInfo value;
            this.assembliesByRelPath.TryGetValue(relativePath, out value);
            return value;
        }

        /// <summary>
        /// Gets or creates a FrameworkInfo given a FrameworkName asynchronously,
        /// but using a static cache to retrieve and store data.
        /// </summary>
        /// <param name="frameworkName">The framework to get information to.</param>
        /// <returns>A Task that returns a FrameworkInfo object when all information is ready.</returns>
        public static Task<FrameworkInfo> GetOrCreateAsync(FrameworkName frameworkName)
        {
            return CreateAsync(frameworkName, true);
        }

        /// <summary>
        /// Creates a FrameworkInfo given a FrameworkName asynchronously.
        /// </summary>
        /// <param name="frameworkName">The framework to get information to.</param>
        /// <param name="useCache">
        /// Whether to use a global cache or not.
        /// When true, reads existing values from the cache, and also stores new values in the cache.
        /// </param>
        /// <returns>A Task that returns a FrameworkInfo object when all information is ready.</returns>
        public static async Task<FrameworkInfo> CreateAsync(FrameworkName frameworkName, bool useCache)
        {
            IFrameworkInfoCache cache = null;
            if (useCache)
            {
                cache = await MyIoC.GetAsync<IFrameworkInfoCache>();
                var frmkInfo = await cache.GetValueAsync(frameworkName);
                if (frmkInfo != null)
                {
                    ConsoleHelper.WriteLine("Framework " + frameworkName, ConsoleColor.DarkYellow, 0);
                    return frmkInfo;
                }
            }

            ConsoleHelper.WriteLine("Framework " + frameworkName, ConsoleColor.Yellow, 0);

            var directory = PathHelper.GetPolyDirectoryInfoFor(frameworkName);
            var dlls = GetFrameworkLibraries(directory, frameworkName);

            var assemblyInfos = dlls.Item1
                .Select(AssemblyInfo.GetAssemblyInfo)
                .ToArray();

            // reading framework metadata
            var supportDir = directory.GetDirectory("SupportedFrameworks");

            List<FrameworkFilter> supportedFrameworkInfos = null;
            if (supportDir != null)
            {
                var supportXml = supportDir.GetFiles("*.xml");
                foreach (var fileInfo in supportXml)
                {
                    var data = await XmlHelpers.DesserializeAsync<SupportedFrameworkItem>(fileInfo.FullName);
                    var xpto = new FrameworkFilter(data);
                    supportedFrameworkInfos = supportedFrameworkInfos ?? new List<FrameworkFilter>();
                    supportedFrameworkInfos.Add(xpto);
                }
            }

            var builder = await MyIoC.GetAsync<FrameworkInfoBuilder>();
            var result = builder.Create(frameworkName, assemblyInfos, supportedFrameworkInfos, dlls.Item2);

            if (cache != null)
            {
                await cache.SetItemAsync(frameworkName, result);
            }

            return result;
        }

        public static async Task<HierarchyGraph> GetFrameworkGraph()
        {
            var allFrmkInfo = await FrameworkInfo.GetFrameworkInfos();
            var nodes = HierarchyGraph.Create(allFrmkInfo);

            foreach (var node in nodes)
                node.Visit(path => ConsoleHelper.WriteLine(path.Peek().ToString(), ConsoleColor.White, path.Count() - 1));

            return new HierarchyGraph(null, nodes);
        }

        public static async Task<FrameworkInfo[]> GetFrameworkInfos()
        {
            var allFrameworks = GetFrameworkNames();
            var allFrmkInfo = await Task.WhenAll(allFrameworks.Select(GetOrCreateAsync));
            return allFrmkInfo;
        }

        public static IEnumerable<FrameworkName> GetFrameworkNames(
            [CanBeNull] string baseFrameworksPath = null,
            [CanBeNull] string identifier = null,
            [CanBeNull] string version = null)
        {
            var dirInfo = baseFrameworksPath == null ? null : new DirectoryInfo(baseFrameworksPath);
            return GetFrameworkNamesCore(dirInfo, null, null)
                .Distinct()
                .OrderBy(x => x.Identifier)
                .ThenBy(x => x.Version)
                .ThenBy(x => x.Profile)
                .ToImmutableArray();
        }

        private static IEnumerable<FrameworkName> GetFrameworkNamesCore(
            [CanBeNull] DirectoryInfo baseDirInfo,
            [CanBeNull] string identifier,
            [CanBeNull] string version)
        {
            if (baseDirInfo == null)
            {
                var basePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                    @"Reference Assemblies\Microsoft\Framework");

                return GetFrameworkNamesCore(new DirectoryInfo(basePath), null, null);
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
                            GetFrameworkNamesCore(
                                subDirInfo,
                                ".NETFramework",
                                match.Groups[1].Value));
                    }
                    else
                    {
                        result = result.Concat(
                            GetFrameworkNamesCore(
                                subDirInfo,
                                subDirInfo.Name,
                                null));
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
                            GetFrameworkNamesCore(
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
                            result = result.Concat(
                                new[] { new FrameworkName(identifier, new Version(version), profileDirInfo.Name) });
                }
            }

            return result;
        }

        [CanBeNull]
        public string GetAssemblyPath([NotNull] AssemblyInfo assemblyInfo)
        {
            if (assemblyInfo == null)
                throw new ArgumentNullException("assemblyInfo");

            Debug.Assert(assemblyInfo.RelativePath.StartsWith("~\\"), "assemblyInfo.RelativePath.StartsWith(\"~\\\")");

            var directory = PathHelper.GetPolyDirectoryInfoFor(this.FrameworkName);
            var pathParts = assemblyInfo.RelativePath
                .Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries);

            Debug.Assert(pathParts.Length > 1, "pathParts.Length > 1");

            for (int it = 1; it < pathParts.Length - 1; it++)
                if (directory != null)
                    directory = directory.GetDirectory(pathParts[it]);

            var file = directory == null ? null : directory.GetFile(pathParts[pathParts.Length - 1]);

            return file == null ? null : file.FullName;
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

        /// <summary>
        /// Determines whether the current framework is a superset of another framework,
        /// that is, if the other framework can be swapped with the current one.
        /// </summary>
        /// <param name="other">The framework to test.</param>
        /// <returns>True when it is a super set; False when not a super set; null when not sure.</returns>
        public bool? IsSupersetOf(FrameworkInfo other)
        {
            var thisSupportedSet = this.SupportedFrameworks == null
                ? new FrameworkNameSet(this.FrameworkName)
                : this.SupportedFrameworks as IUndeterminedSet<FrameworkName>;

            var otherSupportedSet = other.SupportedFrameworks == null
                ? new FrameworkNameSet(other.FrameworkName)
                : other.SupportedFrameworks as IUndeterminedSet<FrameworkName>;

            var supportsAllFrmks = thisSupportedSet.Contains(otherSupportedSet);

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

        public IEnumerable<FrameworkInfo> FindSubsets(IEnumerable<FrameworkInfo> others)
        {
            foreach (var other in others)
                if (this.IsSupersetOf(other) == true && other.IsSupersetOf(this) != true)
                    yield return other;
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

        public bool Equals(FrameworkInfo other)
        {
            return other != null
                   && this.IsSupersetOf(other) == true
                   && other.IsSupersetOf(this) == true;
        }
    }
}