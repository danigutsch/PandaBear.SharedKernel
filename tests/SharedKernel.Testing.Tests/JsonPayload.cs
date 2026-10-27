using System.Text.Json;

namespace PandaBear.Testing.Tests;

/// <summary>Parses JSON text into a detached element for assertion tests.</summary>
internal static class JsonPayload
{
    /// <summary>Parses the JSON text.</summary>
    /// <param name="json">The JSON text.</param>
    /// <returns>A detached root element.</returns>
    public static JsonElement Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    /// <summary>Runs an assertion and reports whether it failed with an xUnit assertion exception.</summary>
    /// <param name="assertion">The assertion to run.</param>
    /// <returns><see langword="true"/> when the assertion failed.</returns>
    public static bool Fails(Action assertion)
    {
        try
        {
            assertion();
            return false;
        }
        catch (Xunit.Sdk.XunitException)
        {
            return true;
        }
    }

    /// <summary>Runs an assertion and returns the message of the xUnit assertion exception it throws.</summary>
    /// <param name="assertion">The assertion to run.</param>
    /// <returns>The failure message, or <see langword="null"/> when the assertion passed.</returns>
    public static string? FailureMessage(Action assertion)
    {
        try
        {
            assertion();
            return null;
        }
        catch (Xunit.Sdk.XunitException exception)
        {
            return exception.Message;
        }
    }
}
