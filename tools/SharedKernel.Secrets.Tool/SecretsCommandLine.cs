using System.CommandLine;
using PandaBear.Tooling;

namespace PandaBear.Secrets.Tool;

/// <summary>Builds the <c>sharedkernel-secrets</c> command tree.</summary>
internal static class SecretsCommandLine
{
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
        keygen.SetAction((parseResult, cancellationToken) => Execute(
            parseResult,
            (output, _) => KeygenCommand.Run(environment, parseResult.GetValue(keygenOutput)!, parseResult.GetValue(forceOption), output, cancellationToken),
            cancellationToken));

        var encryptFile = new Argument<string>("file") { Description = "The new encrypted file, relative to the root, such as secrets/app.sops.yaml. A creation rule must match it." };
        var encrypt = new Command("encrypt", "Encrypt standard input to the recipients of the matching creation rule and create the file. Previews unless --force is given; never overwrites.")
        {
            encryptFile,
            rootOption,
            forceOption,
        };
        encrypt.SetAction((parseResult, cancellationToken) => Execute(
            parseResult,
            (output, _) => EncryptCommand.Run(environment, Root(parseResult, rootOption, environment), parseResult.GetValue(encryptFile)!, parseResult.GetValue(forceOption), output, cancellationToken),
            cancellationToken));

        var decryptTarget = new Argument<string>("target") { Description = "An encrypted file, or file#key.path for one value, relative to the root." };
        var identityOption = new Option<string>("--identity")
        {
            Description = "The age identity file. Defaults to the path in SOPS_AGE_KEY_FILE.",
            HelpName = "path",
        };
        var decryptOutput = new Option<string>("--output", "-o")
        {
            Description = "Write the plaintext to this new file (mode 0600) instead of standard output. Needs --force.",
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
        decrypt.SetAction((parseResult, cancellationToken) => Execute(
            parseResult,
            (_, _) => DecryptCommand.Run(
                environment,
                Root(parseResult, rootOption, environment),
                parseResult.GetValue(decryptTarget)!,
                parseResult.GetValue(identityOption),
                parseResult.GetValue(decryptOutput),
                parseResult.GetValue(forceOption),
                cancellationToken),
            cancellationToken));

        var listFile = new Argument<string>("file") { Description = "The encrypted YAML or JSON file, relative to the root." };
        var list = new Command("list", "Print the recipients that a file is encrypted to. Reads the file's metadata; decrypts nothing.")
        {
            listFile,
            rootOption,
        };
        list.SetAction((parseResult, cancellationToken) => Execute(
            parseResult,
            (output, _) => RecipientsCommand.List(Root(parseResult, rootOption, environment), parseResult.GetValue(listFile)!, output, cancellationToken),
            cancellationToken));

        var checkFile = new Argument<string>("file") { Description = "The encrypted YAML or JSON file, relative to the root." };
        var check = new Command("check", "Check that a file is encrypted to exactly the recipients of its creation rule.")
        {
            checkFile,
            rootOption,
        };
        check.SetAction((parseResult, cancellationToken) => Execute(
            parseResult,
            (output, _) => RecipientsCommand.Check(Root(parseResult, rootOption, environment), parseResult.GetValue(checkFile)!, output, cancellationToken),
            cancellationToken));

        var recipients = new Command("recipients", "Inspect and check the recipients of encrypted files.")
        {
            list,
            check,
        };

        var verify = new Command("verify", "Check that every *.sops.* file that a creation rule matches is encrypted to exactly that rule's recipients.")
        {
            rootOption,
        };
        verify.SetAction((parseResult, cancellationToken) => Execute(
            parseResult,
            (output, error) => VerifyCommand.Run(Root(parseResult, rootOption, environment), output, error, cancellationToken),
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

    private static async Task<int> Execute(ParseResult parseResult, Func<TextWriter, TextWriter, Task<int>> action, CancellationToken cancellationToken)
    {
        var error = parseResult.InvocationConfiguration.Error;
        try
        {
            return await action(parseResult.InvocationConfiguration.Output, error).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            foreach (var line in exception.Message.TrimEnd('\r', '\n').Split('\n'))
            {
                await error.WriteLineAsync(ControlCharacters.Escape(line.TrimEnd('\r')).AsMemory(), cancellationToken).ConfigureAwait(false);
            }

            return ExitCodes.Failure;
        }
    }
}
