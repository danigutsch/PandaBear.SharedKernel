namespace PandaBear.Results.Tests;

/// <summary>
/// Helpers that build invalid inputs for the result error tests.
/// </summary>
internal static class ResultErrorTestsHelpers
{
    /// <summary>
    /// Creates a validation error dictionary whose Name entry has a null message array.
    /// </summary>
    public static Dictionary<string, string[]> CreateValidationErrorsWithNullMessageArray() =>
        new Dictionary<string, string[]>
        {
            ["Name"] = NullArgumentData.StringArray(),
        };
}
