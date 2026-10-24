using System.CommandLine;
using System.Text.Json;
using System.Xml;

namespace PandaBear.CI.Tool;

/// <summary>Builds the repository tool command tree.</summary>
internal static class RepositoryCommandLine
{
    /// <summary>Exit code for a failed selection, validation, or pull request title check.</summary>
    private const int FailureExitCode = 2;

    /// <summary>Creates the command tree.</summary>
    /// <param name="workingDirectory">Directory that relative <c>--root</c> and <c>--event</c> paths resolve from.</param>
    /// <param name="run">Runs a child process and returns its standard output.</param>
    public static RootCommand Create(string workingDirectory, Func<string, string[], string, CancellationToken, Task<string>> run)
    {
        var root = new RootCommand("Selects, builds, and tests affected SharedKernel projects and verifies pull request titles.");

        // Consumer caller tests parse command lines with [diagram] without running them.
        root.Directives.Add(new DiagramDirective());
        root.Subcommands.Add(CreateSelect(workingDirectory, run));
        root.Subcommands.Add(CreateValidate(workingDirectory, run));
        root.Subcommands.Add(CreateVerifyPrTitle(workingDirectory));
        return root;
    }

    private static Command CreateSelect(string workingDirectory, Func<string, string[], string, CancellationToken, Task<string>> run)
    {
        var baseOption = new Option<string>("--base")
        {
            Description = "Revision to compare with HEAD. Without it, every project is selected.",
            HelpName = "revision",
        };

        // Git reads an empty base as HEAD, which selects nothing, and a leading '-' as an option.
        baseOption.Validators.Add(static result =>
        {
            var revision = result.GetValueOrDefault<string>();
            if (string.IsNullOrWhiteSpace(revision) || revision.StartsWith('-'))
            {
                result.AddError("--base requires a revision that is not empty and does not start with '-'.");
            }
        });
        var githubOutputOption = PathOption(
            "--github-output",
            "GitHub Actions output file that receives candidate_required, hosted_required, and hosted_matrix. Relative to the root.",
            "path");
        var rootOption = CreateRootOption();
        var selectionOption = CreateSelectionOption();
        var command = new Command("select", "Select the projects affected since a base revision and write the validation plan.")
        {
            baseOption,
            githubOutputOption,
            rootOption,
            selectionOption,
        };
        command.SetAction((parseResult, ct) => Execute(
            parseResult,
            (output, error) =>
            {
                var root = Path.GetFullPath(parseResult.GetValue(rootOption) ?? ".", workingDirectory);
                return ProjectSelectionPlan.Write(
                    root,
                    Path.GetFullPath(parseResult.GetRequiredValue(selectionOption), root),
                    parseResult.GetValue(baseOption),
                    parseResult.GetValue(githubOutputOption),
                    run,
                    output,
                    error,
                    ct);
            },
            ct));
        return command;
    }

    private static Command CreateValidate(string workingDirectory, Func<string, string[], string, CancellationToken, Task<string>> run)
    {
        var testGroupOption = new Option<string>("--test-group")
        {
            Description = "Projects to validate: all, ordinary (everything except hosted Aspire test projects), or one hosted test project path.",
            HelpName = "group",
            DefaultValueFactory = static _ => "all",
        };
        var rootOption = CreateRootOption();
        var selectionOption = CreateSelectionOption();
        var command = new Command("validate", "Restore, build, and test the projects in the validation plan.")
        {
            testGroupOption,
            rootOption,
            selectionOption,
        };
        command.SetAction((parseResult, ct) => Execute(
            parseResult,
            (output, error) =>
            {
                var root = Path.GetFullPath(parseResult.GetValue(rootOption) ?? ".", workingDirectory);
                return RepositoryValidation.Run(
                    root,
                    Path.GetFullPath(parseResult.GetRequiredValue(selectionOption), root),
                    parseResult.GetRequiredValue(testGroupOption),
                    run,
                    output,
                    error,
                    ct);
            },
            ct));
        return command;
    }

    private static Command CreateVerifyPrTitle(string workingDirectory)
    {
        var eventOption = PathOption("--event", "GitHub pull_request event JSON file.", "path");
        eventOption.Required = true;
        var command = new Command(
            "verify-pr-title",
            "Verify that the pull request title is a Conventional Commit header and that breaking-change footers have a description.")
        {
            eventOption,
        };
        command.SetAction((parseResult, ct) => Execute(
            parseResult,
            (_, _) => PrTitleVerification.Verify(Path.GetFullPath(parseResult.GetRequiredValue(eventOption), workingDirectory), ct),
            ct));
        return command;
    }

    private static Option<string> CreateRootOption() => PathOption("--root", "Repository root. Defaults to the current directory.", "path");

    private static Option<string> CreateSelectionOption()
    {
        var option = PathOption("--selection", "Validation plan directory, relative to the root.", "directory");
        option.DefaultValueFactory = static _ => "TestResults/selection";
        return option;
    }

    private static Option<string> PathOption(string name, string description, string helpName)
    {
        var option = new Option<string>(name) { Description = description, HelpName = helpName };

        // An empty path resolves to the current directory or the root, which a missing shell variable should not select.
        option.Validators.Add(static result =>
        {
            if (string.IsNullOrWhiteSpace(result.GetValueOrDefault<string>()))
            {
                result.AddError($"{result.Option.Name} requires a path.");
            }
        });
        return option;
    }

    private static async Task<int> Execute(ParseResult parseResult, Func<TextWriter, TextWriter, Task> action, CancellationToken ct)
    {
        var error = parseResult.InvocationConfiguration.Error;
        try
        {
            await action(parseResult.InvocationConfiguration.Output, error).ConfigureAwait(false);
            return 0;
        }
        catch (Exception exception) when (ct.IsCancellationRequested)
        {
            // Ctrl+C and SIGTERM sent to the process group also stop the running git or dotnet process, which can fail
            // before the token is observed. System.CommandLine exits 1 without output for a cancellation.
            throw new OperationCanceledException("The command was canceled.", exception, ct);
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or InvalidOperationException or XmlException or JsonException or KeyNotFoundException)
        {
            await error.WriteLineAsync(exception.Message).ConfigureAwait(false);
            return FailureExitCode;
        }
    }
}
