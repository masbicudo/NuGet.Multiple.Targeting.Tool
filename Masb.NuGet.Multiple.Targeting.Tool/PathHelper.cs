using System;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using JetBrains.Annotations;

namespace Masb.NuGet.Multiple.Targeting.Tool
{
    public static class PathHelper
    {
        public static string FrameworkRootPath
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                    @"Reference Assemblies\Microsoft\Framework");
            }
        }

        public static bool TryRemoveFrameworkRootPath(ref string path)
        {
            var frmkRootPath = FrameworkRootPath;
            bool isInFrmkPath = path.StartsWith(frmkRootPath, StringComparison.InvariantCultureIgnoreCase);
            if (isInFrmkPath)
                path = path.Substring(frmkRootPath.Length);
            return isInFrmkPath;
        }

        private static readonly Regex FrmkPathRegex = new Regex(
            @"
                ^
                (?:
                    \\(?<FRMK>[^\\]*)
                )?
                \\v(?<VER>\d+(?:\.\d+(?:\.\d+(?:\.\d+)?)?)?)
                (?:
                    \\Profile
                    \\(?<PROF>[^\\]*)
                )?
                (?:
                    \\(?<FILE>.*)
                )?$
                ",
            RegexOptions.IgnorePatternWhitespace | RegexOptions.IgnoreCase);

        [CanBeNull]
        public static bool TryGetFrameworkRelativePath([NotNull] ref string path)
        {
            if (path == null)
                throw new ArgumentNullException("path");

            if (TryRemoveFrameworkRootPath(ref path) || path.StartsWith("\\"))
            {
                var match = FrmkPathRegex.Match(path);
                if (match.Success)
                {
                    var file = match.Groups["FILE"].Value;
                    path = "~\\" + file;
                    return true;
                }
            }

            return false;
        }

        [CanBeNull]
        public static FrameworkName GetFrameworkName([NotNull] string path)
        {
            if (path == null)
                throw new ArgumentNullException("path");

            if (TryRemoveFrameworkRootPath(ref path) || path.StartsWith("\\"))
            {
                var match = FrmkPathRegex.Match(path);
                if (match.Success)
                {
                    var frmk = match.Groups["FRMK"].Value;
                    if (string.IsNullOrWhiteSpace(frmk))
                        frmk = ".NETFramework";

                    return new FrameworkName(
                        frmk,
                        new Version(match.Groups["VER"].Value),
                        match.Groups["PROF"].Value);
                }
            }

            return null;
        }

        [NotNull]
        public static string BasePath([NotNull] FrameworkName frameworkName)
        {
            if (frameworkName == null)
                throw new ArgumentNullException("frameworkName");

            var versionNum = "v" + frameworkName.Version;
            return Path.Combine(FrameworkRootPath, frameworkName.Identifier, versionNum);
        }

        [CanBeNull]
        public static string OldBasePath([NotNull] FrameworkName frameworkName)
        {
            if (frameworkName == null)
                throw new ArgumentNullException("frameworkName");

            if (StringComparer.InvariantCultureIgnoreCase.Equals(frameworkName.Identifier, ".NETFramework"))
                return null;

            var versionNum = "v" + frameworkName.Version;
            return Path.Combine(FrameworkRootPath, versionNum);
        }

        [CanBeNull]
        public static string BasePathWithProfile([NotNull] FrameworkName frameworkName)
        {
            var basePath = BasePath(frameworkName);
            var hasProfile = !string.IsNullOrEmpty(frameworkName.Profile);

            var profilePath = !hasProfile
                ? null
                : Path.Combine(
                    basePath,
                    @"Profile",
                    frameworkName.Profile);

            return profilePath;
        }

        [CanBeNull]
        public static string OldBasePathWithProfile([NotNull] FrameworkName frameworkName)
        {
            var basePath = OldBasePath(frameworkName);
            var hasProfile = !string.IsNullOrEmpty(frameworkName.Profile);

            var profilePath = basePath == null || !hasProfile
                ? null
                : Path.Combine(
                    basePath,
                    @"Profile",
                    frameworkName.Profile);

            return profilePath;
        }

        public static DirectoryChain GetPolyDirectoryInfoFor(FrameworkName frameworkName)
        {
            // getting base directories of the framework
            var basePath = PathHelper.BasePath(frameworkName);
            var basePathOld = PathHelper.OldBasePath(frameworkName);
            var profilePath = PathHelper.BasePathWithProfile(frameworkName);
            var profilePathOld = PathHelper.OldBasePathWithProfile(frameworkName);

            // listing all assemblies from base directories
            var dir = new DirectoryChain(
                new[] { basePath, basePathOld, profilePath, profilePathOld }
                    .Where(x => !string.IsNullOrEmpty(x))
                    .Where(Directory.Exists)
                    .ToArray());

            return dir;
        }
    }
}