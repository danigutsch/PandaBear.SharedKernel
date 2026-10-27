namespace PandaBear.Blazor;

/// <summary>A bounded server validation message that was placed on a known field.</summary>
/// <param name="FieldName">The known model field name, as configured in <see cref="ServerValidationOptions.KnownFields" />.</param>
/// <param name="Message">The bounded message.</param>
public sealed record ServerValidationIssue(string FieldName, string Message);
