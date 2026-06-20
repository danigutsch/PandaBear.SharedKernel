namespace SharedKernel.Results.SourceGenerator;

internal sealed class ErrorCatalogEntryModel
{
    public string Identifier { get; set; } = string.Empty;

    public string DocumentationPath { get; set; } = string.Empty;

    public string ProviderType { get; set; } = string.Empty;

    public string MemberName { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public int HttpStatusCode { get; set; }

    public string Code { get; set; } = string.Empty;

    public string DetailTemplate { get; set; } = string.Empty;

    public string? Summary { get; set; }
}
