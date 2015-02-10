using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Text.RegularExpressions;
using JetBrains.Annotations;

namespace Masb.NuGet.Multiple.Targeting.Tool
{
    public class FrameworkFilter : IUndeterminedSet<FrameworkName>
    {
        private string fullName;

        internal FrameworkFilter([NotNull] FrameworkInfo.SupportedFrameworkItem data)
        {
            if (data == null)
                throw new ArgumentNullException("data");

            this.DisplayName = data.DisplayName ?? "";
            this.Family = data.Family ?? "";
            this.Identifier = data.Identifier ?? "";
            this.MinimumVersion = GetVersion(data.MinimumVersion ?? "");
            this.MinimumVersionDisplayName = data.MinimumVersionDisplayName ?? "";
            this.MinimumVisualStudioVersion = GetVersion(data.MinimumVisualStudioVersion ?? "");
            this.MaximumVisualStudioVersion = GetVersion(data.MaximumVisualStudioVersion ?? "");
            this.Platform = data.Platform == null ? null : new PlatformFilter(data.Platform);
            this.PlatformArchitectures = GetSplit(';', data.PlatformArchitectures ?? "");
            this.Profile = data.Profile ?? "";
        }

        private static Version GetVersion(string version)
        {
            if (version == null)
                return null;

            Version result;
            Version.TryParse(version, out result);
            return result;
        }

        private static IEnumerable<string> GetSplit(char sep, string str)
        {
            if (string.IsNullOrWhiteSpace(str))
                return Enumerable.Empty<string>();

            return str.Split(new[] { sep }).Select(x => x.Trim()).ToArray();
        }

        public string DisplayName { get; private set; }
        public string Family { get; private set; }
        public string Identifier { get; private set; }
        public Version MinimumVersion { get; private set; }
        public string MinimumVersionDisplayName { get; private set; }
        public Version MinimumVisualStudioVersion { get; private set; }
        public PlatformFilter Platform { get; private set; }
        public IEnumerable<string> PlatformArchitectures { get; private set; }
        public string Profile { get; private set; }
        public Version MaximumVisualStudioVersion { get; private set; }

        public class PlatformFilter
        {
            private string fullName;

            internal PlatformFilter([NotNull] FrameworkInfo.SupportedFrameworkItem.PlatformItem data)
            {
                if (data == null)
                    throw new ArgumentNullException("data");

                this.Identifier = data.Identifier ?? "";
                this.MinimumVersion = GetVersion(data.MinimumVersion ?? "");
            }

            public string Identifier { get; private set; }
            public Version MinimumVersion { get; private set; }

            public string FullName
            {
                get
                {
                    if (this.fullName == null)
                    {
                        var stringBuilder = new StringBuilder();
                        stringBuilder.AppendFormat("{0},Version=v{1}+", this.Identifier, this.MinimumVersion);
                        this.fullName = stringBuilder.ToString();
                    }

                    return this.fullName;
                }
            }

            public override string ToString()
            {
                return this.FullName;
            }
        }

        public string FullName
        {
            get
            {
                if (this.fullName == null)
                {
                    var stringBuilder = new StringBuilder();
                    stringBuilder.AppendFormat("{0},Version=v{1}+", this.Identifier, this.MinimumVersion);
                    if (!string.IsNullOrWhiteSpace(this.Profile))
                        stringBuilder.AppendFormat(",Profile={0}", this.Profile);
                    this.fullName = stringBuilder.ToString();
                }

                return this.fullName;
            }
        }

        private static bool IsStrMatch(string input, string pattern)
        {
            return Regex.IsMatch(input, @"^" + pattern.Replace(".", @"\.").Replace("*", @".*") + @"$", RegexOptions.IgnoreCase);
        }

        private static readonly string a2z = new string(Enumerable.Range('a', 'z').Select(x => (char)x).ToArray());

        private static int CompareMaxVersions(Version a, Version b)
        {
            if (a == null)
                return b == null ? 0 : -1;

            if (b == null)
                return +1;

            return Comparer<Version>.Default.Compare(a, b);
        }

        private static int CompareMinVersions(Version a, Version b)
        {
            if (a == null)
                return b == null ? 0 : +1;

            if (b == null)
                return -1;

            return Comparer<Version>.Default.Compare(a, b);
        }

        public override string ToString()
        {
            return this.FullName;
        }

        /// <summary>
        /// Determines whether an item is inside the set.
        /// </summary>
        /// <param name="frameworkName">FrameworkName to test.</param>
        /// <returns>Returns a boolean indicating whether the item is in the set.</returns>
        public bool Contains([NotNull] FrameworkName frameworkName)
        {
            if (frameworkName == null)
                throw new ArgumentNullException("frameworkName");

            return IsStrMatch(frameworkName.Identifier, this.Identifier)
                   && (this.MinimumVersion == null || frameworkName.Version >= this.MinimumVersion)
                   && IsStrMatch(frameworkName.Profile, this.Profile);
        }

        public bool? Intersects([NotNull] IUndeterminedSet<FrameworkName> set)
        {
            if (set == null)
                throw new ArgumentNullException("set");

            if (set.IsEmpty() == true)
                return false;

            var frmkNameSet = set as FrameworkNameSet;
            if (frmkNameSet != null)
                return this.Intersects(frmkNameSet);

            var filter = set as FrameworkFilter;
            if (filter != null)
                return this.Intersects(filter);

            return null;
        }

        public bool? IsEmpty()
        {
            return false;
        }

        public bool Intersects([NotNull] FrameworkNameSet frmkNameSet)
        {
            if (frmkNameSet == null)
                throw new ArgumentNullException("frmkNameSet");

            return Intersects(this, frmkNameSet.FrameworkName);
        }

        internal static bool Intersects([NotNull] FrameworkFilter filter, [NotNull] FrameworkName frmkName)
        {
            return IsStrMatch(frmkName.Identifier, filter.Identifier)
                   && (filter.MinimumVersion == null || frmkName.Version.Major >= filter.MinimumVersion.Major)
                   && IsStrMatch(frmkName.Profile, filter.Profile);
        }

        public bool Intersects([NotNull] FrameworkFilter filter)
        {
            if (filter == null)
                throw new ArgumentNullException("filter");

            var intersectsId = IsStrMatch(filter.Identifier, this.Identifier)
                               || IsStrMatch(this.Identifier, filter.Identifier)
                               || StringComparer.InvariantCultureIgnoreCase.Equals(
                                   this.Identifier.Replace("*", ""),
                                   filter.Identifier.Replace("*", ""));

            var intersectsProf = IsStrMatch(filter.Profile, this.Profile)
                               || IsStrMatch(this.Profile, filter.Profile)
                               || StringComparer.InvariantCultureIgnoreCase.Equals(
                                   this.Profile.Replace("*", ""),
                                   filter.Profile.Replace("*", ""));

            return intersectsId && intersectsProf;
        }

        /// <summary>
        /// Determines whether another set is a subset.
        /// </summary>
        /// <param name="set">The set to test.</param>
        /// <returns>
        /// Returns a optional boolean indicating whether the set is a subset for sure,
        ///  or not, returning null when it is not possible to know.
        /// </returns>
        public bool? Contains(IUndeterminedSet<FrameworkName> set)
        {
            var frameworkFilter = set as FrameworkFilter;
            if (frameworkFilter != null)
            {
                var result = this.Contains(frameworkFilter);
                return result;
            }

            var nset = set as FrameworkNameSet;
            if (nset != null)
                return this.Contains(nset.FrameworkName);

            return null;
        }

        public bool Contains(FrameworkFilter frameworkFilter)
        {
            return IsStrMatch(frameworkFilter.Identifier.Replace("*", a2z), this.Identifier)
                   && IsStrMatch(frameworkFilter.Identifier.Replace("*", ""), this.Identifier)
                   && IsStrMatch(frameworkFilter.Profile.Replace("*", a2z), this.Profile)
                   && IsStrMatch(frameworkFilter.Profile.Replace("*", ""), this.Profile)
                   && CompareMinVersions(frameworkFilter.MinimumVersion, this.MinimumVersion) >= 0
                   && CompareMinVersions(frameworkFilter.MinimumVisualStudioVersion, this.MinimumVisualStudioVersion) >= 0
                   && CompareMaxVersions(frameworkFilter.MaximumVisualStudioVersion, this.MaximumVisualStudioVersion) <= 0
                   && frameworkFilter.PlatformArchitectures.All(x => this.PlatformArchitectures.Contains(x));
        }
    }
}
