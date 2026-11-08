namespace PandaBear.EntityFrameworkCore;

/// <summary>What a database provider reported about a unique-index violation: a constraint name, a table with columns, or both.</summary>
/// <param name="ConstraintName">The index or constraint name when the provider reports one; otherwise <see langword="null"/>.</param>
/// <param name="Table">The table when the provider reports one; otherwise <see langword="null"/>.</param>
/// <param name="Columns">The violated columns when the provider reports them; otherwise empty.</param>
public sealed record UniqueViolation(string? ConstraintName, string? Table, IReadOnlyList<string> Columns);
