using System.CommandLine;

namespace SharedKernel.Secrets.Tool.Tests;

/// <summary>Runs the tool with fake programs, fake standard streams, and a temporary repository.</summary>
internal static class SecretsToolHarness
{
    /// <summary>Resolves every known program to a fake absolute path.</summary>
    public static string? ResolveAll(string command) => "/fake/" + command;

    /// <summary>Resolves no program at all.</summary>
    public static string? ResolveNone(string command) => null;

    /// <summary>Creates a repository directory, with <paramref name="configuration"/> as its configuration file when it is not <see langword="null"/>.</summary>
    public static TemporaryDirectory CreateRepository(string? configuration)
    {
        var directory = new TemporaryDirectory();
        if (configuration is not null)
        {
            Directory.CreateDirectory(Path.Combine(directory.Path, ".config"));
            File.WriteAllText(Path.Combine(directory.Path, ".config", "sharedkernel-secrets.json"), configuration);
        }

        return directory;
    }

    /// <summary>Writes a repository file, creating its directories.</summary>
    public static void WriteFile(string root, string relativePath, string content)
    {
        var path = Path.Combine(root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    /// <summary>Reads a repository file as text.</summary>
    public static Task<string> ReadText(string root, string relativePath) =>
        File.ReadAllTextAsync(Path.Combine(root, relativePath), TestContext.Current.CancellationToken);

    /// <summary>Runs <paramref name="arguments"/> in <paramref name="workingDirectory"/>, with standard streams that are not a terminal.</summary>
    public static Task<ToolRun> Run(
        string workingDirectory,
        IToolRunner runner,
        Func<string, string?> resolve,
        byte[]? standardInput,
        IReadOnlyDictionary<string, string>? variables,
        params string[] arguments) =>
        Run(workingDirectory, runner, resolve, standardInput, variables, isTerminal: false, arguments);

    /// <summary>Runs <paramref name="arguments"/> in <paramref name="workingDirectory"/> as if the standard streams were a terminal.</summary>
    public static Task<ToolRun> RunInTerminal(
        string workingDirectory,
        IToolRunner runner,
        IReadOnlyDictionary<string, string>? variables,
        params string[] arguments) =>
        Run(workingDirectory, runner, ResolveAll, null, variables, isTerminal: true, arguments);

    /// <summary>Runs <paramref name="arguments"/> as if the standard streams were a terminal, finding programs with <paramref name="resolve"/>.</summary>
    public static Task<ToolRun> RunInTerminal(
        string workingDirectory,
        IToolRunner runner,
        Func<string, string?> resolve,
        IReadOnlyDictionary<string, string>? variables,
        params string[] arguments) =>
        Run(workingDirectory, runner, resolve, null, variables, isTerminal: true, arguments);

    private static async Task<ToolRun> Run(
        string workingDirectory,
        IToolRunner runner,
        Func<string, string?> resolve,
        byte[]? standardInput,
        IReadOnlyDictionary<string, string>? variables,
        bool isTerminal,
        string[] arguments)
    {
        using var output = new InvariantStringWriter();
        using var error = new InvariantStringWriter();
        using var standardOutput = new MemoryStream();
        var input = new MemoryStream(standardInput ?? []);
        var environment = new SecretsEnvironment(
            workingDirectory,
            new ExecutableMap(resolve),
            runner,
            () => input,
            () => new NonClosingStream(standardOutput),
            name => variables is not null && variables.TryGetValue(name, out var value) ? value : null,
            () => isTerminal);
        var exitCode = await SecretsCommandLine
            .Create(environment)
            .Parse(arguments)
            .InvokeAsync(new InvocationConfiguration { Output = output, Error = error, ProcessTerminationTimeout = null }, TestContext.Current.CancellationToken);
        return new ToolRun(exitCode, output.ToString(), error.ToString(), standardOutput.ToArray());
    }

    /// <summary>Runs <paramref name="arguments"/> in a repository with default fakes.</summary>
    public static Task<ToolRun> Run(string root, params string[] arguments) =>
        Run(root, ScriptedToolRunner.Unused(), ResolveAll, null, null, arguments);

    /// <summary>Writes a file that cannot be ruled out as a sops file: <c>utf16</c>, <c>binary</c>, <c>nul</c>, <c>brokenjson</c>, or a JSON document behind a byte order mark.</summary>
    public static void WriteMystery(string path, string kind, string yamlText)
    {
        switch (kind)
        {
            case "utf16":
                File.WriteAllBytes(path, System.Text.Encoding.Unicode.GetPreamble().Concat(System.Text.Encoding.Unicode.GetBytes(yamlText)).ToArray());
                break;
            case "binary":
                File.WriteAllBytes(path, [0xff, 0xfe, 0x00, 0x80, 0xc3, 0x28]);
                break;
            case "nul":
                File.WriteAllBytes(path, [(byte)'a', 0, (byte)'b']);
                break;
            case "brokenjson":
                File.WriteAllText(path, "{\"sops\": ");
                break;
            default:
                File.WriteAllText(path, "\uFEFF" + TestRecipients.Json([TestRecipients.First]));
                break;
        }
    }
}
