using Microsoft.CodeAnalysis;

namespace SharedKernel.Mediator.CodeFixes;

/// <summary>
/// Provides symbol-display formats that preserve nullable reference annotations in generated code.
/// </summary>
internal static class SymbolDisplayFormats
{
    /// <summary>
    /// Gets the fully qualified symbol-display format with nullable reference modifiers preserved.
    /// </summary>
    public static SymbolDisplayFormat FullyQualifiedWithNullability { get; } =
        SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
            SymbolDisplayFormat.FullyQualifiedFormat.MiscellaneousOptions
            | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    /// <summary>
    /// Gets the minimally qualified symbol-display format with nullable reference modifiers preserved.
    /// </summary>
    public static SymbolDisplayFormat MinimallyQualifiedWithNullability { get; } =
        SymbolDisplayFormat.MinimallyQualifiedFormat.WithMiscellaneousOptions(
            SymbolDisplayFormat.MinimallyQualifiedFormat.MiscellaneousOptions
            | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);
}
