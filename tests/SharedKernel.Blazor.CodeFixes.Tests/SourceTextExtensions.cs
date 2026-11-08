namespace PandaBear.Blazor.CodeFixes.Tests;

/// <summary>Helpers for comparing source text without its formatting.</summary>
internal static class SourceTextExtensions
{
    /// <summary>Removes every whitespace character, so a test can ignore indentation and line breaks.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The text without whitespace.</returns>
    public static string WithoutWhitespace(this string text) => new(text.Where(static character => !char.IsWhiteSpace(character)).ToArray());
}
