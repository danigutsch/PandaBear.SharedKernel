using PandaBear.VaultManager.Tool.Review;

namespace PandaBear.VaultManager.Tool.Tests.Review;

/// <summary>Terminal for review command tests: records each session the UI would show and acts on it instead of a user.</summary>
internal sealed class FakeTerminal
{
    private readonly List<ReviewSession> sessions = [];
    private readonly List<int> promptsBeforeRun = [];

    /// <summary>Whether standard output is a terminal.</summary>
    public bool OutputIsTerminal { get; init; } = true;

    /// <summary>What the user does in the UI; returns the exit code of the UI.</summary>
    public Func<ReviewSession, int> User { get; init; } = static _ => 0;

    /// <summary>Counts the password prompts so far; read when the UI starts.</summary>
    public Func<int> Prompts { get; init; } = static () => 0;

    /// <summary>Sessions the UI ran for.</summary>
    public IReadOnlyList<ReviewSession> Sessions => sessions;

    /// <summary>Number of password prompts asked before each UI run.</summary>
    public IReadOnlyList<int> PromptsBeforeRun => promptsBeforeRun;

    /// <summary>Creates the review terminal that reports <see cref="OutputIsTerminal"/> and runs <see cref="User"/> for each session.</summary>
    public ReviewTerminal Terminal() => new(
        () => OutputIsTerminal,
        (session, _) =>
        {
            sessions.Add(session);
            promptsBeforeRun.Add(Prompts());
            return User(session);
        });
}
