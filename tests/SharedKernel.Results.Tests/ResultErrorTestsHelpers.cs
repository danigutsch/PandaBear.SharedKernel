namespace PandaBear.Results.Tests;

internal static class ResultErrorTestsHelpers
{
    public static Dictionary<string, string[]> CreateValidationErrorsWithNullMessageArray() =>
        new Dictionary<string, string[]>
        {
            ["Name"] = NullArgumentData.StringArray(),
        };
}
