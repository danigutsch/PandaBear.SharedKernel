using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace SharedKernel.Testing.SourceGenerator;

/// <summary>A source location without its syntax tree, so a cached model does not keep old trees alive.</summary>
/// <param name="FilePath">The path of the source file.</param>
/// <param name="Span">The text span.</param>
/// <param name="LineSpan">The line span.</param>
internal sealed record LocationInfo(string FilePath, TextSpan Span, LinePositionSpan LineSpan)
{
    /// <summary>Captures a source location.</summary>
    /// <param name="location">The location.</param>
    /// <returns>The captured location, or <see langword="null"/> for a location outside source.</returns>
    public static LocationInfo? From(Location location) =>
        location.SourceTree is { } tree ? new LocationInfo(tree.FilePath, location.SourceSpan, location.GetLineSpan().Span) : null;

    /// <summary>Recreates the location.</summary>
    /// <returns>The location.</returns>
    public Location ToLocation() => Location.Create(FilePath, Span, LineSpan);
}
