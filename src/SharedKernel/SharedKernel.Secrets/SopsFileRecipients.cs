namespace PandaBear.Secrets;

/// <summary>The key holders that a SOPS file's metadata names.</summary>
public sealed class SopsFileRecipients
{
    /// <summary>Creates the description.</summary>
    /// <param name="age">The age recipients, in any order and without duplicates.</param>
    /// <param name="otherKeyTypes">The names of non-age key types that hold at least one key, such as <c>kms</c> or <c>pgp</c>.</param>
    /// <param name="concerns">Short descriptions of anything else that makes the file weaker than the rule assumes, such as a non-default encryption setting or a value that is not encrypted.</param>
    /// <exception cref="ArgumentNullException"><paramref name="age"/> or <paramref name="otherKeyTypes"/> is <see langword="null"/>.</exception>
    public SopsFileRecipients(IReadOnlyList<AgeRecipient> age, IReadOnlyList<string> otherKeyTypes, IReadOnlyList<string>? concerns = null)
    {
        ArgumentNullException.ThrowIfNull(age);
        ArgumentNullException.ThrowIfNull(otherKeyTypes);
        Concerns = [.. (concerns ?? []).Order(StringComparer.Ordinal)];
        Age = [.. age.OrderBy(static item => item.Value, StringComparer.Ordinal)];
        OtherKeyTypes = [.. otherKeyTypes.Order(StringComparer.Ordinal)];
    }

    /// <summary>Gets the age recipients, ordered by text.</summary>
    public IReadOnlyList<AgeRecipient> Age
    {
        get;
    }

    /// <summary>Gets the non-age key types that hold a key. Anyone holding such a key can read the file.</summary>
    public IReadOnlyList<string> OtherKeyTypes
    {
        get;
    }

    /// <summary>
    /// Gets what else weakens the file: a non-default <c>unencrypted_*</c>, <c>encrypted_*</c>, or <c>mac_only_encrypted</c> setting, or values that are not
    /// encrypted. A file with a concern is never exact.
    /// </summary>
    public IReadOnlyList<string> Concerns
    {
        get;
    }
}
