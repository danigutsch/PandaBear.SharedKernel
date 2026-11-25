using System.Globalization;

namespace SharedKernel.Blazor.Tests;

/// <summary>Sets the current culture until disposed, then restores the earlier one.</summary>
internal sealed class CurrentCultureScope : IDisposable
{
    private readonly CultureInfo _previous = CultureInfo.CurrentCulture;

    /// <summary>Sets the current culture.</summary>
    /// <param name="name">The culture name, such as <c>de-DE</c>.</param>
    public CurrentCultureScope(string name) => CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);

    /// <inheritdoc />
    public void Dispose() => CultureInfo.CurrentCulture = _previous;
}
