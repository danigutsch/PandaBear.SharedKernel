using System.CommandLine;
using System.Text.Json;

namespace PandaBear.Documentation.Tool;

/// <summary>
/// Builds the <c>sharedkernel-docs</c> command tree.
/// </summary>
internal static class DocumentationCommandLine
{
    private const int SuccessExitCode = 0;
    private const int FailureExitCode = 1;
    private const string GeneratorConfigDescription = "Generator configuration file, relative to the current directory.";

    /// <summary>
    /// Creates the root command. Configuration paths, and the paths inside each configuration file, resolve
    /// against <paramref name="rootPath" />. Commands write to the invocation's output and error writers.
    /// </summary>
    public static RootCommand Create(string rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);

        var root = new RootCommand("Generates Markdown blocks and checks documentation facts from repository configuration.");

        // Consumer caller tests parse command lines with [diagram] without running them.
        root.Directives.Add(new DiagramDirective());
        root.Subcommands.Add(CreateGenerateCommand(rootPath));
        root.Subcommands.Add(CreateVerifyCommand(rootPath));
        root.Subcommands.Add(CreateCheckCommand(rootPath));
        return root;
    }

    private static Command CreateGenerateCommand(string rootPath)
    {
        var config = CreateConfigOption(GeneratorConfigDescription);
        var command = new Command("generate", "Refresh the generated Markdown blocks declared by a generator configuration.");
        command.Options.Add(config);
        command.SetAction(parseResult => Generate(parseResult, rootPath, parseResult.GetRequiredValue(config)));
        return command;
    }

    private static Command CreateVerifyCommand(string rootPath)
    {
        var config = CreateConfigOption(GeneratorConfigDescription);
        var command = new Command("verify", "Fail when generated Markdown blocks are stale. Writes no files.");
        command.Options.Add(config);
        command.SetAction(parseResult => Verify(parseResult, rootPath, parseResult.GetRequiredValue(config)));
        return command;
    }

    private static Command CreateCheckCommand(string rootPath)
    {
        var config = CreateConfigOption("Conformance configuration file, relative to the current directory.");
        var command = new Command("check", "Check marked documentation facts against configured source files.");
        command.Options.Add(config);
        command.SetAction(parseResult => Check(parseResult, rootPath, parseResult.GetRequiredValue(config)));
        return command;
    }

    private static Option<string> CreateConfigOption(string description) => new("--config")
    {
        Description = description,
        HelpName = "path",
        Required = true,
    };

    private static int Generate(ParseResult parseResult, string rootPath, string configPath)
    {
        if (!TryRunGenerator(parseResult, rootPath, configPath, checkOnly: false, out var changedFiles))
        {
            return FailureExitCode;
        }

        var output = parseResult.InvocationConfiguration.Output;
        if (changedFiles.Count == 0)
        {
            output.WriteLine("Generated documentation is current.");
            return SuccessExitCode;
        }

        output.WriteLine("Updated generated documentation:");
        WritePaths(output, changedFiles);
        return SuccessExitCode;
    }

    private static int Verify(ParseResult parseResult, string rootPath, string configPath)
    {
        if (!TryRunGenerator(parseResult, rootPath, configPath, checkOnly: true, out var staleFiles))
        {
            return FailureExitCode;
        }

        if (staleFiles.Count == 0)
        {
            parseResult.InvocationConfiguration.Output.WriteLine("Generated documentation is current.");
            return SuccessExitCode;
        }

        var error = parseResult.InvocationConfiguration.Error;
        error.WriteLine("Generated documentation is stale:");
        WritePaths(error, staleFiles);
        error.WriteLine($"Run 'generate --config {configPath}' to refresh generated documentation.");
        return FailureExitCode;
    }

    private static int Check(ParseResult parseResult, string rootPath, string configPath)
    {
        try
        {
            DocumentationConformanceChecker.Check(rootPath, configPath);
        }
        catch (Exception exception) when (IsDocumentationFailure(exception))
        {
            parseResult.InvocationConfiguration.Error.WriteLine($"Documentation conformance failed: {exception.Message}");
            return FailureExitCode;
        }

        parseResult.InvocationConfiguration.Output.WriteLine("Documentation conformance checks passed.");
        return SuccessExitCode;
    }

    private static bool TryRunGenerator(
        ParseResult parseResult,
        string rootPath,
        string configPath,
        bool checkOnly,
        out IReadOnlyList<string> changedFiles)
    {
        try
        {
            changedFiles = DocumentationGenerator.Run(rootPath, configPath, checkOnly).ChangedFiles;
            return true;
        }
        catch (Exception exception) when (IsDocumentationFailure(exception))
        {
            parseResult.InvocationConfiguration.Error.WriteLine($"Documentation generation failed: {exception.Message}");
            changedFiles = [];
            return false;
        }
    }

    private static bool IsDocumentationFailure(Exception exception) =>
        exception is ArgumentException or IOException or JsonException or UnauthorizedAccessException or InvalidOperationException;

    private static void WritePaths(TextWriter writer, IReadOnlyList<string> paths)
    {
        foreach (var path in paths)
        {
            writer.WriteLine($"- {path}");
        }
    }
}
