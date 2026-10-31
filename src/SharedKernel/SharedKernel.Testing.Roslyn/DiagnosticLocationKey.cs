using Microsoft.CodeAnalysis;

namespace PandaBear.Testing.Roslyn;

/// <summary>
/// Creates stable keys for diagnostics reported at source locations.
/// </summary>
public static class DiagnosticLocationKey
{
    /// <summary>
    /// Creates a key from the diagnostic source path, line, and character.
    /// </summary>
    /// <param name="diagnostic">The diagnostic.</param>
    /// <returns>The diagnostic location key with zero-based line and character positions.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="diagnostic"/> is <see langword="null"/>.</exception>
    public static string Create(Diagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);

        var lineSpan = diagnostic.Location.GetLineSpan();
        return $"{lineSpan.Path}:{lineSpan.StartLinePosition.Line}:{lineSpan.StartLinePosition.Character}";
    }
}
