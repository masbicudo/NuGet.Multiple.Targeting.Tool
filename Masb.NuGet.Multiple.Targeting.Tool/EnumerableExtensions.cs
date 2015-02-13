using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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

        /// <summary>
        /// Selects a value from the result of a task upon it's completion.
        /// The return will also be an enumeration of tasks of the new selected type.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="enumerable"></param>
        /// <param name="func"></param>
        /// <returns></returns>
        public static IEnumerable<Task<TValue>> ThenSelect<T, TValue>(
            this IEnumerable<Task<T>> enumerable,
            Func<T, TValue> func)
        {
            return enumerable.Select(task => task.ContinueWith(t => func(t.Result)));
        }
    }
}