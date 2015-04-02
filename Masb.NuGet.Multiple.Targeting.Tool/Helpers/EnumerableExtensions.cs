using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Masb.NuGet.Multiple.Targeting.Tool.Helpers
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
        /// <typeparam name="T">Type of the result from the enumerated task.</typeparam>
        /// <typeparam name="TValue">Type of the result of the continuation function.</typeparam>
        /// <param name="enumerable">An enumeration containing tasks, that will be continued by the given function.</param>
        /// <param name="func">Function that will be used as continuation for each of the enumerated tasks.</param>
        /// <returns>A task enumeration, that contains each original task continued by the given delegate.</returns>
        public static IEnumerable<Task<TValue>> ThenSelect<T, TValue>(
            this IEnumerable<Task<T>> enumerable,
            Func<T, TValue> func)
        {
            return enumerable.Select(task => task.ContinueWith(t => func(t.Result)));
        }

        /// <summary>
        /// Runs a task upon completion of each enumerated task.
        /// </summary>
        /// <typeparam name="T">Type of the result from the enumerated task.</typeparam>
        /// <param name="enumerable">An enumeration containing tasks, that will be continued by the given action.</param>
        /// <param name="func">Action that will be used as continuation for each of the enumerated tasks.</param>
        /// <returns>A task enumeration, that contains each original task continued by the given delegate.</returns>
        public static IEnumerable<Task> ThenDo<T>(
            this IEnumerable<Task<T>> enumerable,
            Action<T> func)
        {
            return enumerable.Select(task => task.ContinueWith(t => func(t.Result)));
        }

        public static IEnumerable<T[]> Transpose<T>(this IEnumerable<IEnumerable<T>> enumerable)
        {
            var enumerators = enumerable
                .Select(x => x.GetEnumerator())
                .ToArray();

            while (enumerators.All(x => x.MoveNext()))
                yield return enumerators.Select(x => x.Current).ToArray();
        }
    }
}