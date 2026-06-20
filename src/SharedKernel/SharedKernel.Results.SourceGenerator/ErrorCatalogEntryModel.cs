namespace SharedKernel.Results.SourceGenerator;

internal sealed class ErrorCatalogEntryModel(
    string identifier,
    string documentationPath,
    string providerType,
    string memberName,
    string status,
    int httpStatusCode,
    string code,
    string detailTemplate,
    string? summary)
{
    public string Identifier => identifier;

    public string DocumentationPath => documentationPath;

    public string ProviderType => providerType;

    public string MemberName => memberName;

    public string Status => status;

    public int HttpStatusCode => httpStatusCode;

    public string Code => code;

    public string DetailTemplate => detailTemplate;

    public string? Summary => summary;
}
