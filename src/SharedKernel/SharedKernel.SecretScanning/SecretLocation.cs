using System.Globalization;

namespace SharedKernel.SecretScanning;

/// <summary>Where a secret sits in a scanned text, without the secret itself.</summary>
/// <param name="FieldPath">
/// The name the caller gave the scanned text, such as a field path of a record (<c>notes</c>) or a file name.
/// </param>
/// <param name="Start">Character offset of the value inside the field text.</param>
/// <param name="Length">Character length of the value.</param>
/// <param name="FirstLine">First 1-based line removed with the value, when whole lines are removed.</param>
/// <param name="LastLine">Last 1-based line removed with the value, when whole lines are removed.</param>
public sealed record SecretLocation(string FieldPath, int Start, int Length, int? FirstLine = null, int? LastLine = null)
{
    /// <summary>Character offset just past the value.</summary>
    public int End => Start + Length;

    /// <summary>Describes the location without any value.</summary>
    /// <returns>Field path, offset, and length.</returns>
    public override string ToString()
    {
        string lines = FirstLine is { } first
            ? string.Create(CultureInfo.InvariantCulture, $" lines {first}-{LastLine ?? first}")
            : string.Empty;
        return string.Create(CultureInfo.InvariantCulture, $"{FieldPath}:{Start}+{Length}{lines}");
    }
}
