namespace SharedKernel.Results.SourceGenerator;

internal sealed record ErrorCatalogEntryModel(
    string Identifier,
    string DocumentationPath,
    string ProviderType,
    string MemberName,
    string Status,
    string Code,
    string DetailTemplate,
    string? Summary);
