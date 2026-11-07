namespace PandaBear.BuildingBlocks;

/// <summary>Calendar helpers for <see cref="TimeProvider"/>.</summary>
public static class TimeProviderExtensions
{
    /// <summary>Gets the current calendar day in the provider's local time zone.</summary>
    /// <param name="provider">The provider.</param>
    /// <returns>The local date.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="provider"/> is <see langword="null"/>.</exception>
    public static DateOnly Today(this TimeProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        return DateOnly.FromDateTime(provider.GetLocalNow().DateTime);
    }
}
