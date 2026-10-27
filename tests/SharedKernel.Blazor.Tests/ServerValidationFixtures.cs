namespace PandaBear.Blazor.Tests;

/// <summary>Builds options and error dictionaries for server validation tests.</summary>
internal static class ServerValidationFixtures
{
    /// <summary>Creates options that know the name and email fields.</summary>
    /// <returns>The options.</returns>
    public static ServerValidationOptions Options() => new(["Name", "Email"], "This value is not valid.");

    /// <summary>Creates a server error dictionary.</summary>
    /// <param name="entries">The field and message pairs.</param>
    /// <returns>The dictionary.</returns>
    public static Dictionary<string, string[]> Errors(params (string Field, string[] Messages)[] entries)
        => entries.ToDictionary(entry => entry.Field, entry => entry.Messages, StringComparer.Ordinal);
}
