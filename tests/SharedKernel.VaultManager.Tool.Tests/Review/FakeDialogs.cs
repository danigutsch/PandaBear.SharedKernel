using PandaBear.VaultManager.Tool.Review;

namespace PandaBear.VaultManager.Tool.Tests.Review;

/// <summary>Answers the questions of the review window and records every question and message.</summary>
internal sealed class FakeDialogs : IReviewDialogs
{
    private readonly Queue<QuitChoice> quitAnswers = new();
    private readonly List<bool> quitQuestions = [];
    private readonly List<IReadOnlyList<ReviewKey>> help = [];
    private readonly List<string> errors = [];
    private readonly List<string> confirmations = [];

    /// <summary>Why the dialogs cannot be shown, or <see langword="null"/> when they can.</summary>
    public string? Unavailable
    {
        get; init;
    }

    /// <summary>Password returned for the new export, or <see langword="null"/> to cancel.</summary>
    public string? Password
    {
        get; init;
    }

    /// <summary>Whether the review could save, for each quit question asked.</summary>
    public IReadOnlyList<bool> QuitQuestions => quitQuestions;

    /// <summary>Number of export password questions asked.</summary>
    public int PasswordQuestions
    {
        get; private set;
    }

    /// <summary>Key lists shown as help.</summary>
    public IReadOnlyList<IReadOnlyList<ReviewKey>> Help => help;

    /// <summary>Answer to every yes-or-no question.</summary>
    public bool ConfirmAnswer { get; init; }

    /// <summary>Titles of the yes-or-no questions asked.</summary>
    public IReadOnlyList<string> Confirmations => confirmations;

    /// <summary>Problems shown.</summary>
    public IReadOnlyList<string> Errors => errors;

    /// <summary>Queues answers to quit questions; once they run out, the answer is <see cref="QuitChoice.Discard"/>.</summary>
    public FakeDialogs AnswerQuit(params QuitChoice[] answers)
    {
        foreach (QuitChoice answer in answers)
        {
            quitAnswers.Enqueue(answer);
        }

        return this;
    }

    public QuitChoice AskQuit(bool canSave)
    {
        quitQuestions.Add(canSave);
        return quitAnswers.Count > 0 ? quitAnswers.Dequeue() : QuitChoice.Discard;
    }

    public string? AskExportPassword()
    {
        PasswordQuestions++;
        return Password;
    }

    public bool Confirm(string title, string message)
    {
        confirmations.Add(title);
        return ConfirmAnswer;
    }

    public void ShowHelp(IReadOnlyList<ReviewKey> keys) => help.Add(keys);

    public void ShowError(string message) => errors.Add(message);
}
