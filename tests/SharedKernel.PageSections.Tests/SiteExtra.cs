namespace SharedKernel.PageSections.Tests;

/// <summary>V2's extension of a section: a settings block and a condition; <c>default</c> means none.</summary>
/// <param name="Block">The settings block.</param>
/// <param name="Condition">The condition.</param>
public readonly record struct SiteExtra(PageBlock Block, SectionCondition Condition);
