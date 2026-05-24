namespace SharedKernel.OpenApi;

/// <summary>
/// Defines a named OpenAPI document and the route prefix that belongs in that document.
/// </summary>
/// <param name="DocumentName">The OpenAPI document name.</param>
/// <param name="RoutePrefix">The route prefix used to select matching endpoints.</param>
public sealed record OpenApiBoundaryDocument(string DocumentName, string RoutePrefix);
