namespace SharedKernel.PageSections;

/// <summary>An unvalidated question of a section's frequently asked questions.</summary>
/// <param name="Question">The question, one line; required.</param>
/// <param name="Answer">The answer; paragraphs are separated by blank lines; required.</param>
public sealed record PageQuestionDefinition(string? Question, string? Answer);
