using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Masb.NuGet.Multiple.Targeting.Tool
{
    public class IntersectionSet<T> : IUndeterminedSet<T>
    {
        public IntersectionSet(IEnumerable<IUndeterminedSet<T>> setsToIntersect)
        {
            this.IntersectedSets = setsToIntersect.ToImmutableArray();
        }

        public IEnumerable<IUndeterminedSet<T>> IntersectedSets { get; private set; }

        public bool Contains(T item)
        {
            return this.IntersectedSets.All(s => s.Contains(item));
        }

        public bool? Contains(IUndeterminedSet<T> otherSet)
        {
            var otherIntersect = otherSet as IntersectionSet<T>;
            if (otherIntersect != null)
            {
                var contains = this.IntersectedSets
                    .All(osf => otherIntersect.IntersectedSets.Any(x => osf.Contains(x) == true));

                if (contains)
                    return true;

                var notContains = this.IntersectedSets
                    .Any(osf => otherIntersect.IntersectedSets.All(x => osf.Contains(x) == false));

                if (notContains)
                    return false;
            }

            return null;
        }
    }
}