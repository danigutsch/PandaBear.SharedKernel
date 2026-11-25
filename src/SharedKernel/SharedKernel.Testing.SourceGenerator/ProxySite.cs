namespace SharedKernel.Testing.SourceGenerator;

/// <summary>What one place in the source that wraps or hooks a service asks for: a proxy to generate, or diagnostics to report.</summary>
/// <param name="Model">The proxy to generate, or <see langword="null"/> when the service cannot be proxied.</param>
/// <param name="Diagnostics">The diagnostics to report at this place.</param>
internal sealed record ProxySite(ProxyModel? Model, EquatableArray<DiagnosticInfo> Diagnostics);
