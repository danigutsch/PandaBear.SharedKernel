namespace SharedKernel.Results;

/// <summary>
/// Describes one generated centralized error entry.
/// </summary>
public sealed class ResultErrorCatalogEntry
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ResultErrorCatalogEntry"/> class.
    /// </summary>
    public ResultErrorCatalogEntry()
    {
    }

    /// <summary>
    /// Gets the stable generated identifier for the error entry.
    /// </summary>
    public required string Identifier { get; init; }

    /// <summary>
    /// Gets the canonical repository documentation path for the error entry.
    /// </summary>
    public required string DocumentationPath { get; init; }

    /// <summary>
    /// Gets the fully qualified error provider type name.
    /// </summary>
    public required string ProviderType { get; init; }

    /// <summary>
    /// Gets the error provider member name.
    /// </summary>
    public required string MemberName { get; init; }

    /// <summary>
    /// Gets the result status produced by the member.
    /// </summary>
    public required ResultStatus Status { get; init; }

    /// <summary>
    /// Gets the HTTP status code conventionally associated with the result status.
    /// </summary>
    public required int HttpStatusCode { get; init; }

    /// <summary>
    /// Gets the stable machine-readable result error code.
    /// </summary>
    public required string Code { get; init; }

    /// <summary>
    /// Gets the generated detail template derived from the result factory call.
    /// </summary>
    public required string DetailTemplate { get; init; }

    /// <summary>
    /// Gets the optional XML-doc summary for the error provider member.
    /// </summary>
    public string? Summary { get; init; }
}
