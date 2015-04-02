using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Masb.NuGet.Multiple.Targeting.Tool.Sets
{
    /// <summary>
    /// Represents a set created by the intersection of multiple other sets.
    /// </summary>
    /// <typeparam name="T">The type of the elements of the set.</typeparam>
    public class IntersectionSet<T> : IUndeterminedSet<T>
    {
        private readonly bool? isEmpty;

        public IntersectionSet(IEnumerable<IUndeterminedSet<T>> setsToIntersect)
        {
            var sets = this.IntersectedSets = setsToIntersect.ToImmutableArray();
            var allEmpty = sets.All(x => x.IsEmpty() == true) || !sets.Any();
            this.isEmpty = allEmpty;
        }

        public IntersectionSet(IEnumerable<IUndeterminedSet<T>> setsToIntersect, bool? isEmptyHint)
        {
            var sets = this.IntersectedSets = setsToIntersect.ToImmutableArray();
            var allEmpty = sets.All(x => x.IsEmpty() == true) || !sets.Any();
            this.isEmpty = allEmpty ? true : isEmptyHint;
        }

        /// <summary>
        /// Gets the intersected sets, that form this intersection.
        /// </summary>
        public IEnumerable<IUndeterminedSet<T>> IntersectedSets { get; private set; }

        public bool Contains(T item)
        {
            return this.IntersectedSets.All(s => s.Contains(item));
        }

        /// <summary>
        /// Determines whether this set contains another set.
        /// </summary>
        /// <param name="otherSet">The other set to test.</param>
        /// <returns>
        /// Returns a optional boolean indicating whether the set is a subset or not for sure,
        /// returning null when it is not possible to tell.
        /// </returns>
        public bool? Contains(IUndeterminedSet<T> otherSet)
        {
            if (otherSet.IsEmpty() == true)
                return true;

            var otherIntersect = otherSet as IntersectionSet<T>;
            if (otherIntersect != null)
            {
                // CONTAINS FOR SURE
                // -----------------------------
                // all intersectors of this set
                // contain at least one of the
                // intersectors of the other set
                var contains = this.IntersectedSets.All(
                    osf => otherIntersect.IntersectedSets.Any(
                        x => osf.Contains(x) == true));

                if (contains)
                    return true;

                // DOES NOT CONTAIN FOR SURE
                // -----------------------------
                // the other set is not empty
                //  == and ==
                // any intersector of this set
                // does not intersect with any
                // intersector of the other set
                var notContains = otherIntersect.isEmpty == false
                                  && this.IntersectedSets.Any(
                                      osf => otherIntersect.IntersectedSets.Any(
                                          x => osf.Intersects(x) == false));

                if (notContains)
                    return false;
            }

            return null;
        }

        /// <summary>
        /// Determines whether another set intersects with this set.
        /// </summary>
        /// <param name="set">The other set to test.</param>
        /// <returns>
        /// Returns an optional boolean indicating whether the set intersects or not for sure,
        /// returning null when it is not possible to tell.
        /// </returns>
        public bool? Intersects(IUndeterminedSet<T> set)
        {
            // nothing intersects with an empty set
            if (this.isEmpty == true || set.IsEmpty() == true)
                return false;

            return null;
        }

        /// <summary>
        /// Returns a value indicating whether this set is empty or not.
        /// </summary>
        /// <returns>
        /// True if the set is empty for sure,
        ///  false when it contains elements,
        ///  or null when it is not possible to tell.
        /// </returns>
        public bool? IsEmpty()
        {
            return this.isEmpty;
        }
    }
}