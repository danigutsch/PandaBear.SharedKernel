namespace PandaBear.VaultManager.Tool.Tests.Review;

/// <summary>What happened when a review was canceled while a dialog was open.</summary>
/// <param name="ExitCode">Exit code the review returned.</param>
/// <param name="DialogTitle">Title of the dialog that was open, or <see langword="null"/> when no dialog opened.</param>
/// <param name="DialogClosedByCancellation">Whether canceling the review asked the open dialog to close.</param>
/// <param name="OpenSessions">Terminal.Gui sessions still running after the review returned.</param>
internal sealed record CanceledReviewRun(int ExitCode, string? DialogTitle, bool DialogClosedByCancellation, int OpenSessions);
