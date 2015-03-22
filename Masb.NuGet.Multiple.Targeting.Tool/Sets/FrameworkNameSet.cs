using System;
using System.Linq;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using JetBrains.Annotations;

namespace Masb.NuGet.Multiple.Targeting.Tool.Sets
{
    /// <summary>
    /// Represents a set of <see cref="FrameworkName"/>s, by natural pertinence relation.
    /// For example, a <see cref="FrameworkName"/> with v4.0 is a superset of v4.5.
    /// This class is an <see cref="IUndeterminedSet{T}"/> that is capable of testing this relation.
    /// </summary>
    public class FrameworkNameSet :
        IUndeterminedSet<FrameworkName>
    {
        /// <summary>
        /// Gets the <see cref="FrameworkName"/> that this set is subordinated to.
        /// </summary>
        [NotNull]
        public FrameworkName FrameworkName { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="FrameworkNameSet"/> class,
        /// given the <see cref="FrameworkName"/> that this set will be subordinated to.
        /// </summary>
        /// <param name="frameworkName">
        /// The <see cref="FrameworkName"/> that this set will be subordinated to.
        /// </param>
        /// <exception cref="ArgumentNullException">When <paramref name="frameworkName"/> is null. </exception>
        public FrameworkNameSet([NotNull] FrameworkName frameworkName)
        {
            if (frameworkName == null)
                throw new ArgumentNullException("frameworkName");

            this.FrameworkName = frameworkName;
        }

        /// <summary>
        /// Determines whether a specific <see cref="FrameworkName"/> is in this set.
        /// </summary>
        /// <param name="item">The <see cref="FrameworkName"/> to test.</param>
        /// <returns>True if this set contains the given <see cref="FrameworkName"/>.</returns>
        public bool Contains([NotNull] FrameworkName item)
        {
            if (item == null)
                throw new ArgumentNullException("item");

            return Contains(this.FrameworkName, item);
        }

        /// <summary>
        /// Determines whether another set is a subset.
        /// </summary>
        /// <param name="set">The set to test.</param>
        /// <returns>
        /// Returns a optional boolean indicating whether the set is a subset for sure,
        ///  or not, returning null when it is not possible to tell.
        /// </returns>
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
                return false;

            var intersection = set as IntersectionSet<FrameworkName>;
            if (intersection != null)
                return this.Contains(intersection);

            return null;
        }

        /// <summary>
        /// Determines whether another set (created by intersection) is a subset or not.
        /// </summary>
        /// <param name="intersection">The intersection set to test.</param>
        /// <returns>
        /// Returns a optional boolean indicating whether the intersection is a subset for sure,
        ///  or not, returning null when it is not possible to tell.
        /// </returns>
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
        /// Determines whether a <see cref="FrameworkNameSet"/> intersects with this set.
        /// </summary>
        /// <param name="set">The <see cref="FrameworkNameSet"/> to test.</param>
        /// <returns>True if this set intersects the other given set.</returns>
        public bool Intersects([NotNull] FrameworkNameSet set)
        {
            if (set == null)
                throw new ArgumentNullException("set");

            if (this.Contains(set.FrameworkName) || set.Contains(this.FrameworkName))
                return true;

            return false;
        }

        /// <summary>
        /// Determines whether a <see cref="FrameworkFilter"/> intersects with this set.
        /// </summary>
        /// <param name="set">The <see cref="FrameworkFilter"/> to test.</param>
        /// <returns>True if this set intersects the other given set.</returns>
        public bool Intersects([NotNull] FrameworkFilter set)
        {
            if (set == null)
                throw new ArgumentNullException("set");

            return FrameworkFilter.Intersects(set, this.FrameworkName);
        }

        /// <summary>
        /// Indicates whether this set is an empty set.
        /// In the case of a <see cref="FrameworkNameSet"/>, it always returns false.
        /// </summary>
        /// <returns>Returns false to indicate that this set is not an empty set.</returns>
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

        /// <summary>
        /// Returns a string representation of this set.
        /// </summary>
        /// <returns>A string representing the set.</returns>
        public override string ToString()
        {
            return this.FrameworkName.ToString();
        }

        /// <summary>
        /// Tries creating a <see cref="FrameworkNameSet"/> from it's string representation.
        /// </summary>
        /// <param name="str">The string to try parsing.</param>
        /// <param name="value">Output parameter that receives the resulting <see cref="FrameworkNameSet"/> if the string is valid, or null otherwise.</param>
        /// <returns>True if the string represents a valid <see cref="FrameworkNameSet"/>. Otherwise false.</returns>
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