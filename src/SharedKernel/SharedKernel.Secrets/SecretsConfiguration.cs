namespace SharedKernel.Secrets;

/// <summary>The consumer policy in <c>.config/sharedkernel-secrets.json</c>.</summary>
/// <param name="Version">Format version; only <c>1</c> is supported.</param>
/// <param name="CreationRules">The creation rules in the order they are tried; the first rule that matches a path applies.</param>
internal sealed record SecretsConfiguration(int Version, CreationRuleConfiguration[] CreationRules);
