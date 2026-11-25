namespace SharedKernel.Versioning;

/// <summary>Which consumer <see cref="ConsumerVerifier.Verify"/> checks against which candidate, and how.</summary>
/// <param name="RepoRoot">The consumer repository root.</param>
/// <param name="Candidate">The candidate manifest, <c>candidate.json</c>, beside the packages it lists.</param>
/// <param name="Solution">The solution whose projects' lock files are checked, or <see langword="null"/> for the only <c>.slnx</c> in the repository root.</param>
/// <param name="Output">The file that receives the evidence; it must not be the candidate manifest.</param>
/// <param name="Command">The command and its arguments, run in the clone against the staged candidate packages. Empty when using <see cref="Recipe"/>.</param>
public sealed record ConsumerVerificationOptions(
    string RepoRoot,
    string Candidate,
    string? Solution,
    string Output,
    IReadOnlyList<string> Command)
{
    /// <summary>Gets the committed JSON command recipe path, resolved inside the consumer repository.</summary>
    public string? Recipe { get; init; }

    /// <summary>Gets caller-supplied recipe inputs in <c>name=value</c> form.</summary>
    public IReadOnlyList<string> RecipeInputs { get; init; } = [];
}
