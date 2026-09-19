namespace MediatrUnionPoc.Application.Common.Results;

/// <summary>Shared null-safe, distinct-collection coercion used by every case type that stores a collection.</summary>
internal static class EnumerableExtensions
{
    /// <summary>Deduplicates <paramref name="source"/> into a read-only collection; <see langword="null"/> becomes empty.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="source">The sequence to deduplicate. <see langword="null"/> is treated as empty.</param>
    /// <returns>A read-only, duplicate-free snapshot of <paramref name="source"/>.</returns>
    internal static IReadOnlyCollection<T> ToDistinctReadOnlyCollection<T>(
        this IEnumerable<T>? source
    ) => source is null ? [] : source.Distinct().ToArray();

    /// <summary>
    /// Set equality (order-independent) between two collections already known to be duplicate-free
    /// — i.e. ones produced by <see cref="ToDistinctReadOnlyCollection{T}"/>. Backs value equality
    /// for every case type whose record-generated equality would otherwise compare the collection
    /// field by reference.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="first">The first collection.</param>
    /// <param name="second">The second collection.</param>
    /// <returns><see langword="true"/> if both collections contain the same elements, regardless of order.</returns>
    internal static bool SetEqual<T>(
        this IReadOnlyCollection<T> first,
        IReadOnlyCollection<T> second
    ) => first.Count == second.Count && first.All(second.Contains);

    /// <summary>An order-independent hash code matching <see cref="SetEqual{T}"/> — equal sets always hash equal.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="source">The collection to hash.</param>
    /// <returns>A hash code unaffected by the order of <paramref name="source"/>'s elements.</returns>
    internal static int GetSetHashCode<T>(this IEnumerable<T> source)
    {
        var hash = 0;
        foreach (var item in source)
        {
            hash ^= item?.GetHashCode() ?? 0;
        }

        return hash;
    }
}
