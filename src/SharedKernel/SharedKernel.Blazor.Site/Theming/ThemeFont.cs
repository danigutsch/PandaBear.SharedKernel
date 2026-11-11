using PandaBear.BuildingBlocks;
using PandaBear.Results;

namespace PandaBear.Blazor.Site.Theming;

/// <summary>
/// A self-hosted font family: the key that typography roles reference, the family name written into the stylesheet and its faces,
/// kept in weight then style order. The family name admits only letters, digits, single inner spaces, hyphens and underscores, so it
/// is written into a CSS string as is and cannot end the string, the rule or a <c>style</c> element.
/// </summary>
public sealed class ThemeFont : ValueObject
{
    /// <summary>The longest accepted key.</summary>
    public const int MaxKeyLength = 32;

    /// <summary>The longest accepted family name.</summary>
    public const int MaxFamilyLength = 64;

    /// <summary>The message of an invalid font key.</summary>
    internal const string KeyMessage = "A font key must start with a lower-case letter and contain only lower-case letters, digits and hyphens, at most 32 characters.";

    /// <summary>The message of an invalid weight.</summary>
    internal const string WeightMessage = "The weight must be a multiple of 100 from 100 to 900.";

    private ThemeFont(string key, string family, IReadOnlyList<ThemeFontFace> faces)
    {
        Key = key;
        Family = family;
        Faces = faces;
    }

    /// <summary>Gets the key that typography roles reference.</summary>
    public string Key
    {
        get;
    }

    /// <summary>Gets the family name of the <c>@font-face</c> rules.</summary>
    public string Family
    {
        get;
    }

    /// <summary>Gets the faces in weight then style order.</summary>
    public IReadOnlyList<ThemeFontFace> Faces
    {
        get;
    }

    /// <summary>Creates a font.</summary>
    /// <param name="key">The key: a lower-case letter, then lower-case letters, digits or hyphens.</param>
    /// <param name="family">The family name.</param>
    /// <param name="faces">At least one face, no two with the same weight and style.</param>
    /// <returns>The font, or validation errors on the fields <c>key</c>, <c>family</c> and <c>faces</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="faces" /> or one face is <see langword="null" />.</exception>
    public static Result<ThemeFont> Create(string? key, string? family, IEnumerable<ThemeFontFace> faces)
    {
        ArgumentNullException.ThrowIfNull(faces);
        var list = faces.ToArray();
        if (list.Any(face => face is null))
        {
            throw new ArgumentNullException(nameof(faces), "A font face must not be null.");
        }

        var errors = new ThemeErrors();
        if (key is null || !IsKey(key))
        {
            errors.Add("key", KeyMessage);
        }

        if (!IsFamily(family))
        {
            errors.Add("family", $"A family name must be letters, digits, single spaces, hyphens or underscores, at most {MaxFamilyLength} characters.");
        }

        if (list.Length == 0)
        {
            errors.Add("faces", "A font needs at least one face.");
        }
        else if (list.DistinctBy(face => (face.Weight, face.Style)).Count() != list.Length)
        {
            errors.Add("faces", "Two faces of a font must not have the same weight and style.");
        }

        return errors.HasErrors
            ? errors.ToResult<ThemeFont>("The font is invalid.")
            : Result.Ok(new ThemeFont(key!, family!, list.OrderBy(face => face.Weight).ThenBy(face => face.Style).ToArray()));
    }

    /// <inheritdoc />
    public override string ToString() => Key;

    /// <summary>Checks the shape of a font key.</summary>
    /// <param name="key">The key.</param>
    /// <returns><see langword="true" /> for a valid key.</returns>
    internal static bool IsKey(string key) =>
        key.Length is > 0 and <= MaxKeyLength
        && char.IsAsciiLetterLower(key[0])
        && key.All(character => char.IsAsciiLetterLower(character) || char.IsAsciiDigit(character) || character == '-');

    /// <summary>Checks a font weight.</summary>
    /// <param name="weight">The weight.</param>
    /// <returns><see langword="true" /> for a multiple of 100 from 100 to 900.</returns>
    internal static bool IsWeight(int weight) => weight is >= 100 and <= 900 && weight % 100 == 0;

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Key;
        yield return Family;
        foreach (var face in Faces)
        {
            yield return face;
        }
    }

    private static bool IsFamily(string? family) =>
        family is { Length: > 0 and <= MaxFamilyLength }
        && family[0] != ' '
        && family[^1] != ' '
        && !family.Contains("  ", StringComparison.Ordinal)
        && family.All(character => char.IsLetterOrDigit(character) || character is ' ' or '-' or '_');
}
