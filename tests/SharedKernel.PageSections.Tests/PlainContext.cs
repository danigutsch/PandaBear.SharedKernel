using SharedKernel.Results;

namespace SharedKernel.PageSections.Tests;

/// <summary>
/// A context of an application whose links are plain paths and that has no extension or images: it writes only the link check and the
/// image knowledge, and takes the default extension handling.
/// </summary>
internal sealed class PlainContext : IPageSectionContext<Guid, string, string, NoExtra>
{
    /// <inheritdoc />
    public bool Exists(Guid imageId) => false;

    /// <inheritdoc />
    public bool IsEligible(Guid imageId, ContentCulture culture) => false;

    /// <inheritdoc />
    public Result<string> ValidateLink(string input, string field) => Result.Ok(input);
}
