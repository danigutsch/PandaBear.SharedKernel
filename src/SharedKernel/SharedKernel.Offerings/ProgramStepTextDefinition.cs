namespace PandaBear.Offerings;

/// <summary>Unvalidated text of one step in one culture.</summary>
/// <param name="Title">The title; required, and it must not begin with a forbidden prefix and a number, such as "Dia 1".</param>
/// <param name="Text">The text; paragraphs are separated by blank lines.</param>
/// <param name="Note">The optional one-line note.</param>
public sealed record ProgramStepTextDefinition(string? Title, string? Text = null, string? Note = null);
