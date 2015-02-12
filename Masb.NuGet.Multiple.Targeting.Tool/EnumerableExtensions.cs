using System;
using System.Collections.Generic;
using System.Linq;

namespace Masb.NuGet.Multiple.Targeting.Tool
{
    public static class EnumerableExtensions
    {
        public static IEnumerable<T> Distinct<T, TValue>(this IEnumerable<T> enumerable, Func<T, TValue> func)
        {
            return enumerable
                .GroupBy(func)
                .Select(x => x.Last());
        }

        public static IEnumerable<T> Distinct<T, TValue>(this IEnumerable<T> enumerable, Func<T, TValue> func, Func<IEnumerable<T>, T> aggregator)
        {
            return enumerable
                .GroupBy(func)
                .Select(aggregator);
        }
    }
}