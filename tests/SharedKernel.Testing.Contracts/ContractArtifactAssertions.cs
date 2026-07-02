namespace SharedKernel.Testing.Contracts;

/// <summary>
/// Shared assertions for published contract artifacts.
/// </summary>
public static class ContractArtifactAssertions
{
    /// <summary>
    /// Verifies that a generated contract artifact still matches the canonical artifact.
    /// </summary>
    /// <typeparam name="TContract">The contract artifact type.</typeparam>
    /// <param name="canonicalContract">The canonical artifact.</param>
    /// <param name="generatedContract">The generated artifact.</param>
    public static void MatchesGeneratedArtifact<TContract>(TContract canonicalContract, TContract generatedContract)
    {
        ArgumentNullException.ThrowIfNull(canonicalContract);
        ArgumentNullException.ThrowIfNull(generatedContract);

        if (!EqualityComparer<TContract>.Default.Equals(canonicalContract, generatedContract))
        {
            throw new InvalidOperationException("Generated contract artifact does not match the canonical artifact.");
        }
    }
}
