namespace PandaBear.Secrets;

/// <summary>The key holders that a SOPS file's metadata names.</summary>
public sealed class SopsFileRecipients
{
    /// <summary>Creates the description.</summary>
    /// <param name="age">The age recipients, in any order and without duplicates.</param>
    /// <param name="otherKeyTypes">The names of non-age key types that hold at least one key, such as <c>kms</c> or <c>pgp</c>.</param>
    /// <param name="concerns">Short descriptions of anything else that makes the file weaker than the rule assumes, such as a non-default encryption setting or a value that is not encrypted.</param>
    /// <param name="envelopes">For each age recipient, the text of its <c>enc</c> value, so a later version of the file can be compared label by label.</param>
    /// <exception cref="ArgumentNullException"><paramref name="age"/> or <paramref name="otherKeyTypes"/> is <see langword="null"/>.</exception>
    public SopsFileRecipients(IReadOnlyList<AgeRecipient> age, IReadOnlyList<string> otherKeyTypes, IReadOnlyList<string>? concerns = null, IReadOnlyDictionary<string, string>? envelopes = null)
    {
        ArgumentNullException.ThrowIfNull(age);
        ArgumentNullException.ThrowIfNull(otherKeyTypes);
        Concerns = [.. (concerns ?? []).Order(StringComparer.Ordinal)];
        Envelopes = new Dictionary<string, string>(envelopes ?? new Dictionary<string, string>(), StringComparer.Ordinal);
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

    /// <summary>
    /// Gets each age recipient's <c>enc</c> text, trimmed. Only someone holding the data key can produce a new valid <c>enc</c>, so a label
    /// whose text is new or different from the base revision's deserves a reviewer's attention.
    /// </summary>
    public IReadOnlyDictionary<string, string> Envelopes
    {
        get;
    }
}
