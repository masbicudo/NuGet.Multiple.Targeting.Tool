using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Masb.NuGet.Multiple.Targeting.Tool.Sets
{
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

        public IEnumerable<IUndeterminedSet<T>> IntersectedSets { get; private set; }

        public bool Contains(T item)
        {
            return this.IntersectedSets.All(s => s.Contains(item));
        }

        public bool? Contains(IUndeterminedSet<T> otherSet)
        {
            if (otherSet.IsEmpty() == true)
                return true;

            var otherIntersect = otherSet as IntersectionSet<T>;
            if (otherIntersect != null)
            {
                var contains = this.IntersectedSets.All(
                    osf => otherIntersect.IntersectedSets.Any(
                        x => osf.Contains(x) == true));

                if (contains)
                    return true;

                var notContains = otherIntersect.isEmpty == false
                                  && this.IntersectedSets.Any(
                                      osf => otherIntersect.IntersectedSets.Any(
                                          x => osf.Intersects(x) == false));

                if (notContains)
                    return false;
            }

            return null;
        }

        public bool? Intersects(IUndeterminedSet<T> set)
        {
            if (this.isEmpty == true || set.IsEmpty() == true)
                return false;

            return null;
        }

        public bool? IsEmpty()
        {
            return this.isEmpty;
        }
    }
}