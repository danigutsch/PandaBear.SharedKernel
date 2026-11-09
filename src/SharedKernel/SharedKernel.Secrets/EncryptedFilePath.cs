using PandaBear.BuildingBlocks;
using PandaBear.Results;

namespace PandaBear.Secrets;

/// <summary>
/// A safe repository-relative path of a SOPS-encrypted file: segments separated by <c>/</c>, none empty, <c>.</c>, or
/// <c>..</c>, without a root, a <c>\</c>, a <c>:</c>, or a control character, and a file name that matches
/// <c>*.sops.*</c> (for example <c>app.sops.yaml</c>).
/// </summary>
public sealed class EncryptedFilePath : ValueObject
{
    /// <summary>The longest accepted path.</summary>
    public const int MaxLength = 1024;

    private const string Marker = ".sops.";

    private EncryptedFilePath(string value) => Value = value;

    /// <summary>Gets the relative path with <c>/</c> separators.</summary>
    public string Value
    {
        get;
    }

    /// <summary>Gets the document format taken from the last extension, or <see langword="null"/> when it is not YAML or JSON.</summary>
    public SopsFormat? Format
    {
        get
        {
            var extension = Path.GetExtension(Value);
            return extension switch
            {
                ".yaml" or ".yml" => SopsFormat.Yaml,
                ".json" => SopsFormat.Json,
                _ => null,
            };
        }
    }

    /// <summary>Creates a path from <paramref name="value"/>, which is not changed.</summary>
    /// <param name="value">The candidate relative path.</param>
    /// <param name="field">The field name used in validation errors.</param>
    /// <returns>The path or a validation error that never echoes the input.</returns>
    public static Result<EncryptedFilePath> Create(string? value, string field = "file")
    {
        string? problem = value switch
        {
            null or "" => "File path is required.",
            { Length: > MaxLength } => $"File path cannot exceed {MaxLength} characters.",
            _ when value.Any(char.IsControl) => "File path cannot contain control characters.",
            _ when value.Contains('\\', StringComparison.Ordinal) || value.Contains(':', StringComparison.Ordinal) => "File path must use '/' separators and no ':'.",
            _ when value.Split('/').Any(static segment => segment is "" or "." or "..") => "File path must be relative and cannot have empty, '.', or '..' segments.",
            _ when !HasSopsName(value) => "File name must match '*.sops.*'.",
            _ => null,
        };
        return problem is null ? Result.Ok(new EncryptedFilePath(value!)) : Result.Invalid<EncryptedFilePath>(problem, field, problem);
    }

    /// <summary>
    /// Resolves the path under <paramref name="root"/> without following a symbolic link in any segment.
    /// </summary>
    /// <param name="root">The absolute root directory.</param>
    /// <returns>The absolute path, or an invalid result when the root is not absolute or a segment is a symbolic link.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="root"/> is <see langword="null"/>.</exception>
    public Result<string> ResolveUnder(string root)
    {
        ArgumentNullException.ThrowIfNull(root);
        if (!Path.IsPathFullyQualified(root))
        {
            return Result.Invalid<string>("Root must be an absolute path.", "root", "Root must be an absolute path.");
        }

        var current = root;
        foreach (var segment in Value.Split('/'))
        {
            current = Path.Combine(current, segment);
            if (new FileInfo(current).LinkTarget is not null || new DirectoryInfo(current).LinkTarget is not null)
            {
                return Result.Invalid<string>("A symbolic link is not followed in a file path.", "file", "A symbolic link is not followed in a file path.");
            }
        }

        return Result.Ok(current);
    }

    /// <inheritdoc />
    public override string ToString() => Value;

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    private static bool HasSopsName(string value)
    {
        var name = value[(value.LastIndexOf('/') + 1)..];
        var index = name.IndexOf(Marker, StringComparison.Ordinal);
        return index > 0 && index + Marker.Length < name.Length;
    }
}
