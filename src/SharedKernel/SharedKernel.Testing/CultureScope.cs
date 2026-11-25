using System.Globalization;

namespace SharedKernel.Testing;

/// <summary>Temporarily sets the current culture and UI culture of the running thread and restores both on disposal.</summary>
/// <remarks>
/// The cultures flow with the asynchronous context, so code awaited inside the scope sees them too. Dispose nested scopes in
/// reverse order.
/// </remarks>
public sealed class CultureScope : IDisposable
{
    private readonly CultureInfo _previousCulture;
    private readonly CultureInfo _previousUiCulture;

    /// <summary>Sets both the current culture and the UI culture to <paramref name="culture"/>.</summary>
    /// <param name="culture">The culture to apply until disposal.</param>
    /// <exception cref="ArgumentNullException"><paramref name="culture"/> is <see langword="null"/>.</exception>
    public CultureScope(CultureInfo culture)
        : this(culture, culture)
    {
    }

    /// <summary>Sets the current culture to <paramref name="culture"/> and the UI culture to <paramref name="uiCulture"/>.</summary>
    /// <param name="culture">The culture for formatting until disposal.</param>
    /// <param name="uiCulture">The culture for resources until disposal.</param>
    /// <exception cref="ArgumentNullException"><paramref name="culture"/> or <paramref name="uiCulture"/> is <see langword="null"/>.</exception>
    public CultureScope(CultureInfo culture, CultureInfo uiCulture)
    {
        ArgumentNullException.ThrowIfNull(culture);
        ArgumentNullException.ThrowIfNull(uiCulture);
        _previousCulture = CultureInfo.CurrentCulture;
        _previousUiCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = uiCulture;
    }

    /// <summary>Returns what <paramref name="action"/> returns when it runs with both cultures set to <paramref name="culture"/>.</summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="culture">The culture to apply while the action runs.</param>
    /// <param name="action">The code to run.</param>
    /// <returns>The action's result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="culture"/> or <paramref name="action"/> is <see langword="null"/>.</exception>
    public static T Run<T>(CultureInfo culture, Func<T> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        using var scope = new CultureScope(culture);
        return action();
    }

    /// <summary>Restores the cultures that were current when the scope was created.</summary>
    public void Dispose()
    {
        CultureInfo.CurrentCulture = _previousCulture;
        CultureInfo.CurrentUICulture = _previousUiCulture;
    }
}
