namespace PandaBear.Secrets.Age.Tests;

/// <summary>Finds the real age and age-keygen programs in the absolute directories of PATH, so integration tests can skip when they are absent.</summary>
internal static class RealTools
{
    private static readonly Lazy<string?> AgePath = new(() => Find("age"));
    private static readonly Lazy<string?> AgeKeygenPath = new(() => Find("age-keygen"));

    /// <summary>Gets a value indicating whether <c>age</c> and <c>age-keygen</c> are installed.</summary>
    public static bool AgeAvailable => AgePath.Value is not null && AgeKeygenPath.Value is not null;

    /// <summary>Gets the age adapter over the real programs.</summary>
    public static ProcessAgeTool Age() => new(new SystemToolRunner(), AgePath.Value!, AgeKeygenPath.Value!);

    /// <summary>Generates a throwaway identity file in <paramref name="directory"/> and returns it with its recipient.</summary>
    public static async Task<(AgeIdentityRef Identity, AgeRecipient Recipient)> NewKey(string directory, string name, CancellationToken cancellationToken)
    {
        var identity = AgeIdentityRef.Create(Path.Combine(directory, name)).ShouldBeSuccess();
        var recipientResult = await Age().GenerateIdentity(identity, cancellationToken);
        var recipient = recipientResult.ShouldBeSuccess();
        return (identity, recipient);
    }

    private static string? Find(string command)
    {
        foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(directory, command);
            if (Path.IsPathFullyQualified(directory) && File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
