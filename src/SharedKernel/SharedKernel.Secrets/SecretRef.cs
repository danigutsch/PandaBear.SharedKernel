using System.Text;
using PandaBear.BuildingBlocks;
using PandaBear.Results;

namespace PandaBear.Secrets;

/// <summary>
/// A reference to one value inside an encrypted file: <c>&lt;file&gt;#&lt;key path&gt;</c>, for example
/// <c>secrets/app.sops.yaml#db.password</c> or <c>secrets/app.sops.yaml#servers[0].token</c>. Key names are limited to ASCII
/// letters, digits, <c>_</c>, and <c>-</c>; an index is <c>[n]</c>. The reference never holds a value.
/// </summary>
public sealed class SecretRef : ValueObject
{
    private const int MaxSegments = 32;
    private const int MaxSegmentLength = 128;
    private const int MaxIndexDigits = 6;

    private SecretRef(EncryptedFilePath file, string keyPath, string extractPath)
    {
        File = file;
        KeyPath = keyPath;
        SopsExtractPath = extractPath;
    }

    /// <summary>Gets the encrypted file.</summary>
    public EncryptedFilePath File
    {
        get;
    }

    /// <summary>Gets the key path in the form it was written, such as <c>db.password</c>.</summary>
    public string KeyPath
    {
        get;
    }

    /// <summary>Gets the key path in the form <c>sops --extract</c> takes, such as <c>["db"]["password"]</c>.</summary>
    public string SopsExtractPath
    {
        get;
    }

    /// <summary>Creates a reference from <paramref name="value"/>.</summary>
    /// <param name="value">The candidate text.</param>
    /// <param name="field">The field name used in validation errors.</param>
    /// <returns>The reference or a validation error that never echoes the input.</returns>
    public static Result<SecretRef> Create(string? value, string field = "secret")
    {
        if (string.IsNullOrEmpty(value))
        {
            return Invalid(field, "Secret reference is required.");
        }

        var separator = value.IndexOf('#', StringComparison.Ordinal);
        if (separator < 0 || value.IndexOf('#', separator + 1) >= 0)
        {
            return Invalid(field, "Secret reference must be '<file>#<key path>'.");
        }

        var file = EncryptedFilePath.Create(value[..separator], field);
        if (file.IsFailure)
        {
            return Invalid(field, file.ErrorDetails?.Detail ?? "File path is invalid.");
        }

        var keyPath = value[(separator + 1)..];
        var extract = TryExtractPath(keyPath);
        return extract is null
            ? Invalid(field, "Key path must be dot-separated names of letters, digits, '_' and '-', each optionally followed by [n] indexes.")
            : Result.Ok(new SecretRef(file.Value, keyPath, extract));
    }

    /// <inheritdoc />
    public override string ToString() => $"{File.Value}#{KeyPath}";

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return File;
        yield return KeyPath;
    }

    private static Result<SecretRef> Invalid(string field, string message) => Result.Invalid<SecretRef>(message, field, message);

    private static string? TryExtractPath(string keyPath)
    {
        var segments = keyPath.Split('.');
        if (segments.Length > MaxSegments)
        {
            return null;
        }

        StringBuilder extract = new();
        foreach (var segment in segments)
        {
            var bracket = segment.IndexOf('[', StringComparison.Ordinal);
            var name = bracket < 0 ? segment : segment[..bracket];
            if (name.Length is 0 or > MaxSegmentLength || !name.All(static c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-'))
            {
                return null;
            }

            extract.Append("[\"").Append(name).Append("\"]");
            if (bracket >= 0 && !AppendIndexes(segment.AsSpan(bracket), extract))
            {
                return null;
            }
        }

        return extract.ToString();
    }

    private static bool AppendIndexes(ReadOnlySpan<char> indexes, StringBuilder extract)
    {
        while (!indexes.IsEmpty)
        {
            var close = indexes.IndexOf(']');
            if (indexes[0] != '[' || close < 2 || close - 1 > MaxIndexDigits || indexes[1..close].ContainsAnyExceptInRange('0', '9'))
            {
                return false;
            }

            extract.Append('[').Append(indexes[1..close]).Append(']');
            indexes = indexes[(close + 1)..];
        }

        return true;
    }
}
