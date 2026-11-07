using Microsoft.AspNetCore.Components.Forms;

namespace PandaBear.Blazor.Tests;

/// <summary>Builds <see cref="ServerFormSubmission"/> instances for tests.</summary>
internal static class ServerFormSubmissionFixtures
{
    /// <summary>Creates a submission over the context with the name and email fields known.</summary>
    /// <param name="context">The edit context.</param>
    /// <param name="failureMessage">The general failure message.</param>
    /// <param name="conflictMessage">The conflict message, or <see langword="null"/>.</param>
    /// <param name="notFoundMessage">The not found message, or <see langword="null"/>.</param>
    /// <returns>The submission; the caller disposes it.</returns>
    public static ServerFormSubmission Create(EditContext context, string failureMessage, string? conflictMessage, string? notFoundMessage) =>
        new(context, ServerValidationFixtures.Options(), field => "form-" + field, failureMessage)
        {
            ConflictMessage = conflictMessage,
            NotFoundMessage = notFoundMessage,
        };
}
