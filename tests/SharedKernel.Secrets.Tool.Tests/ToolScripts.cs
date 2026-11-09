using static PandaBear.Secrets.Tool.Tests.SecretsToolHarness;

namespace PandaBear.Secrets.Tool.Tests;

/// <summary>Scripted programs and repositories shared by the proof and base-revision tests.</summary>
internal static class ToolScripts
{
    private static readonly string Config = TestRecipients.Config(2, TestRecipients.First, TestRecipients.Second);

    /// <summary>A fake git that answers rev-parse, ls-tree, and show from the files at the base revision.</summary>
    public static ScriptedToolRunner Git(Dictionary<string, string> filesAtBase, int revParseExit = 0) =>
        new(request =>
        {
            var arguments = request.Arguments;
            if (arguments.Contains("rev-parse"))
            {
                return ScriptedToolRunner.Result(revParseExit, revParseExit == 0 ? "abc123\n" : string.Empty, revParseExit == 0 ? string.Empty : "fatal: not a commit");
            }

            var path = arguments[^1];
            if (arguments.Contains("ls-tree"))
            {
                return ScriptedToolRunner.Result(0, filesAtBase.ContainsKey(path) ? path + "\0" : string.Empty);
            }

            var file = arguments[^1][(arguments[^1].IndexOf(':', StringComparison.Ordinal) + 1)..];
            return filesAtBase.TryGetValue(file, out var content) ? ScriptedToolRunner.Result(0, content) : ScriptedToolRunner.Result(128, string.Empty, "fatal: path does not exist");
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

    /// <summary>A fake sops that decrypts only for the listed identity files.</summary>
    public static ScriptedToolRunner Decrypting(params string[] identitiesThatCanDecrypt) =>
        new(request => identitiesThatCanDecrypt.Contains(request.Environment["SOPS_AGE_KEY_FILE"])
            ? ScriptedToolRunner.Result(0, "SECRET-PLAINTEXT")
            : ScriptedToolRunner.Result(2, string.Empty, "Failed to get the data key required to decrypt the SOPS file."));
}
