namespace PandaBear.Repository.Tests;

/// <summary>Answers the Git commands that stage-candidate runs as a clean top-level checkout of the pinned revision.</summary>
internal sealed class CandidateGit
{
    /// <summary>Gets or sets the output of <c>git rev-parse --is-inside-work-tree --show-prefix</c>.</summary>
    public string Location { get; set; } = "true\n\n";

    /// <summary>Gets or sets the output of <c>git rev-parse --verify HEAD</c>.</summary>
    public string Head { get; set; } = CandidateStagingScope.Revision + "\n";

    /// <summary>Gets or sets the output of <c>git status --porcelain</c>.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Gets or sets an exception that every command throws instead of answering.</summary>
    public Exception? Failure
    {
        get; set;
    }

    public List<ProcessCall> Calls { get; } = [];

    public string[] Commands => [.. Calls.Select(static call => string.Join(' ', call.Arguments))];

    public Task<string> Run(string[] arguments, string workingDirectory, CancellationToken ct)
    {
        Calls.Add(new ProcessCall("git", arguments, workingDirectory, ct));
        if (Failure is not null)
        {
            return Task.FromException<string>(Failure);
        }

        return Task.FromResult(string.Join(' ', arguments) switch
        {
            "rev-parse --is-inside-work-tree --show-prefix" => Location,
            "rev-parse --verify HEAD" => Head,
            "status --porcelain" => Status,
            var command => throw new InvalidOperationException("Unexpected git command: " + command),
        });
    }
}
