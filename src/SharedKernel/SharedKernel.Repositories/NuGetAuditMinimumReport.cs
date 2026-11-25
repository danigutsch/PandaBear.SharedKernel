namespace SharedKernel.Repositories;

/// <summary>Evidence supporting the asserted minimum or explaining why it remains unproven.</summary>
/// <param name="Status">The outcome of the bounded search.</param>
/// <param name="Attempts">Number of exact candidate sets tried in the isolated workspace.</param>
/// <param name="SelectedUpdates">The verified direct edits, in deterministic order.</param>
/// <param name="Catalog">Per-owner source completeness without feed locations or credentials.</param>
internal sealed record NuGetAuditMinimumReport(string Status, int Attempts, string[] SelectedUpdates, NuGetVersionSet[] Catalog);
