namespace PandaBear.BuildingBlocks.Tests;

/// <summary>Describes the characters the country-name sort key supports.</summary>
internal static class CountryNameAlphabet
{
    private const string Plain = " '-,.()&0123456789";

    private const string FoldedAccents = "ÅãçéíôüÉ";

    /// <summary>Determines whether <paramref name="character"/> is handled by the sort key.</summary>
    /// <param name="character">The character to check.</param>
    /// <returns><see langword="true"/> for ASCII letters, the plain punctuation set and the folded accented set.</returns>
    internal static bool IsSupported(char character) =>
        char.IsAsciiLetter(character) || Plain.Contains(character, StringComparison.Ordinal) || FoldedAccents.Contains(character, StringComparison.Ordinal);

    /// <summary>Computes the expected key independently of the implementation.</summary>
    /// <param name="name">The English name.</param>
    /// <returns>The upper-case, accent-free key.</returns>
    internal static string ExpectedKey(string name) =>
        string.Concat(name.Select(character => character switch
        {
            'Å' => 'A',
            'ã' => 'a',
            'ç' => 'c',
            'é' or 'É' => 'e',
            'í' => 'i',
            'ô' => 'o',
            'ü' => 'u',
            _ => character,
        })).ToUpperInvariant();
}
