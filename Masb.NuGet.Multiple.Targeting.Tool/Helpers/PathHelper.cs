using System;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using JetBrains.Annotations;

namespace Masb.NuGet.Multiple.Targeting.Tool.Helpers
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

        [ContractAnnotation("path:null=>false; =>true,relativePath:notnull; =>false, relativePath:null")]
        public static bool TryGetFrameworkRelativePath([NotNull] string path, out string relativePath)
        {
            if (path == null)
                throw new ArgumentNullException("path");

            if (TryRemoveFrameworkRootPath(ref path) || path.StartsWith("\\"))
            {
                var match = FrmkPathRegex.Match(path);
                if (match.Success)
                {
                    var file = match.Groups["FILE"].Value;
                    relativePath = "~\\" + file;
                    return true;
                }
            }

            relativePath = null;
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

        public static string Combine(string a, string b)
        {
            if (Path.IsPathRooted(b))
                return EatDotPathParts(b);

            var path = Path.Combine(a, b);
            return EatDotPathParts(path);
        }

        private static string EatDotPathParts(string path)
        {
            var parts = path.Split('\\');
            var dest = 0;
            var minDest = Path.IsPathRooted(path) ? 1 : int.MinValue;
            for (int it = 0; it < parts.Length; it++)
            {
                var part = parts[it];
                if (part == ".")
                {
                }
                else if (part == "..")
                {
                    if (dest-- < minDest)
                        throw new Exception("Invalid path combination.");
                }
                else
                {
                    parts[Math.Abs(dest++)] = part;
                }
            }

            if (dest < 0)
                for (int it = 0; it < Math.Abs(dest); it++)
                    parts[it] = "..";

            return string.Join("\\", parts, 0, Math.Abs(dest));
        }

        public static string GetRelativePath([NotNull] string fromDirectory, [NotNull] string to)
        {
            if (fromDirectory == null)
                throw new ArgumentNullException("fromDirectory");

            if (to == null)
                throw new ArgumentNullException("to");

            if (!Path.IsPathRooted(fromDirectory))
                throw new ArgumentException("Argment 'fromDirectory' must be an absolute path, that referes to a directory.", "fromDirectory");

            if (!Path.IsPathRooted(to))
                return to;

            var fromParts = fromDirectory.Split('\\');

            if (fromParts.Length == 1)
                fromParts = new[] { fromParts[0], "" };

            var toParts = to.Split('\\');

            if (toParts.Length == 1)
                toParts = new[] { toParts[0], "" };

            int eqs = 0;
            for (; ; eqs++)
            {
                if (eqs >= fromParts.Length - 1)
                    break;

                if (eqs >= toParts.Length - 1)
                    break;

                if (!fromParts[eqs].Equals(toParts[eqs], StringComparison.CurrentCultureIgnoreCase))
                    break;
            }

            return string.Join(
                "\\",
                Enumerable.Range(eqs, fromParts.Length - eqs - 1).Select(x => "..")
                .Concat(toParts.Skip(eqs)));
        }
    }
}