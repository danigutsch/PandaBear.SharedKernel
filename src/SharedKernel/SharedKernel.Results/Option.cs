namespace SharedKernel.Results;

/// <summary>
/// Creates <see cref="Option{T}"/> instances without putting static members on the generic type.
/// </summary>
public static class Option
{
    /// <summary>
    /// Creates an option containing a non-null value.
    /// </summary>
    /// <typeparam name="T">The wrapped non-null type.</typeparam>
    /// <param name="value">The value to wrap.</param>
    /// <returns>An option containing <paramref name="value"/>.</returns>
    public static Option<T> Some<T>(T value)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(value);

        return new Option<T>(value, true);
    }

    /// <summary>
    /// Creates an empty option.
    /// </summary>
    /// <typeparam name="T">The wrapped non-null type.</typeparam>
    /// <returns>An empty option.</returns>
    public static Option<T> None<T>()
        where T : notnull => new(default, false);

    /// <summary>
    /// Creates an option from a nullable reference.
    /// </summary>
    /// <typeparam name="T">The wrapped non-null type.</typeparam>
    /// <param name="value">The nullable value to wrap.</param>
    /// <returns>An empty option for <see langword="null"/>; otherwise an option containing the value.</returns>
    public static Option<T> FromNullable<T>(T? value)
        where T : notnull =>
        value is null ? None<T>() : Some(value);
}
