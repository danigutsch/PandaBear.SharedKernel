using System.Globalization;

namespace PandaBear.VaultManager.Tool.Secrets;

/// <summary>Where a secret sits inside an item, without the secret itself.</summary>
/// <param name="FieldPath">
/// Field path, for example <c>notes</c>, <c>login.password</c>, <c>fields[2].value</c>, or
/// <c>attachment:&lt;file name&gt;</c>.
/// </param>
/// <param name="Start">Character offset of the value inside the field text.</param>
/// <param name="Length">Character length of the value.</param>
/// <param name="FirstLine">First 1-based line removed with the value, when whole lines are removed.</param>
/// <param name="LastLine">Last 1-based line removed with the value, when whole lines are removed.</param>
internal sealed record SecretLocation(string FieldPath, int Start, int Length, int? FirstLine = null, int? LastLine = null)
{
    /// <summary>Notes field path.</summary>
    public const string Notes = "notes";

    /// <summary>Login password field path.</summary>
    public const string LoginPassword = "login.password";

    /// <summary>Login user name field path.</summary>
    public const string LoginUsername = "login.username";

    /// <summary>Login TOTP field path.</summary>
    public const string LoginTotp = "login.totp";

    /// <summary>Card number field path.</summary>
    public const string CardNumber = "card.number";

    /// <summary>Prefix of attachment field paths.</summary>
    public const string AttachmentPrefix = "attachment:";

    /// <summary>Character offset just past the value.</summary>
    public int End => Start + Length;

    /// <summary>Field path of a custom field value.</summary>
    /// <param name="index">Zero-based custom field index.</param>
    /// <returns>The field path.</returns>
    public static string FieldValue(int index)
    {
        return string.Create(CultureInfo.InvariantCulture, $"fields[{index}].value");
    }

    /// <summary>Field path of a custom field name.</summary>
    /// <param name="index">Zero-based custom field index.</param>
    /// <returns>The field path.</returns>
    public static string FieldName(int index)
    {
        return string.Create(CultureInfo.InvariantCulture, $"fields[{index}].name");
    }

    /// <summary>Field path of attachment text.</summary>
    /// <param name="fileName">Attachment file name.</param>
    /// <returns>The field path.</returns>
    public static string Attachment(string fileName)
    {
        return AttachmentPrefix + fileName;
    }

    /// <summary>Zero-based custom field index of a <c>fields[i].value</c> or <c>fields[i].name</c> path.</summary>
    /// <param name="fieldPath">Field path.</param>
    /// <param name="index">Custom field index.</param>
    /// <param name="isName">Whether the path names the field label rather than its value.</param>
    /// <returns><see langword="true"/> when the path refers to a custom field.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="fieldPath"/> is <see langword="null"/>.</exception>
    public static bool TryParseField(string fieldPath, out int index, out bool isName)
    {
        ArgumentNullException.ThrowIfNull(fieldPath);
        index = -1;
        isName = false;
        if (!fieldPath.StartsWith("fields[", StringComparison.Ordinal))
        {
            return false;
        }

        int close = fieldPath.IndexOf(']', StringComparison.Ordinal);
        if (close < 0)
        {
            return false;
        }

        string suffix = fieldPath[(close + 1)..];
        if (suffix is not (".value" or ".name")
            || !int.TryParse(fieldPath.AsSpan(7, close - 7), NumberStyles.None, CultureInfo.InvariantCulture, out index))
        {
            return false;
        }

        isName = suffix == ".name";
        return true;
    }

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
