using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace SharedKernel.OpenApi.Tests;

/// <summary>
/// Represents a multipart form payload with a file and an extra string field.
/// </summary>
internal readonly record struct TestCommitImportFormDto
{
    /// <summary>
    /// Gets or sets the uploaded file.
    /// </summary>
    [FromForm(Name = "file")]
    public required IFormFile File { get; init; }

    /// <summary>
    /// Gets or sets a serialized conflict-resolution payload.
    /// </summary>
    [FromForm(Name = "conflictResolutions")]
    public string? ConflictResolutions { get; init; }
}
