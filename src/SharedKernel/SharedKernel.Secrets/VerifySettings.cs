namespace SharedKernel.Secrets;

/// <summary>What <see cref="SecretsRepository.Verify"/> accepts.</summary>
public sealed record VerifySettings
{
    /// <summary>Gets a value indicating whether SOPS files that match no creation rule are accepted. The risk is that nobody checks who can read them.</summary>
    public bool AllowUnverifiedFiles { get; init; }

    /// <summary>Gets a git revision to compare each label's <c>enc</c> with, or <see langword="null"/>.</summary>
    public string? BaseRevision { get; init; }

    /// <summary>Gets a value indicating whether a label whose <c>enc</c> is new or differs from <see cref="BaseRevision"/> is accepted.</summary>
    public bool AllowChangedRecipients { get; init; }
}
