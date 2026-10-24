using System.Text.Json;
using System.Text.Unicode;

namespace PandaBear.RepoConfig.Tool;

/// <summary>
/// Verifies that license exceptions still match the reviewed approval values and the locked package versions.
/// </summary>
internal static class LicenseExceptionVerifier
{
    private const int MaxApprovalBytes = 1024 * 1024;

    // Approval files are strict JSON; a repeated property would leave the approved value ambiguous.
    private static readonly JsonDocumentOptions ApprovalOptions = new() { AllowDuplicateProperties = false };

    /// <summary>
    /// Reports each approval value that differs from the approval file, each approved package that a tracked
    /// <c>packages.lock.json</c> resolves to another version, and each approved package that no tracked lock file
    /// resolves.
    /// </summary>
    /// <exception cref="InvalidOperationException">Git cannot list the index.</exception>
    /// <exception cref="TimeoutException">Git does not list the index in time.</exception>
    public static async Task<IReadOnlyList<RepoConfigIssue>> Verify(
        string rootPath,
        LicenseExceptionConfiguration licenseExceptions,
        CancellationToken cancellationToken)
    {
        var root = Path.GetFullPath(rootPath);
        List<RepoConfigIssue> issues = [];
        await VerifyApproval(root, licenseExceptions.Approval, issues, cancellationToken).ConfigureAwait(false);
        await VerifyLockedVersions(root, licenseExceptions.Packages, issues, cancellationToken).ConfigureAwait(false);
        return issues;
    }

    private static async Task VerifyApproval(
        string root,
        LicenseApprovalConfiguration approval,
        List<RepoConfigIssue> issues,
        CancellationToken cancellationToken)
    {
        byte[]? content;
        try
        {
            content = await RepositoryFile.Read(RepositoryPath.Resolve(root, approval.File), MaxApprovalBytes, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException exception)
        {
            issues.Add(new RepoConfigIssue(approval.File, exception.Message));
            return;
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            issues.Add(new RepoConfigIssue(approval.File, "Does not exist."));
            return;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            issues.Add(new RepoConfigIssue(approval.File, "Cannot be read."));
            return;
        }

        if (content is null)
        {
            issues.Add(new RepoConfigIssue(approval.File, "Exceeds the 1 MiB approval file size limit."));
            return;
        }

        using var document = Parse(approval.File, RepositoryFile.WithoutUtf8ByteOrderMark(content), ApprovalOptions, issues);
        if (document is null)
        {
            return;
        }

        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new RepoConfigIssue(approval.File, "Is not a JSON object."));
            return;
        }

        foreach (var (name, expected) in approval.Values)
        {
            JsonElement? actual = document.RootElement.TryGetProperty(name, out var value) ? value : null;
            if (actual is not { } current || !Matches(current, expected))
            {
                issues.Add(new RepoConfigIssue(
                    approval.File,
                    $"{name} is {Describe(actual)}, not the approved {Describe(expected)}; re-review the license exceptions."));
            }
        }
    }

    private static async Task VerifyLockedVersions(
        string root,
        OrderedDictionary<string, string> packages,
        List<RepoConfigIssue> issues,
        CancellationToken cancellationToken)
    {
        // NuGet package IDs are case-insensitive.
        Dictionary<string, string> approved = new(packages, StringComparer.OrdinalIgnoreCase);
        HashSet<string> resolved = new(StringComparer.OrdinalIgnoreCase);
        var lockFiles = await GitTrackedFiles.Read(root, ["packages.lock.json", "*/packages.lock.json"], cancellationToken).ConfigureAwait(false);
        if (lockFiles.Count == 0)
        {
            issues.Add(new RepoConfigIssue(".", "Git tracks no packages.lock.json file."));
        }

        foreach (var lockFile in lockFiles)
        {
            var (content, failure) = await JsonVerifier.ReadTrackedFile(root, lockFile, cancellationToken).ConfigureAwait(false);
            if (failure is not null)
            {
                issues.Add(new RepoConfigIssue(lockFile, failure));
                continue;
            }

            using var document = Parse(lockFile, RepositoryFile.WithoutUtf8ByteOrderMark(content!), default, issues);
            if (document is not null)
            {
                VerifyLockFile(lockFile, document.RootElement, approved, resolved, issues);
            }
        }

        foreach (var (packageId, version) in packages)
        {
            if (!resolved.Contains(packageId))
            {
                issues.Add(new RepoConfigIssue(
                    ConsumerConfigurationFile.RelativePath,
                    $"{packageId} {version} is approved, but no tracked packages.lock.json resolves it; remove its license exception."));
            }
        }
    }

    private static void VerifyLockFile(
        string lockFile,
        JsonElement lockRoot,
        Dictionary<string, string> approved,
        HashSet<string> resolved,
        List<RepoConfigIssue> issues)
    {
        if (lockRoot.ValueKind != JsonValueKind.Object
            || !lockRoot.TryGetProperty("dependencies", out var targets)
            || targets.ValueKind != JsonValueKind.Object
            || targets.EnumerateObject().Any(static target => target.Value.ValueKind != JsonValueKind.Object))
        {
            issues.Add(new RepoConfigIssue(lockFile, "Is not a NuGet lock file."));
            return;
        }

        // Every target framework and runtime counts, and a package listed twice in one target is checked twice.
        foreach (var target in targets.EnumerateObject())
        {
            foreach (var package in target.Value.EnumerateObject())
            {
                if (!approved.TryGetValue(package.Name, out var version))
                {
                    continue;
                }

                resolved.Add(package.Name);
                var lockedVersion = package.Value.ValueKind == JsonValueKind.Object
                    && package.Value.TryGetProperty("resolved", out var resolvedVersion)
                    && resolvedVersion.ValueKind == JsonValueKind.String
                        ? resolvedVersion.GetString()
                        : null;
                if (!string.Equals(lockedVersion, version, StringComparison.Ordinal))
                {
                    issues.Add(new RepoConfigIssue(
                        lockFile,
                        lockedVersion is null
                            ? $"{package.Name} has no resolved version for {target.Name}, not the approved {version}."
                            : $"{package.Name} resolves to {lockedVersion} for {target.Name}, not the approved {version}."));
                }
            }
        }
    }

    private static JsonDocument? Parse(string path, ReadOnlyMemory<byte> content, JsonDocumentOptions options, List<RepoConfigIssue> issues)
    {
        // Property names and strings are read as text below, which requires valid UTF-8.
        if (!Utf8.IsValid(content.Span))
        {
            issues.Add(new RepoConfigIssue(path, JsonVerifier.DescribeInvalidUtf8(content.Span)));
            return null;
        }

        try
        {
            return JsonDocument.Parse(content, options);
        }
        catch (JsonException exception)
        {
            issues.Add(new RepoConfigIssue(path, JsonVerifier.DescribeInvalidJson(exception)));
            return null;
        }
    }

    private static bool Matches(JsonElement actual, JsonElement expected) =>
        actual.ValueKind == expected.ValueKind
        && (expected.ValueKind != JsonValueKind.String || string.Equals(actual.GetString(), expected.GetString(), StringComparison.Ordinal));

    private static string Describe(JsonElement? value) => value?.ValueKind switch
    {
        null => "missing",
        JsonValueKind.String => "\"" + value.Value.GetString() + "\"",
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        JsonValueKind.Null => "null",
        JsonValueKind.Number => "a number",
        JsonValueKind.Array => "an array",
        _ => "an object",
    };
}
