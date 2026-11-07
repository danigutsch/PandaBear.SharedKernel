namespace PandaBear.Mediator;

/// <summary>The field and message a mapped unique rule produces.</summary>
/// <param name="Field">The field name.</param>
/// <param name="Message">The caller-authored message.</param>
internal readonly record struct UniqueRuleError(string Field, string Message);
