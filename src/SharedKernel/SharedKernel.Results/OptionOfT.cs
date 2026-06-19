using System.Diagnostics.CodeAnalysis;

namespace SharedKernel.Results;

/// <summary>
/// Represents an optional non-null value that may or may not be present.
/// </summary>
/// <typeparam name="T">The wrapped non-null type.</typeparam>
[SuppressMessage("Design", "CA1000:Do not declare static members on generic types",
    Justification = "Compatibility factories preserve the established Option<T> call shape during migration to SharedKernel.Functional.")]
public readonly struct Option<T> : IEquatable<Option<T>>
    where T : notnull
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
    /// Maps the current option value into a new option value.
    /// </summary>
    /// <typeparam name="TResult">The projected non-null type.</typeparam>
    /// <param name="map">The projection to apply when a value is present.</param>
    /// <returns>The projected option, or an empty option when no value is present.</returns>
    public Option<TResult> Map<TResult>(Func<T, TResult> map)
        where TResult : notnull
    {
        ArgumentNullException.ThrowIfNull(map);

        return TryGetValue(out var currentValue)
            ? Option.Some(map(currentValue))
            : Option.None<TResult>();
    }

    /// <summary>
    /// Maps the current option value into a new option value using an asynchronous projection.
    /// </summary>
    /// <typeparam name="TResult">The projected non-null type.</typeparam>
    /// <param name="map">The asynchronous projection to apply when a value is present.</param>
    /// <returns>The projected option, or an empty option when no value is present.</returns>
    public async Task<Option<TResult>> Map<TResult>(Func<T, Task<TResult>> map)
        where TResult : notnull
    {
        ArgumentNullException.ThrowIfNull(map);

        if (TryGetValue(out var currentValue))
        {
            return Option.Some(await map(currentValue).ConfigureAwait(false));
        }

        return Option.None<TResult>();
    }

    /// <summary>
    /// Maps the current option value into a new option value using an asynchronous projection.
    /// </summary>
    /// <typeparam name="TResult">The projected non-null type.</typeparam>
    /// <param name="map">The asynchronous projection to apply when a value is present.</param>
    /// <returns>The projected option, or an empty option when no value is present.</returns>
    public async ValueTask<Option<TResult>> Map<TResult>(Func<T, ValueTask<TResult>> map)
        where TResult : notnull
    {
        ArgumentNullException.ThrowIfNull(map);

        if (TryGetValue(out var currentValue))
        {
            return Option.Some(await map(currentValue).ConfigureAwait(false));
        }

        return Option.None<TResult>();
    }

    /// <summary>
    /// Binds the current option value into another option.
    /// </summary>
    /// <typeparam name="TResult">The projected non-null type.</typeparam>
    /// <param name="bind">The projection to apply when a value is present.</param>
    /// <returns>The bound option, or an empty option when no value is present.</returns>
    public Option<TResult> Bind<TResult>(Func<T, Option<TResult>> bind)
        where TResult : notnull
    {
        ArgumentNullException.ThrowIfNull(bind);

        return TryGetValue(out var currentValue)
            ? bind(currentValue)
            : Option.None<TResult>();
    }

    /// <summary>
    /// Binds the current option value into another option using an asynchronous projection.
    /// </summary>
    /// <typeparam name="TResult">The projected non-null type.</typeparam>
    /// <param name="bind">The asynchronous projection to apply when a value is present.</param>
    /// <returns>The bound option, or an empty option when no value is present.</returns>
    public async Task<Option<TResult>> Bind<TResult>(Func<T, Task<Option<TResult>>> bind)
        where TResult : notnull
    {
        ArgumentNullException.ThrowIfNull(bind);

        if (TryGetValue(out var currentValue))
        {
            return await bind(currentValue).ConfigureAwait(false);
        }

        return Option.None<TResult>();
    }

    /// <summary>
    /// Binds the current option value into another option using an asynchronous projection.
    /// </summary>
    /// <typeparam name="TResult">The projected non-null type.</typeparam>
    /// <param name="bind">The asynchronous projection to apply when a value is present.</param>
    /// <returns>The bound option, or an empty option when no value is present.</returns>
    public async ValueTask<Option<TResult>> Bind<TResult>(Func<T, ValueTask<Option<TResult>>> bind)
        where TResult : notnull
    {
        ArgumentNullException.ThrowIfNull(bind);

        if (TryGetValue(out var currentValue))
        {
            return await bind(currentValue).ConfigureAwait(false);
        }

        return Option.None<TResult>();
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

    /// <summary>
    /// Projects the option into one of two result values using asynchronous delegates.
    /// </summary>
    /// <typeparam name="TResult">The result type produced by the match.</typeparam>
    /// <param name="whenSome">Called when a value is present.</param>
    /// <param name="whenNone">Called when the option is empty.</param>
    /// <returns>The value produced by the matched branch.</returns>
    public async Task<TResult> Match<TResult>(Func<T, Task<TResult>> whenSome, Func<Task<TResult>> whenNone)
    {
        ArgumentNullException.ThrowIfNull(whenSome);
        ArgumentNullException.ThrowIfNull(whenNone);

        return TryGetValue(out var currentValue)
            ? await whenSome(currentValue).ConfigureAwait(false)
            : await whenNone().ConfigureAwait(false);
    }

    /// <summary>
    /// Projects the option into one of two result values using asynchronous delegates.
    /// </summary>
    /// <typeparam name="TResult">The result type produced by the match.</typeparam>
    /// <param name="whenSome">Called when a value is present.</param>
    /// <param name="whenNone">Called when the option is empty.</param>
    /// <returns>The value produced by the matched branch.</returns>
    public async ValueTask<TResult> Match<TResult>(Func<T, ValueTask<TResult>> whenSome, Func<ValueTask<TResult>> whenNone)
    {
        ArgumentNullException.ThrowIfNull(whenSome);
        ArgumentNullException.ThrowIfNull(whenNone);

        return TryGetValue(out var currentValue)
            ? await whenSome(currentValue).ConfigureAwait(false)
            : await whenNone().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public bool Equals(Option<T> other) =>
        HasValue == other.HasValue && EqualityComparer<T?>.Default.Equals(value, other.value);

    /// <inheritdoc />
    public override bool Equals(object? obj) =>
        obj is Option<T> other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(HasValue, value);

    /// <inheritdoc />
    public override string ToString() =>
        TryGetValue(out var currentValue)
            ? $"Some({currentValue})"
            : "None";

    /// <summary>
    /// Determines whether two option instances are equal.
    /// </summary>
    public static bool operator ==(Option<T> left, Option<T> right) => left.Equals(right);

    /// <summary>
    /// Determines whether two option instances are not equal.
    /// </summary>
    public static bool operator !=(Option<T> left, Option<T> right) => !left.Equals(right);
}
