using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace SharedKernel.OpenApi.Tests;

/// <summary>
/// Represents an optional multipart form shape for normalization tests.
/// </summary>
internal readonly record struct TestOptionalCommitImportFormDto
{
    /// <summary>
    /// Gets the optional file payload.
    /// </summary>
    [FromForm(Name = "file")]
    public IFormFile? File { get; init; }

    /// <summary>
    /// Gets the optional conflict resolutions payload.
    /// </summary>
    [FromForm(Name = "conflictResolutions")]
    public string? ConflictResolutions { get; init; }
}
