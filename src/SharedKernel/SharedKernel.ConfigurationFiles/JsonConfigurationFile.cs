using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using PandaBear.IO;

namespace PandaBear.ConfigurationFiles;

/// <summary>
/// Reads a strict JSON configuration file, such as <c>.config/sharedkernel-ci.json</c>, from under a repository root.
/// The strictness comes from the caller's source-generated <see cref="JsonTypeInfo{T}"/>, whose options disallow
/// unmapped members and duplicate properties. Every error is an <see cref="InvalidOperationException"/> whose message
/// starts with the file's relative path, so a command prints it as is.
/// </summary>
public static class JsonConfigurationFile
{
    /// <summary>The largest configuration file accepted: 1 MiB.</summary>
    public const int MaxBytes = 1024 * 1024;

    private static ReadOnlySpan<byte> Utf8ByteOrderMark => [0xEF, 0xBB, 0xBF];

    /// <summary>
    /// Reads and deserializes <paramref name="relativePath"/> under <paramref name="root"/> without following symbolic
    /// links.
    /// </summary>
    /// <typeparam name="T">The configuration type.</typeparam>
    /// <param name="root">The repository root.</param>
    /// <param name="relativePath">The file, relative to <paramref name="root"/>, with <c>/</c> separators.</param>
    /// <param name="typeInfo">The source-generated metadata of <typeparamref name="T"/>.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The configuration, not yet validated beyond its JSON shape.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// The file does not exist, is reached through a symbolic link, is larger than 1 MiB, or is not a JSON object of
    /// <typeparamref name="T"/>.
    /// </exception>
    /// <exception cref="IOException">The file cannot be read.</exception>
    /// <exception cref="UnauthorizedAccessException">The path names a directory, or access is denied.</exception>
    public static async Task<T> Read<T>(string root, string relativePath, JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken)
        where T : class =>
        await ReadCore(root, relativePath, typeInfo, optional: false, cancellationToken).ConfigureAwait(false)
        ?? throw new InvalidOperationException($"{relativePath} does not exist.");

    /// <summary>
    /// Reads an optional configuration file like <see cref="Read{T}"/>, and returns <see langword="null"/> when it does not exist.
    /// </summary>
    /// <typeparam name="T">The configuration type.</typeparam>
    /// <param name="root">The repository root.</param>
    /// <param name="relativePath">The file, relative to <paramref name="root"/>, with <c>/</c> separators.</param>
    /// <param name="typeInfo">The source-generated metadata of <typeparamref name="T"/>.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The configuration, or <see langword="null"/> when the file or a parent directory does not exist.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// The file is reached through a symbolic link, is larger than 1 MiB, or is not a JSON object of <typeparamref name="T"/>.
    /// </exception>
    /// <exception cref="IOException">The file cannot be read.</exception>
    /// <exception cref="UnauthorizedAccessException">The path names a directory, or access is denied.</exception>
    public static Task<T?> ReadOptional<T>(string root, string relativePath, JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken)
        where T : class =>
        ReadCore(root, relativePath, typeInfo, optional: true, cancellationToken);

    /// <summary>
    /// Deserializes configuration bytes, for a caller that read them itself, such as from a Git revision. A leading UTF-8
    /// byte order mark is ignored.
    /// </summary>
    /// <typeparam name="T">The configuration type.</typeparam>
    /// <param name="content">The UTF-8 JSON.</param>
    /// <param name="description">Names the file at the start of each error, such as its relative path.</param>
    /// <param name="typeInfo">The source-generated metadata of <typeparamref name="T"/>.</param>
    /// <returns>The configuration.</returns>
    /// <exception cref="ArgumentException"><paramref name="description"/> is <see langword="null"/>, empty, or white-space.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="typeInfo"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// The content is not valid JSON for <typeparamref name="T"/>, or is <c>null</c>. The message names the JSON path and
    /// line of the problem, never the serializer's text, which names .NET types.
    /// </exception>
    public static T Parse<T>(ReadOnlySpan<byte> content, string description, JsonTypeInfo<T> typeInfo)
        where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(typeInfo);
        if (content.StartsWith(Utf8ByteOrderMark))
        {
            content = content[Utf8ByteOrderMark.Length..];
        }

        T? configuration;
        try
        {
            configuration = JsonSerializer.Deserialize(content, typeInfo);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{description} is not valid at {exception.Path ?? "$"} (line {(exception.LineNumber ?? 0) + 1}). Check for a misspelled, unknown, missing, or repeated property, a value of the wrong type, or broken JSON syntax."),
                exception);
        }

        return configuration ?? throw new InvalidOperationException($"{description} must contain a JSON object.");
    }

    private static async Task<T?> ReadCore<T>(string root, string relativePath, JsonTypeInfo<T> typeInfo, bool optional, CancellationToken cancellationToken)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(relativePath);
        ArgumentNullException.ThrowIfNull(typeInfo);
        string path;
        try
        {
            path = RepositoryPath.Parse(relativePath).ResolveUnder(root);
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidOperationException($"{relativePath}: {exception.Message}", exception);
        }

        byte[]? content;
        try
        {
            content = await BoundedReader.ReadFile(path, MaxBytes, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return optional ? null : throw new InvalidOperationException($"{relativePath} does not exist.", exception);
        }

        return content is null
            ? throw new InvalidOperationException($"{relativePath} is larger than 1 MiB.")
            : Parse(content, relativePath, typeInfo);
    }
}
