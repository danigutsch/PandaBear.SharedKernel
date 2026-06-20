namespace SharedKernel.Results;

/// <summary>
/// Describes one generated centralized error entry.
/// </summary>
public sealed class ResultErrorCatalogEntry
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ResultErrorCatalogEntry"/> class.
    /// </summary>
    public ResultErrorCatalogEntry(
        string identifier,
        string documentationPath,
        string providerType,
        string memberName,
        ResultStatus status,
        int httpStatusCode,
        string code,
        string detailTemplate,
        string? summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);
        ArgumentException.ThrowIfNullOrWhiteSpace(documentationPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerType);
        ArgumentException.ThrowIfNullOrWhiteSpace(memberName);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(detailTemplate);

        Identifier = identifier;
        DocumentationPath = documentationPath;
        ProviderType = providerType;
        MemberName = memberName;
        Status = status;
        HttpStatusCode = httpStatusCode;
        Code = code;
        DetailTemplate = detailTemplate;
        Summary = summary;
    }

    /// <summary>
    /// Gets the stable generated identifier for the error entry.
    /// </summary>
    public string Identifier { get; }

    /// <summary>
    /// Gets the canonical repository documentation path for the error entry.
    /// </summary>
    public string DocumentationPath { get; }

    /// <summary>
    /// Gets the fully qualified error provider type name.
    /// </summary>
    public string ProviderType { get; }

    /// <summary>
    /// Gets the error provider member name.
    /// </summary>
    public string MemberName { get; }

    /// <summary>
    /// Gets the result status produced by the member.
    /// </summary>
    public ResultStatus Status { get; }

    /// <summary>
    /// Gets the HTTP status code conventionally associated with the result status.
    /// </summary>
    public int HttpStatusCode { get; }

    /// <summary>
    /// Gets the stable machine-readable result error code.
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// Gets the generated detail template derived from the result factory call.
    /// </summary>
    public string DetailTemplate { get; }

    /// <summary>
    /// Gets the optional XML-doc summary for the error provider member.
    /// </summary>
    public string? Summary { get; }
}
