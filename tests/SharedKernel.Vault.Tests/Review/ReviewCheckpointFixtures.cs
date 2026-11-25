using SharedKernel.Vault.Formats;
using SharedKernel.Vault.Model;
using SharedKernel.Vault.Pipeline;

namespace SharedKernel.Vault.Tests.Review;

/// <summary>Provides scanned synthetic vault data and a host-free pipeline for checkpoint tests.</summary>
internal static class ReviewCheckpointFixtures
{
    /// <summary>First source whose password can be selected.</summary>
    public const string KeeperKey = "keeper|keeper.json|one";

    /// <summary>First source's synthetic password.</summary>
    public const string KeeperPassword = "fixture-first-password";

    /// <summary>Creates a scanned pair with no timestamp preference and a conflicting password.</summary>
    public static ScannedVault ConflictingLogins()
    {
        VaultItem keeper = Login("keeper", "keeper.json", "one", KeeperPassword);
        VaultItem bitwarden = Login("bitwarden", "bitwarden.json", "two", "fixture-second-password");
        return VaultPipeline.Scan(new LoadedVault(
            ["keeper.json", "bitwarden.json"],
            [new SourceItem(keeper, KeeperKey, "keeper.json"), new SourceItem(bitwarden, "bitwarden|bitwarden.json|two", "bitwarden.json")],
            []));
    }

    /// <summary>Creates a pipeline with no terminal, input reads, or password prompts.</summary>
    public static VaultPipeline CreatePipeline() =>
        new(new UnusedReader(), new PreviewWriter(), new VaultPasswordPrompts(UnexpectedPrompt, UnexpectedPrompt), TimeProvider.System);

    private static VaultItem Login(string provider, string sourceFile, string id, string password) => new()
    {
        Name = "Example account",
        Type = VaultItemType.Login,
        Origin = new ItemOrigin(provider, sourceFile, id, null),
        Login = new LoginData { Username = "alice", Password = password, Uris = [new LoginUri("https://example.test")] }
    };

    private static string UnexpectedPrompt(string _) => throw new InvalidOperationException("The review should not prompt.");

    private sealed class UnusedReader : IVaultExportReader
    {
        /// <summary>Fails when the test tries to read a previously scanned input.</summary>
        /// <exception cref="InvalidOperationException">The review should use the scanned input.</exception>
        public VaultReadResult Read(string path, Func<string, string> passwordProvider) =>
            throw new InvalidOperationException("The review should use the scanned input.");
    }

    private sealed class PreviewWriter : IVaultExportWriter
    {
        /// <inheritdoc />
        public VaultWriteResult Write(VaultExport vault) => new("{}", [], []);

        /// <summary>Fails when the preview tries to encrypt an export.</summary>
        /// <exception cref="InvalidOperationException">The test pipeline is configured for previews.</exception>
        public string Encrypt(string json, string password) => throw new InvalidOperationException("The review should not encrypt a preview.");
    }
}
