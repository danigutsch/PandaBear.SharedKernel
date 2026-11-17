using System.CommandLine;
using PandaBear.Secrets.Age;
using PandaBear.Secrets.Sops;

namespace PandaBear.Secrets.Tool;

/// <summary>Builds the <c>sharedkernel-secrets</c> command tree.</summary>
internal static class SecretsCommandLine
{
    private const string IdentityVariable = "SOPS_AGE_KEY_FILE";

    /// <summary>Creates the root command.</summary>
    /// <param name="environment">The working directory, program lookup, process runner, standard streams, and environment reader.</param>
    /// <exception cref="ArgumentNullException"><paramref name="environment"/> is <see langword="null"/>.</exception>
    public static RootCommand Create(SecretsEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        var rootOption = new Option<string>("--root")
        {
            Description = "Repository root directory. Defaults to the current directory.",
            HelpName = "path",
        };
        var forceOption = new Option<bool>("--force")
        {
            Description = "Make the change. Without it the command only previews.",
        };

        var keygenOutput = new Option<string>("--output", "-o")
        {
            Description = "The identity file to create. It must not exist; age-keygen writes it with mode 0600.",
            HelpName = "path",
            Required = true,
        };
        var keygen = new Command("keygen", "Create an age identity file and print the public recipient. Previews unless --force is given.")
        {
            keygenOutput,
            forceOption,
        };
        keygen.SetAction((parseResult, cancellationToken) => Repository(environment, environment.WorkingDirectory).CreateIdentity(
            new CreateIdentitySettings
            {
                IdentityPath = Path.GetFullPath(parseResult.GetValue(keygenOutput)!, environment.WorkingDirectory),
                Force = parseResult.GetValue(forceOption),
            },
            parseResult.InvocationConfiguration.Output,
            parseResult.InvocationConfiguration.Error,
            cancellationToken));

        var encryptFile = new Argument<string>("file") { Description = "The new encrypted file, relative to the root, such as secrets/app.sops.yaml. A creation rule must match it." };
        var encrypt = new Command("encrypt", "Encrypt standard input to the recipients of the matching creation rule and create the file. Previews unless --force is given; never overwrites.")
        {
            encryptFile,
            rootOption,
            forceOption,
        };
        encrypt.SetAction((parseResult, cancellationToken) => Repository(environment, Root(parseResult, rootOption, environment)).Encrypt(
            new EncryptSettings
            {
                File = parseResult.GetValue(encryptFile)!,
                Force = parseResult.GetValue(forceOption),
                OpenPlaintext = environment.OpenStandardInput,
            },
            parseResult.InvocationConfiguration.Output,
            parseResult.InvocationConfiguration.Error,
            cancellationToken));

        var decryptTarget = new Argument<string>("target") { Description = "An encrypted file, or file#key.path for one value, relative to the root." };
        var identityOption = new Option<string>("--identity")
        {
            Description = "The age identity file. Defaults to the path in SOPS_AGE_KEY_FILE.",
            HelpName = "path",
        };
        var decryptOutput = new Option<string>("--output", "-o")
        {
            Description = "Write the plaintext to this new file inside the root (mode 0600, written through a temporary file) instead of standard output. Relative to the root, no symbolic links. Needs --force.",
            HelpName = "path",
        };
        var decrypt = new Command("decrypt", "Decrypt a file or one value to standard output. Writes to a file only with --output and --force.")
        {
            decryptTarget,
            rootOption,
            identityOption,
            decryptOutput,
            forceOption,
        };
        decrypt.SetAction(async (parseResult, cancellationToken) =>
        {
            var identity = await Identity(parseResult, identityOption, environment, cancellationToken).ConfigureAwait(false);
            return identity is null
                ? SecretsExitCodes.Failure
                : await Repository(environment, Root(parseResult, rootOption, environment)).Decrypt(
                    new DecryptSettings
                    {
                        Target = parseResult.GetValue(decryptTarget)!,
                        IdentityPath = identity,
                        OutputPath = parseResult.GetValue(decryptOutput),
                        Force = parseResult.GetValue(forceOption),
                        OpenPlaintextOutput = environment.OpenStandardOutput,
                    },
                    parseResult.InvocationConfiguration.Error,
                    cancellationToken).ConfigureAwait(false);
        });

        var listFile = new Argument<string>("file") { Description = "The encrypted YAML or JSON file, relative to the root." };
        var list = new Command("list", "Print the recipients that a file is encrypted to. Reads the file's metadata; decrypts nothing.")
        {
            listFile,
            rootOption,
        };
        list.SetAction((parseResult, cancellationToken) => Repository(environment, Root(parseResult, rootOption, environment)).ListRecipients(
            parseResult.GetValue(listFile)!,
            parseResult.InvocationConfiguration.Output,
            parseResult.InvocationConfiguration.Error,
            cancellationToken));

        var checkFile = new Argument<string>("file") { Description = "The encrypted YAML or JSON file, relative to the root." };
        var proveWith = new Option<string[]>("--prove-with-identity")
        {
            Description = "An identity file that must be able to decrypt the file. A trial decryption, run by sops in an empty directory with no configuration; the plaintext is discarded. Repeatable.",
            HelpName = "path",
            AllowMultipleArgumentsPerToken = false,
        };
        var proveAbsent = new Option<string[]>("--prove-absent-identity")
        {
            Description = "An identity file that must NOT be able to decrypt the file, such as a revoked key. Only meaningful next to a --prove-with-identity that decrypts. Repeatable.",
            HelpName = "path",
            AllowMultipleArgumentsPerToken = false,
        };
        var check = new Command("check", "Check that a file's labels are consistent with the recipients of its creation rule. Labels are not proof of who can decrypt; add --prove-with-identity for that.")
        {
            checkFile,
            rootOption,
            proveWith,
            proveAbsent,
        };
        check.SetAction((parseResult, cancellationToken) => Repository(environment, Root(parseResult, rootOption, environment)).CheckRecipients(
            new CheckRecipientsSettings
            {
                File = parseResult.GetValue(checkFile)!,
                ProveWithIdentities = FullPaths(parseResult.GetValue(proveWith), environment),
                ProveAbsentIdentities = FullPaths(parseResult.GetValue(proveAbsent), environment),
            },
            parseResult.InvocationConfiguration.Output,
            parseResult.InvocationConfiguration.Error,
            cancellationToken));

        var recipients = new Command("recipients", "Inspect and check the recipients of encrypted files.")
        {
            list,
            check,
        };

        var allowUnverified = new Option<bool>("--allow-unverified-files")
        {
            Description = "RISK: do not fail because some *.sops.* files match no creation rule. Those files are listed but nobody checks who can read them.",
        };
        var baseOption = new Option<string>("--base")
        {
            Description = "A git revision (such as origin/main). A label whose enc is new or different from that revision's is reported as 'changed: needs review', because only someone holding the data key can legitimately change it.",
            HelpName = "rev",
        };
        var allowChanged = new Option<bool>("--allow-changed-recipients")
        {
            Description = "RISK: do not fail because a label's enc changed since --base. A forged label looks exactly like this; review the changes instead.",
        };
        var verify = new Command("verify", "Check that every SOPS-encrypted file is consistent with the recipients of its creation rule, with nothing in clear. Proves labels and structure, not who can decrypt. Fails when nothing is verified, an entry cannot be judged, or a file matches no rule.")
        {
            rootOption,
            allowUnverified,
            baseOption,
            allowChanged,
        };
        verify.SetAction((parseResult, cancellationToken) => Repository(environment, Root(parseResult, rootOption, environment)).Verify(
            new VerifySettings
            {
                AllowUnverifiedFiles = parseResult.GetValue(allowUnverified),
                BaseRevision = parseResult.GetValue(baseOption),
                AllowChangedRecipients = parseResult.GetValue(allowChanged),
            },
            parseResult.InvocationConfiguration.Output,
            parseResult.InvocationConfiguration.Error,
            cancellationToken));

        var root = new RootCommand("Generates age identities, encrypts and decrypts SOPS files, and checks the recipients of encrypted files against the creation rules in .config/sharedkernel-secrets.json. Secrets travel on standard input and output only.");

        // Consumer caller tests parse command lines with [diagram] without running them.
        root.Directives.Add(new DiagramDirective());
        root.Subcommands.Add(keygen);
        root.Subcommands.Add(encrypt);
        root.Subcommands.Add(decrypt);
        root.Subcommands.Add(recipients);
        root.Subcommands.Add(verify);
        return root;
    }

    private static string Root(ParseResult parseResult, Option<string> rootOption, SecretsEnvironment environment) =>
        Path.GetFullPath(parseResult.GetValue(rootOption) ?? ".", environment.WorkingDirectory);

    private static SecretsRepository Repository(SecretsEnvironment environment, string root) => new(
        root,
        new SecretsPrograms
        {
            Runner = environment.Runner,
            ExecutableResolver = environment.ExecutableResolver,
            Age = () => ProcessAgeTool.Find(environment.Runner, environment.ExecutableResolver),
            Sops = () => ProcessSopsTool.Find(environment.Runner, environment.ExecutableResolver),
            FileReadTimeout = environment.FileReadTimeout ?? TimeSpan.FromSeconds(5),
        });

    // An empty path stays empty so that the engine refuses it, instead of resolving it to the working directory.
    private static string[] FullPaths(string[]? paths, SecretsEnvironment environment) =>
        [.. (paths ?? []).Select(path => path.Length > 0 ? Path.GetFullPath(path, environment.WorkingDirectory) : path)];

    /// <summary>Returns the absolute identity path from <c>--identity</c> or <c>SOPS_AGE_KEY_FILE</c>, or writes why there is none and returns <see langword="null"/>.</summary>
    private static async Task<string?> Identity(ParseResult parseResult, Option<string> identityOption, SecretsEnvironment environment, CancellationToken cancellationToken)
    {
        var identity = parseResult.GetValue(identityOption) ?? environment.GetEnvironmentVariable(IdentityVariable);
        if (!string.IsNullOrEmpty(identity))
        {
            return Path.GetFullPath(identity, environment.WorkingDirectory);
        }

        await parseResult.InvocationConfiguration.Error.WriteLineAsync(
            $"No identity file: pass --identity <path> or set {IdentityVariable} to the path of an identity file.".AsMemory(),
            cancellationToken).ConfigureAwait(false);
        return null;
    }
}
