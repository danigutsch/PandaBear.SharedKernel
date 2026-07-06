namespace SharedKernel.OpenApi;

/// <summary>
/// Describes one named OpenAPI document and the route prefix it includes.
/// </summary>
/// <param name="DocumentName">The OpenAPI document name.</param>
/// <param name="RoutePrefix">The route prefix included in the document.</param>
public sealed record OpenApiBoundaryDocument(string DocumentName, string RoutePrefix);
