namespace SharedKernel.Blazor.Site;

/// <summary>Caller-localized labels for rendering photo credits.</summary>
/// <param name="Creator">The label for identified creators.</param>
/// <param name="CopyrightOwner">The label for identified copyright owners.</param>
/// <param name="Licensor">The label for identified licensors.</param>
/// <param name="Designated">The label for other parties designated for credit.</param>
/// <param name="Source">The label for the original source link.</param>
/// <param name="Terms">The general label for the terms.</param>
/// <param name="Details">The label for the full credit link or disclosure.</param>
/// <param name="Modification">The label for the current modification notice.</param>
/// <param name="PreviousModifications">The label for earlier modification notices.</param>
public sealed record PhotoCreditLabels(
    string Creator, string CopyrightOwner, string Licensor, string Designated, string Source,
    string Terms, string Details, string Modification, string PreviousModifications)
{
    /// <summary>Checks that each localized label gives its text or control a readable name.</summary>
    /// <param name="parameterName">The component parameter that supplied the labels.</param>
    /// <exception cref="ArgumentException">Any label is missing or blank.</exception>
    internal void Require(string parameterName)
    {
        string[] values = [Creator, CopyrightOwner, Licensor, Designated, Source, Terms, Details, Modification, PreviousModifications];
        if (values.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Photo credit labels must be present.", parameterName);
        }
    }
}
