#if portable && (net40)
// from: portable net40
// to: net45
namespace System.Collections.Generic
{
    public interface IReadOnlyList<out T> :
        IEnumerable<T>
    {
        T this[int index] { get; }
        int Count { get; }
    }
}
#endif