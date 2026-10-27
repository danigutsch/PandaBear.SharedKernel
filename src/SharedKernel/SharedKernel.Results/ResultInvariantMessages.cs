namespace PandaBear.Results;

/// <summary>Shared exception messages for result invariant violations.</summary>
internal static class ResultInvariantMessages
{
    /// <summary>Message used when a validation failure is created without field-level details.</summary>
    internal const string ValidationErrorsMustContainFieldDetails = "Validation errors must include field details.";
}
