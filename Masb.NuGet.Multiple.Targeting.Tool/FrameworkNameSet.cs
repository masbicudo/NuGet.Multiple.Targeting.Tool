using System;
using System.Linq;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using JetBrains.Annotations;

namespace Masb.NuGet.Multiple.Targeting.Tool
{
    public class FrameworkNameSet : IUndeterminedSet<FrameworkName>
    {
        [NotNull]
        public FrameworkName FrameworkName { get; private set; }

        public FrameworkNameSet([NotNull] FrameworkName frameworkName)
        {
            if (frameworkName == null)
                throw new ArgumentNullException("frameworkName");

            this.FrameworkName = frameworkName;
        }

        public bool Contains([NotNull] FrameworkName item)
        {
            if (item == null)
                throw new ArgumentNullException("item");

            return Contains(this.FrameworkName, item);
        }

        public bool? Contains([NotNull] IUndeterminedSet<FrameworkName> set)
        {
            if (set == null)
                throw new ArgumentNullException("set");

            if (set.IsEmpty() == true)
                return true;

            var unitSet = set as FrameworkNameSet;
            if (unitSet != null)
                return this.Contains(unitSet.FrameworkName);

            var filter = set as FrameworkFilter;
            if (filter != null)
                return this.Contains(filter);

            var intersection = set as IntersectionSet<FrameworkName>;
            if (intersection != null)
                return this.Contains(intersection);

            return null;
        }

        public bool? Contains([NotNull] FrameworkFilter filter)
        {
            if (filter == null)
                throw new ArgumentNullException("filter");

            return false;
        }

        public bool? Contains([NotNull] IntersectionSet<FrameworkName> intersection)
        {
            if (intersection == null)
                throw new ArgumentNullException("intersection");

            var contains = intersection.IntersectedSets.Any(
                x => this.Contains(x) == true);

            if (contains)
                return true;

            var notContains = intersection.IsEmpty() == false
                              && intersection.IntersectedSets.Any(
                                  x => this.Intersects(x) == false);

            if (notContains)
                return false;

            return null;
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

        public bool Intersects([NotNull] FrameworkNameSet set)
        {
            if (set == null)
                throw new ArgumentNullException("set");

            if (this.Contains(set.FrameworkName) || set.Contains(this.FrameworkName))
                return true;

            return false;
        }

        public bool Intersects([NotNull] FrameworkFilter set)
        {
            if (set == null)
                throw new ArgumentNullException("set");

            return FrameworkFilter.Intersects(set, this.FrameworkName);
        }

        public bool? IsEmpty()
        {
            return false;
        }

        private static bool Contains(FrameworkName set, FrameworkName subset)
        {
            if (set.Equals(subset))
                return true;

            if (StringComparer.InvariantCultureIgnoreCase.Equals(set.Identifier, subset.Identifier))
                if (StringComparer.InvariantCultureIgnoreCase.Equals(set.Profile, subset.Profile))
                    if (set.Version.Major == subset.Version.Major)
                        if (set.Version >= subset.Version)
                            return true;

            return false;
        }

        public override string ToString()
        {
            return this.FrameworkName.ToString();
        }

        public static bool TryParse(string str, out FrameworkNameSet value)
        {
            if (Regex.IsMatch(str, @"^[^,]*,Version=v(?:\d+(?:\.\d+(?:\.\d+(?:\.\d+)?)?)?)(?:,Profile=.*)?$"))
            {
                value = new FrameworkNameSet(new FrameworkName(str));
                return true;
            }

            value = null;
            return false;
        }
    }
}