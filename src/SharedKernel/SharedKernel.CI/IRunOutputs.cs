namespace SharedKernel.CI;

/// <summary>
/// Hands named values of the current step to later steps and jobs of a forge's workflow run, such as the
/// <c>GITHUB_OUTPUT</c> file of GitHub Actions or the <c>FORGEJO_OUTPUT</c> file of Forgejo Actions.
/// </summary>
public interface IRunOutputs
{
    /// <summary>Writes <paramref name="outputs"/> in order, in one write.</summary>
    /// <param name="outputs">The names and values. A name is not empty and holds no <c>=</c> or line break; a value holds no line break.</param>
    /// <param name="ct">Cancels the write.</param>
    /// <exception cref="ArgumentException">A name or value has a forbidden character.</exception>
    /// <exception cref="IOException">The outputs could not be written.</exception>
    Task Write(IReadOnlyList<KeyValuePair<string, string>> outputs, CancellationToken ct);
}
