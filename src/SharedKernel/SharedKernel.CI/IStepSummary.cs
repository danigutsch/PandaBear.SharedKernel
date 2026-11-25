namespace SharedKernel.CI;

/// <summary>
/// Appends Markdown to the summary of the current step of a forge's workflow run, such as the <c>GITHUB_STEP_SUMMARY</c>
/// file of GitHub Actions or the <c>FORGEJO_STEP_SUMMARY</c> file of Forgejo Actions.
/// </summary>
public interface IStepSummary
{
    /// <summary>Appends <paramref name="markdown"/> after what the step already wrote.</summary>
    /// <param name="markdown">The Markdown to append.</param>
    /// <param name="ct">Cancels the write.</param>
    /// <exception cref="IOException">The summary could not be written.</exception>
    Task Append(string markdown, CancellationToken ct);
}
