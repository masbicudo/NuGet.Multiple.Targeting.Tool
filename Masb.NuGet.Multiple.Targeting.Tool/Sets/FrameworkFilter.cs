using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using Masb.NuGet.Multiple.Targeting.Tool.InfoModel;

namespace Masb.NuGet.Multiple.Targeting.Tool.Sets
{
    /// <summary>
    /// Represents a set of <see cref="FrameworkName"/>, by indicating the characteristics of such objects.
    /// It turns out, that this set is the result of filtering another greater virtual set of frameworks,
    /// by matching their characteristics.
    /// </summary>
    public class FrameworkFilter :
        IUndeterminedSet<FrameworkName>
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

        /// <summary>
        /// Gets the display name of accepted <see cref="FrameworkName"/>s.
        /// </summary>
        public string DisplayName { get; private set; }

        /// <summary>
        /// Gets the family of an accepted <see cref="FrameworkName"/>s.
        /// </summary>
        public string Family { get; private set; }

        /// <summary>
        /// Gets the identifier of an accepted <see cref="FrameworkName"/>s.
        /// </summary>
        public string Identifier { get; private set; }

        /// <summary>
        /// Gets the minimum version of an accepted <see cref="FrameworkName"/>s.
        /// </summary>
        public Version MinimumVersion { get; private set; }

        /// <summary>
        /// Gets the minimum version display name of an accepted <see cref="FrameworkName"/>s.
        /// </summary>
        public string MinimumVersionDisplayName { get; private set; }

        /// <summary>
        /// Gets the minimum Visual Studio version that support an accepted <see cref="FrameworkName"/>s.
        /// This means that for a framework to be in considered in the set, it must be supported by a Visual Studio version
        /// that is greater or equal to the indicated one.
        /// </summary>
        public Version MinimumVisualStudioVersion { get; private set; }

        /// <summary>
        /// Gets the platform characteristics of accepted <see cref="FrameworkName"/>s.
        /// </summary>
        public PlatformFilter Platform { get; private set; }

        /// <summary>
        /// Gets the platform architectures of accepted <see cref="FrameworkName"/>s.
        /// </summary>
        public IEnumerable<string> PlatformArchitectures { get; private set; }

        /// <summary>
        /// Gets the profile name of accepted <see cref="FrameworkName"/>s.
        /// </summary>
        public string Profile { get; private set; }

        /// <summary>
        /// Gets the maximum Visual Studio version that support an accepted <see cref="FrameworkName"/>s.
        /// This means that for a framework to be in considered in the set, it must be supported by a Visual Studio version
        /// that is lesser or equal to the indicated one.
        /// </summary>
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

            /// <summary>
            /// Gets the platform identifier of accepted <see cref="FrameworkName"/>s.
            /// </summary>
            public string Identifier { get; private set; }

            /// <summary>
            /// Gets the platform minimum version of accepted <see cref="FrameworkName"/>s.
            /// </summary>
            public Version MinimumVersion { get; private set; }

            private string FullName
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

            /// <summary>
            /// Gets a complete string representation of this platform set.
            /// </summary>
            /// <returns>
            /// The <see cref="string"/> representing the platform set.
            /// </returns>
            public override string ToString()
            {
                return this.FullName;
            }
        }

        private string FullName
        {
            get
            {
                if (this.fullName == null)
                {
                    var stringBuilder = new StringBuilder();
                    stringBuilder.AppendFormat("{0},Version=v{1}+", this.Identifier, this.MinimumVersion);

                    if (!string.IsNullOrWhiteSpace(this.Profile))
                        stringBuilder.AppendFormat(",Profile={0}", this.Profile);

                    if (!string.IsNullOrWhiteSpace(this.DisplayName))
                        stringBuilder.AppendFormat(",DisplayName={0}", this.DisplayName);

                    if (!string.IsNullOrWhiteSpace(this.Family))
                        stringBuilder.AppendFormat(",Family={0}", this.Family);

                    if (!string.IsNullOrWhiteSpace(this.MinimumVersionDisplayName))
                        stringBuilder.AppendFormat(",MinimumVersionDisplayName={0}", this.MinimumVersionDisplayName);

                    if (this.MaximumVisualStudioVersion != null)
                        stringBuilder.AppendFormat(",MaximumVisualStudioVersion={0}", this.MaximumVisualStudioVersion);

                    if (this.MinimumVisualStudioVersion != null)
                        stringBuilder.AppendFormat(",MinimumVisualStudioVersion={0}", this.MinimumVisualStudioVersion);

                    if (this.PlatformArchitectures != null)
                        stringBuilder.AppendFormat(",PlatformArchitectures={0}", string.Join("|", this.PlatformArchitectures));

                    if (this.Platform != null)
                    {
                        if (!string.IsNullOrWhiteSpace(this.Platform.Identifier))
                            stringBuilder.AppendFormat(",PlatformIdentifier={0}", this.Platform.Identifier);
                        if (this.Platform.MinimumVersion != null)
                            stringBuilder.AppendFormat(",PlatformMinimumVersion={0}", this.Platform.MinimumVersion);
                    }

                    this.fullName = stringBuilder.ToString();
                }

                return this.fullName;
            }
        }

        private static bool IsStrMatch(string input, string pattern)
        {
            return Regex.IsMatch(input, @"^" + pattern.Replace(".", @"\.").Replace("*", @".*") + @"$", RegexOptions.IgnoreCase);
        }

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

        /// <summary>
        /// Gets a complete string representation of this framework set.
        /// </summary>
        /// <returns>
        /// The <see cref="string"/> representing the <see cref="FrameworkName"/> set.
        /// </returns>
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

        /// <summary>
        /// Determines whether another set intersects with this set.
        /// </summary>
        /// <param name="set">The other set to test.</param>
        /// <returns>
        /// Returns a optional boolean indicating whether the set intersects or not for sure,
        /// returning null when it is not possible to tell.
        /// </returns>
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

        /// <summary>
        /// Indicates whether this set is an empty set.
        /// In the case of a <see cref="FrameworkFilter"/>, it always returns false.
        /// </summary>
        /// <returns>Returns false to indicate that this set is not an empty set.</returns>
        public bool? IsEmpty()
        {
            return false;
        }

        /// <summary>
        /// Determines whether a <see cref="FrameworkNameSet"/> intersects with this set.
        /// </summary>
        /// <param name="frmkNameSet">The <see cref="FrameworkNameSet"/> to test.</param>
        /// <returns>True if this set intersects the other given set.</returns>
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

        /// <summary>
        /// Determines whether another <see cref="FrameworkFilter"/> intersects with this set.
        /// </summary>
        /// <param name="filter">The other <see cref="FrameworkFilter"/> to test.</param>
        /// <returns>True if this set intersects the other given set.</returns>
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
        ///  or not, returning null when it is not possible to tell.
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

        /// <summary>
        /// Determines whether another <see cref="FrameworkFilter"/> is a subset.
        /// </summary>
        /// <param name="frameworkFilter">The other <see cref="FrameworkFilter"/> to test.</param>
        /// <returns>True if this set contains the other given set.</returns>
        public bool Contains(FrameworkFilter frameworkFilter)
        {
            // ReSharper disable once InconsistentNaming
            const string Letters_a_to_z = "abcdefghijklmnopqrstuvwxyz";

            return IsStrMatch(frameworkFilter.Identifier.Replace("*", Letters_a_to_z), this.Identifier)
                   && IsStrMatch(frameworkFilter.Identifier.Replace("*", ""), this.Identifier)
                   && IsStrMatch(frameworkFilter.Profile.Replace("*", Letters_a_to_z), this.Profile)
                   && IsStrMatch(frameworkFilter.Profile.Replace("*", ""), this.Profile)
                   && CompareMinVersions(frameworkFilter.MinimumVersion, this.MinimumVersion) >= 0
                   && CompareMinVersions(frameworkFilter.MinimumVisualStudioVersion, this.MinimumVisualStudioVersion) >= 0
                   && CompareMaxVersions(frameworkFilter.MaximumVisualStudioVersion, this.MaximumVisualStudioVersion) <= 0
                   && frameworkFilter.PlatformArchitectures.All(x => this.PlatformArchitectures.Contains(x));
        }

        private static string ReadVer(Dictionary<string, string> dic, string key)
        {
            string str;
            if (dic.TryGetValue(key, out str))
                if (Regex.IsMatch(str, @"^\d+(?:\.\d+(?:\.\d+(?:\.\d+)?)?)?$"))
                    return str.Substring(1);

            return null;
        }

        private static string ReadStr(Dictionary<string, string> dic, string key)
        {
            string value;
            if (dic.TryGetValue(key, out value))
                return value;

            return null;
        }

        private static string ReadList(Dictionary<string, string> dic, string key)
        {
            string value;
            if (dic.TryGetValue(key, out value))
                if (value != null)
                    return value.Replace("|", ",");

            return null;
        }

        private static readonly char[] separatorComma = { ',' };
        private static readonly char[] separatorEqualSign = { '=' };

        /// <summary>
        /// Tries parsing a <see cref="FrameworkFilter"/> string representation.
        /// </summary>
        /// <param name="str">The string to try parsing.</param>
        /// <param name="value">Output parameter that receives the resulting <see cref="FrameworkFilter"/> if the string is valid, or null otherwise.</param>
        /// <returns>True if the string represents a valid <see cref="FrameworkFilter"/>. Otherwise false.</returns>
        public static bool TryParse(string str, out FrameworkFilter value)
        {
            if (!string.IsNullOrWhiteSpace(str))
            {
                var parts = str.Split(separatorComma, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0)
                {
                    var dic = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
                    foreach (var part in parts.Skip(1))
                    {
                        var eq = part.Split(separatorEqualSign, 2, StringSplitOptions.RemoveEmptyEntries);
                        if (eq.Length == 2)
                            dic[eq[0].Trim()] = eq[1].Trim();
                    }

                    value = new FrameworkFilter(new FrameworkInfo.SupportedFrameworkItem
                    {
                        Identifier = parts[0],
                        MinimumVersion = ReadVer(dic, "MinimumVersion"),
                        DisplayName = ReadStr(dic, "DisplayName"),
                        Family = ReadStr(dic, "Family"),
                        MaximumVisualStudioVersion = ReadStr(dic, "MaximumVisualStudioVersion"),
                        MinimumVersionDisplayName = ReadStr(dic, "MinimumVersionDisplayName"),
                        MinimumVisualStudioVersion = ReadStr(dic, "MinimumVisualStudioVersion"),
                        Profile = ReadStr(dic, "Profile"),
                        PlatformArchitectures = ReadList(dic, "PlatformArchitectures"),
                        Platform = new FrameworkInfo.SupportedFrameworkItem.PlatformItem
                        {
                            Identifier = ReadStr(dic, "PlatformIdentifier"),
                            MinimumVersion = ReadStr(dic, "PlatformMinimumVersion"),
                        }
                    });

                    return true;
                }
            }

            value = null;
            return false;
        }
    }
}
