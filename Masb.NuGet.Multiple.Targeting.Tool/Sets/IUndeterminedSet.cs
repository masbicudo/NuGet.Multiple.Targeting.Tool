namespace Masb.NuGet.Multiple.Targeting.Tool.Sets
{
    /// <summary>
    /// Represents a set whose items cannot be determined.
    /// </summary>
    /// <typeparam name="T">Type of the items in the set.</typeparam>
    public interface IUndeterminedSet<T>
    {
        /// <summary>
        /// Determines whether an item is inside the set.
        /// </summary>
        /// <param name="item">Item to test.</param>
        /// <returns>Returns a boolean indicating whether the item is in the set.</returns>
        bool Contains(T item);

        /// <summary>
        /// Determines whether another set is a subset.
        /// </summary>
        /// <param name="set">The set to test.</param>
        /// <returns>
        /// Returns a optional boolean indicating whether the set is a subset for sure,
        /// or not, returning null when it is not possible to know.
        /// </returns>
        bool? Contains(IUndeterminedSet<T> set);

        /// <summary>
        /// Determines whether another set has an intersection with this set.
        /// </summary>
        /// <param name="set">The set to test.</param>
        /// <returns>
        /// Returns a optional boolean indicating whether the set intersects for sure,
        /// or not, returning null when it is not possible to know.
        /// </returns>
        bool? Intersects(IUndeterminedSet<T> set);

        bool? IsEmpty();
    }
}