namespace SharedKernel.Blazor.Site;

/// <summary>Checks that an enum parameter holds one of its declared values, so a cast such as <c>(HeroLayout)9</c> is refused.</summary>
public static class EnumParameter
{
    /// <summary>Refuses a value that is not declared by its enum.</summary>
    /// <typeparam name="TEnum">The enum type.</typeparam>
    /// <param name="value">The value.</param>
    /// <param name="parameterName">The parameter name reported on failure.</param>
    /// <param name="message">The message on failure, such as <c>The hero layout is not defined.</c></param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is not declared by <typeparamref name="TEnum"/>.</exception>
    public static void RequireDefined<TEnum>(TEnum value, string parameterName, string message)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(parameterName, value, message);
        }
    }

    /// <summary>Refuses a value that is not declared by its enum; <see langword="null"/> is accepted.</summary>
    /// <typeparam name="TEnum">The enum type.</typeparam>
    /// <param name="value">The value, or <see langword="null"/>.</param>
    /// <param name="parameterName">The parameter name reported on failure.</param>
    /// <param name="message">The message on failure.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is not declared by <typeparamref name="TEnum"/>.</exception>
    public static void RequireDefined<TEnum>(TEnum? value, string parameterName, string message)
        where TEnum : struct, Enum
    {
        if (value is { } defined)
        {
            RequireDefined(defined, parameterName, message);
        }
    }
}
