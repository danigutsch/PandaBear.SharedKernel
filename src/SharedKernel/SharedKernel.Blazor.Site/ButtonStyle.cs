namespace PandaBear.Blazor.Site;

/// <summary>The classes of the button look, shared by <see cref="ButtonLink" /> and the more look of a <see cref="Card" />, so both are identical.</summary>
internal static class ButtonStyle
{
    /// <summary>Gets the classes of a button look.</summary>
    /// <param name="variant">The variant; an undefined value is the primary look.</param>
    /// <param name="size">The size.</param>
    /// <returns>The class list, such as <c>site-button site-button--outline site-button--compact</c>.</returns>
    public static string Classes(ButtonVariant variant, ButtonSize size)
    {
        var look = variant switch
        {
            ButtonVariant.Secondary => "site-button site-button--secondary",
            ButtonVariant.Outline => "site-button site-button--outline",
            _ => "site-button site-button--primary",
        };
        return size == ButtonSize.Compact ? look + " site-button--compact" : look;
    }

    /// <summary>Checks a size.</summary>
    /// <param name="size">The size.</param>
    /// <param name="parameterName">The parameter name reported on failure.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="size" /> is not defined.</exception>
    public static void RequireSize(ButtonSize size, string parameterName)
    {
        if (!Enum.IsDefined(size))
        {
            throw new ArgumentOutOfRangeException(parameterName, size, "The button size is not defined.");
        }
    }
}
