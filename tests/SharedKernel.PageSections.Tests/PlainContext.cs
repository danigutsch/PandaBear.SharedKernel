using PandaBear.Results;

namespace PandaBear.PageSections.Tests;

/// <summary>A context of an application whose links are plain paths and that has no extension or images.</summary>
internal sealed class PlainContext : IPageSectionContext<Guid, string, NoExtra>
{
    /// <inheritdoc />
    public bool Exists(Guid imageId) => false;

    /// <inheritdoc />
    public bool IsEligible(Guid imageId, ContentCulture culture) => false;

    /// <inheritdoc />
    public Result<string> ValidateLink(string target, string field) => Result.Ok(target);

    /// <inheritdoc />
    public Result<NoExtra> ValidateExtra(NoExtra extra, bool published, string field) => Result.Ok(extra);

    /// <inheritdoc />
    public bool CountsAsContent(NoExtra extra) => false;
}
