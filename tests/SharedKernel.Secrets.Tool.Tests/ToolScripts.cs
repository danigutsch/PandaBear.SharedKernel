using static PandaBear.Secrets.Tool.Tests.SecretsToolHarness;

namespace PandaBear.Secrets.Tool.Tests;

/// <summary>Scripted programs and repositories shared by the proof and base-revision tests.</summary>
internal static class ToolScripts
{
    private static readonly string Config = TestRecipients.Config(2, TestRecipients.First, TestRecipients.Second);

    /// <summary>A fake git that answers rev-parse, ls-tree, and show from the files at the base revision, with paths relative to the working directory as git resolves them.</summary>
    public static ScriptedToolRunner Git(Dictionary<string, string> filesAtBase, int revParseExit = 0) =>
        new(request =>
        {
            var arguments = request.Arguments;
            if (arguments.Contains("rev-parse"))
            {
                return ScriptedToolRunner.Result(revParseExit, revParseExit == 0 ? "abc123\n" : string.Empty, revParseExit == 0 ? string.Empty : "fatal: not a commit");
            }

            if (arguments.Contains("ls-tree"))
            {
                var listed = arguments[^1];
                return ScriptedToolRunner.Result(0, filesAtBase.ContainsKey(listed[2..]) ? $"100644 blob 0123456789abcdef\t{listed[2..]}\0" : string.Empty);
            }

            var spec = arguments[^1];
            var file = spec[(spec.IndexOf(':', StringComparison.Ordinal) + 3)..];
            return spec.Contains(":./", StringComparison.Ordinal) && filesAtBase.TryGetValue(file, out var content) ? ScriptedToolRunner.Result(0, content) : ScriptedToolRunner.Result(128, string.Empty, "fatal: path does not exist");
        });

    /// <summary>The standard two-recipient file with the envelope of <paramref name="recipient"/> changed.</summary>
    public static string Rotated(string recipient) => TestRecipients.Yaml([TestRecipients.First, TestRecipients.Second]).Replace(AgeEnvelopes.Indented(recipient, 12), AgeEnvelopes.Indented(recipient + "x", 12), StringComparison.Ordinal);

    /// <summary>A repository with a rule for two recipients and one file encrypted to them.</summary>
    public static TemporaryDirectory ProveRepository()
    {
        var repository = CreateRepository(Config);
        WriteFile(repository.Path, "secrets/app.sops.yaml", TestRecipients.Yaml([TestRecipients.First, TestRecipients.Second]));
        return repository;
    }

    /// <summary>What real sops prints when an identity matches none of the recipients.</summary>
    public const string NoMatchingIdentityMessage = "Failed to get the data key required to decrypt the SOPS file.\n\nGroup 0: FAILED\n  age1x: FAILED\n    - | failed to create reader for decrypting sops data key with\n      | age: identity did not match any of the recipients: incorrect\n      | identity for recipient block.\n";

    /// <summary>A syntactically valid age identity file (the key is made up; no real key is involved).</summary>
    public const string IdentityText = "# created: 2026-01-01T00:00:00Z\n# public key: age1x\n" + FakeKeys.Varied + "\n";

    /// <summary>Writes an identity file in <paramref name="directory"/> and returns its path.</summary>
    public static string IdentityFile(string directory, string name, string? content = null)
    {
        var path = Path.Combine(directory, name);
        File.WriteAllText(path, content ?? IdentityText);
        return path;
    }

    /// <summary>A fake sops that decrypts only for the listed identity files and says "no matching identity" for the others.</summary>
    public static ScriptedToolRunner Decrypting(params string[] identitiesThatCanDecrypt) =>
        new(request => identitiesThatCanDecrypt.Contains(request.Environment["SOPS_AGE_KEY_FILE"])
            ? ScriptedToolRunner.Result(0, "SECRET-PLAINTEXT")
            : ScriptedToolRunner.Result(128, string.Empty, NoMatchingIdentityMessage));

    /// <summary>Creates something at <paramref name="path"/> that is not a usable age identity file: <c>empty</c>, <c>garbage</c>, <c>directory</c>, <c>ssh</c>, <c>plugin</c>, <c>truncated</c>, or <c>commentsonly</c>; anything else creates nothing.</summary>
    public static void WriteBadIdentity(string path, string kind)
    {
        switch (kind)
        {
            case "empty":
                File.WriteAllText(path, string.Empty);
                break;
            case "garbage":
                File.WriteAllText(path, "garbage\n");
                break;
            case "directory":
                Directory.CreateDirectory(path);
                break;
            case "ssh":
                File.WriteAllText(path, "-----BEGIN OPENSSH PRIVATE KEY-----\nAAAA\n-----END OPENSSH PRIVATE KEY-----\n");
                break;
            case "plugin":
                File.WriteAllText(path, "AGE-PLUGIN-YUBIKEY-1QQQQQQQQQQQ\n");
                break;
            case "truncated":
                File.WriteAllText(path, "AGE-SECRET-KEY-1QPZRY9X8GF2TVDW0S3JN54KHCE\n");
                break;
            case "commentsonly":
                File.WriteAllText(path, "# public key: age1x\n");
                break;
            default:
                break;
        }
    }
}
