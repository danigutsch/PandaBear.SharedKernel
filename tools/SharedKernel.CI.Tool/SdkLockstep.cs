using System.Text.Json;
using PandaBear.Tooling;

namespace PandaBear.CI.Tool;

/// <summary>
/// Requires the consumer and the library to pin the same .NET SDK, because the consumer builds the library's tools with
/// its own SDK.
/// </summary>
internal static class SdkLockstep
{
    private const int MaxBytes = 1024 * 1024;

    // global.json is read the way the SDK reads it, which accepts comments and trailing commas.
    private static readonly JsonDocumentOptions Options = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    /// <summary>Compares <c>sdk.version</c> and <c>sdk.rollForward</c> in both <c>global.json</c> files.</summary>
    /// <exception cref="InvalidOperationException">A value differs; the message names both files' values.</exception>
    /// <exception cref="InvalidDataException">A file is not JSON or is larger than 1 MiB.</exception>
    public static async Task Verify(string root, string libraryRoot, CancellationToken ct)
    {
        var consumerPath = Path.Combine(root, "global.json");
        var libraryPath = Path.Combine(libraryRoot, "global.json");
        var consumer = await Read(consumerPath, ct).ConfigureAwait(false);
        var library = await Read(libraryPath, ct).ConfigureAwait(false);
        if (consumer != library)
        {
            throw new InvalidOperationException(
                $"The consumer and the library must pin the same SDK: {consumerPath} has {Describe(consumer)}, but {libraryPath} has {Describe(library)}.");
        }
    }

    private static async Task<(string? Version, string? RollForward)> Read(string path, CancellationToken ct)
    {
        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"The SDK lockstep check needs {path}.");
        }

        var json = await BoundedFile.Read(path, MaxBytes, path, ct).ConfigureAwait(false);
        try
        {
            using var document = JsonDocument.Parse(json, Options);
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("sdk", out var sdk)
                && sdk.ValueKind == JsonValueKind.Object
                    ? (StringProperty(sdk, "version"), StringProperty(sdk, "rollForward"))
                    : (null, null);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"{path} is not valid JSON: {ControlCharacters.Escape(exception.Message)}", exception);
        }
    }

    private static string? StringProperty(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static string Describe((string? Version, string? RollForward) sdk) =>
        $"sdk.version {Value(sdk.Version)} and sdk.rollForward {Value(sdk.RollForward)}";

    private static string Value(string? value) => value is null ? "(not set)" : ControlCharacters.Escape(value);
}
