namespace PandaBear.Secrets;

/// <summary>The key holders that a SOPS file's metadata names.</summary>
public sealed class SopsFileRecipients
{
    /// <summary>Creates the description.</summary>
    /// <param name="age">The age recipients, in any order and without duplicates.</param>
    /// <param name="otherKeyTypes">The names of non-age key types that hold at least one key, such as <c>kms</c> or <c>pgp</c>.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public SopsFileRecipients(IReadOnlyList<AgeRecipient> age, IReadOnlyList<string> otherKeyTypes)
    {
        ArgumentNullException.ThrowIfNull(age);
        ArgumentNullException.ThrowIfNull(otherKeyTypes);
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
}
