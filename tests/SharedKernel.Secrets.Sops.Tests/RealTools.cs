using SharedKernel.Secrets.Age;

namespace SharedKernel.Secrets.Sops.Tests;

/// <summary>Finds the real age, age-keygen, and sops programs in the absolute directories of PATH, so integration tests can skip when they are absent.</summary>
internal static class RealTools
{
    private static readonly Lazy<string?> AgePath = new(() => Find("age"));
    private static readonly Lazy<string?> AgeKeygenPath = new(() => Find("age-keygen"));
    private static readonly Lazy<string?> SopsPathLazy = new(() => Find("sops"));

    /// <summary>Gets a value indicating whether <c>age</c> and <c>age-keygen</c> are installed.</summary>
    public static bool AgeAvailable => AgePath.Value is not null && AgeKeygenPath.Value is not null;

    /// <summary>Gets a value indicating whether <c>age</c>, <c>age-keygen</c>, and <c>sops</c> are installed.</summary>
    public static bool AllAvailable => AgeAvailable && SopsPathLazy.Value is not null;

    /// <summary>Gets the age adapter over the real programs.</summary>
    public static ProcessAgeTool Age() => new(new SystemToolRunner(), AgePath.Value!, AgeKeygenPath.Value!);

    /// <summary>Gets the absolute path of sops.</summary>
    public static string? SopsPath() => SopsPathLazy.Value;

    /// <summary>Gets the sops adapter over the real program.</summary>
    public static ProcessSopsTool Sops() => new(new SystemToolRunner(), SopsPathLazy.Value!);

    /// <summary>Generates a throwaway identity file in <paramref name="directory"/> and returns it with its recipient.</summary>
    public static async Task<(AgeIdentityRef Identity, AgeRecipient Recipient)> NewKey(string directory, string name, CancellationToken cancellationToken)
    {
        var identity = AgeIdentityRef.Create(Path.Combine(directory, name)).ShouldBeSuccess();
        var recipientResult = await Age().GenerateIdentity(identity, cancellationToken);
        var recipient = recipientResult.ShouldBeSuccess();
        return (identity, recipient);
    }

    /// <summary>Encrypts <paramref name="yaml"/> to <paramref name="recipients"/> with real sops and writes the result to <paramref name="file"/>.</summary>
    public static async Task EncryptToFile(ProcessSopsTool sops, string file, AgeRecipientSet recipients, string yaml, CancellationToken cancellationToken)
    {
        var encrypted = await sops.Encrypt(SopsFormat.Yaml, recipients, System.Text.Encoding.UTF8.GetBytes(yaml), cancellationToken);
        await File.WriteAllTextAsync(file, encrypted.ShouldBeSuccess(), cancellationToken);
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
