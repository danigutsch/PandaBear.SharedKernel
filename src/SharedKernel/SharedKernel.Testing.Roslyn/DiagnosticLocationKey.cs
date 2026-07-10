using Microsoft.CodeAnalysis;

namespace SharedKernel.Testing.Roslyn;

/// <summary>
/// Creates stable keys for diagnostics reported at source locations.
/// </summary>
public static class DiagnosticLocationKey
{
    /// <summary>
    /// Creates a key from the diagnostic source path, line, and character.
    /// </summary>
    /// <param name="diagnostic">The diagnostic.</param>
    /// <returns>The diagnostic location key.</returns>
    public static string Create(Diagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);

        var lineSpan = diagnostic.Location.GetLineSpan();
        return $"{lineSpan.Path}:{lineSpan.StartLinePosition.Line}:{lineSpan.StartLinePosition.Character}";
    }
}
