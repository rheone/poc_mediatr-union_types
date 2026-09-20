namespace MediatrUnionPoc.Application.Common;

/// <summary>
/// A value that was either supplied or not â€” the distinction a partial update (JSON Merge Patch)
/// needs and <see langword="null"/> cannot carry, because "not supplied" and "supplied as null" mean
/// different things. <c>default(Optional&lt;T&gt;)</c> is absent, which is also what a serializer
/// leaves behind for a member missing from the request body. Serializer-free on purpose: the
/// JSON binding lives in the Api project.
/// </summary>
/// <typeparam name="T">The type of the value when present. Use a nullable type (<c>string?</c>, <c>decimal?</c>) to let a present value be <see langword="null"/>.</typeparam>
public readonly struct Optional<T>
{
    private readonly T _value;

    private Optional(T value)
    {
        _value = value;
        IsPresent = true;
    }

    /// <summary>Gets a value indicating whether a value was supplied (even if that value is <see langword="null"/>).</summary>
    public bool IsPresent { get; }

    /// <summary>Gets the supplied value.</summary>
    /// <exception cref="InvalidOperationException">The optional is absent; check <see cref="IsPresent"/> first.</exception>
    public T Value =>
        IsPresent
            ? _value
            : throw new InvalidOperationException("The optional is absent and has no value.");

    /// <summary>Gets the supplied value, or <see langword="default"/> when absent — for callers that treat "not supplied" and "supplied as <see langword="null"/>" alike.</summary>
    /// <returns>The value when present; otherwise <see langword="default"/>.</returns>
    public T? GetValueOrDefault() => _value;

    /// <summary>Wraps a supplied value — including <see langword="null"/> when <typeparamref name="T"/> allows it — as present.</summary>
    /// <param name="value">The supplied value.</param>
    /// <returns>A present optional holding <paramref name="value"/>.</returns>
    public static Optional<T> Of(T value) => new(value);
}
