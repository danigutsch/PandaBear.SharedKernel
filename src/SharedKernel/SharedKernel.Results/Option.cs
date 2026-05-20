using System.Diagnostics.CodeAnalysis;

namespace SharedKernel.Results;

/// <summary>
/// Creates <see cref="Option{T}"/> instances without putting static members on the generic type.
/// </summary>
public static class Option
{
    /// <summary>
    /// Creates an option containing a non-null value.
    /// </summary>
    /// <typeparam name="T">The wrapped reference type.</typeparam>
    /// <param name="value">The value to wrap.</param>
    /// <returns>An option containing <paramref name="value"/>.</returns>
    public static Option<T> Some<T>(T value)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(value);

        return new Option<T>(value, true);
    }

    /// <summary>
    /// Creates an empty option.
    /// </summary>
    /// <typeparam name="T">The wrapped reference type.</typeparam>
    /// <returns>An empty option.</returns>
    public static Option<T> None<T>()
        where T : class => new(null, false);

    /// <summary>
    /// Creates an option from a nullable reference.
    /// </summary>
    /// <typeparam name="T">The wrapped reference type.</typeparam>
    /// <param name="value">The nullable value to wrap.</param>
    /// <returns>An empty option for <see langword="null"/>; otherwise an option containing the value.</returns>
    public static Option<T> FromNullable<T>(T? value)
        where T : class =>
        value is null ? None<T>() : Some(value);
}

/// <summary>
/// Represents an optional reference value that may or may not be present.
/// </summary>
/// <typeparam name="T">The wrapped reference type.</typeparam>
public readonly struct Option<T> : IEquatable<Option<T>>
    where T : class
{
    private readonly T? value;

    internal Option(T? value, bool hasValue)
    {
        this.value = value;
        HasValue = hasValue;
    }

    /// <summary>
    /// Gets a value indicating whether the option contains a value.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Value))]
    [MemberNotNullWhen(true, nameof(value))]
    public bool HasValue { get; }

    /// <summary>
    /// Gets a value indicating whether the option is empty.
    /// </summary>
    public bool IsEmpty => !HasValue;

    /// <summary>
    /// Gets the wrapped value when present; otherwise <see langword="null"/>.
    /// </summary>
    public T? Value => value;

    /// <summary>
    /// Returns the current value when present.
    /// </summary>
    /// <param name="currentValue">The current value when present; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the option contains a value.</returns>
    public bool TryGetValue([NotNullWhen(true)] out T? currentValue)
    {
        currentValue = value;
        return HasValue;
    }

    /// <summary>
    /// Projects the option into one of two result values.
    /// </summary>
    /// <typeparam name="TResult">The result type produced by the match.</typeparam>
    /// <param name="whenSome">Called when a value is present.</param>
    /// <param name="whenNone">Called when the option is empty.</param>
    /// <returns>The value produced by the matched branch.</returns>
    public TResult Match<TResult>(Func<T, TResult> whenSome, Func<TResult> whenNone)
    {
        ArgumentNullException.ThrowIfNull(whenSome);
        ArgumentNullException.ThrowIfNull(whenNone);

        return TryGetValue(out var currentValue)
            ? whenSome(currentValue)
            : whenNone();
    }

    /// <inheritdoc />
    public bool Equals(Option<T> other) =>
        HasValue == other.HasValue && EqualityComparer<T?>.Default.Equals(value, other.value);

    /// <inheritdoc />
    public override bool Equals(object? obj) =>
        obj is Option<T> other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(HasValue, value);

    /// <summary>
    /// Determines whether two option instances are equal.
    /// </summary>
    public static bool operator ==(Option<T> left, Option<T> right) => left.Equals(right);

    /// <summary>
    /// Determines whether two option instances are not equal.
    /// </summary>
    public static bool operator !=(Option<T> left, Option<T> right) => !left.Equals(right);
}
