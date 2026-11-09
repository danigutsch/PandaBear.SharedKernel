namespace PandaBear.Secrets;

/// <summary>The outcome of comparing a SOPS file's key holders with the recipient set of its creation rule.</summary>
public sealed class RecipientVerification
{
    private RecipientVerification(IReadOnlyList<AgeRecipient> missing, IReadOnlyList<AgeRecipient> unexpected, IReadOnlyList<string> otherKeyTypes, IReadOnlyList<string> concerns)
    {
        Concerns = concerns;
        Missing = missing;
        Unexpected = unexpected;
        OtherKeyTypes = otherKeyTypes;
    }

    /// <summary>Gets the rule's recipients that the file is not encrypted to.</summary>
    public IReadOnlyList<AgeRecipient> Missing
    {
        get;
    }

    /// <summary>Gets the file's age recipients that the rule does not list.</summary>
    public IReadOnlyList<AgeRecipient> Unexpected
    {
        get;
    }

    /// <summary>Gets the non-age key types that hold a key in the file; the rule allows none.</summary>
    public IReadOnlyList<string> OtherKeyTypes
    {
        get;
    }

    /// <summary>Gets what else weakens the file (see <see cref="SopsFileRecipients.Concerns"/>).</summary>
    public IReadOnlyList<string> Concerns
    {
        get;
    }

    /// <summary>Gets a value indicating whether the file's key holders equal the rule's recipient set, with nothing else.</summary>
    public bool IsExact => Missing.Count == 0 && Unexpected.Count == 0 && OtherKeyTypes.Count == 0 && Concerns.Count == 0;

    /// <summary>
    /// Compares <paramref name="actual"/> with the recipient set of <paramref name="rule"/>: equal sets are exact; a subset,
    /// a superset, a different set, or any non-age key holder is not. Order does not matter.
    /// </summary>
    /// <param name="actual">The key holders read from the file.</param>
    /// <param name="rule">The rule that applies to the file.</param>
    /// <returns>The comparison.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static RecipientVerification Compare(SopsFileRecipients actual, SopsCreationRule rule)
    {
        ArgumentNullException.ThrowIfNull(actual);
        ArgumentNullException.ThrowIfNull(rule);
        var expected = rule.Recipients;
        var missing = expected.Recipients.Where(item => !actual.Age.Contains(item)).ToArray();
        var unexpected = actual.Age.Where(item => !expected.Contains(item)).ToArray();
        return new RecipientVerification(missing, unexpected, actual.OtherKeyTypes, actual.Concerns);
    }
}
