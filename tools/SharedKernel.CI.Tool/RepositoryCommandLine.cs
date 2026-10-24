using System.CommandLine;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using PandaBear.Tooling;

namespace PandaBear.CI.Tool;

/// <summary>Builds the <c>sharedkernel-ci</c> command tree.</summary>
internal static partial class RepositoryCommandLine
{
    /// <summary>Exit code for a failed selection, validation, staging, or commit message check.</summary>
    private const int FailureExitCode = 2;

    /// <summary>Creates the command tree with the Git, network, and clock access that the tool uses.</summary>
    /// <param name="workingDirectory">Directory that relative <c>--root</c>, <c>--event</c>, and <c>--file</c> paths resolve from.</param>
    /// <param name="run">Runs a child process and returns its standard output.</param>
    public static RootCommand Create(string workingDirectory, Func<string, string[], string, CancellationToken, Task<string>> run) =>
        Create(workingDirectory, run, CandidateStagingServices.Default);

    /// <summary>Creates the command tree.</summary>
    /// <param name="workingDirectory">Directory that relative <c>--root</c>, <c>--event</c>, and <c>--file</c> paths resolve from.</param>
    /// <param name="run">Runs a child process and returns its standard output.</param>
    /// <param name="staging">Supplies what <c>stage-candidate</c> uses to reach Git, the network, and the clock.</param>
    public static RootCommand Create(
        string workingDirectory, Func<string, string[], string, CancellationToken, Task<string>> run, CandidateStagingServices staging)
    {
        var root = new RootCommand(
            "Selects, builds, and tests affected test projects, stages package candidates, and verifies commit messages.");

        // Consumer caller tests parse command lines with [diagram] without running them.
        root.Directives.Add(new DiagramDirective());
        root.Subcommands.Add(CreateSelect(workingDirectory, run));
        root.Subcommands.Add(CreateValidate(workingDirectory, run));
        root.Subcommands.Add(CreateStageCandidate(workingDirectory, staging));
        root.Subcommands.Add(CreateVerifyCommitMessage(workingDirectory));
        return root;
    }

    private static Command CreateSelect(string workingDirectory, Func<string, string[], string, CancellationToken, Task<string>> run)
    {
        var baseOption = new Option<string>("--base")
        {
            Description = "Git object ID of the revision to compare with HEAD. Without one, an invalid one, or one that is not an ancestor of HEAD, every test project is selected.",
            HelpName = "object-id",
        };
        var githubOutputOption = PathOption(
            "--github-output",
            "GitHub Actions output file that receives build_required, documentation_only, matrix, and <slice>_required. Relative to the root.",
            "path");
        var rootOption = CreateRootOption();
        var selectionOption = CreateSelectionOption();
        var command = new Command(
            "select", "Select the test projects affected since a base revision, as .config/sharedkernel-ci.json configures, and write the validation plan.")
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
        var sliceOption = new Option<string>("--slice")
        {
            Description = "Slice whose solution filter is restored, built, and tested.",
            HelpName = "name",
            Required = true,
        };
        sliceOption.Validators.Add(static result =>
        {
            if (!CiConfigurationFile.IsSliceName(result.GetValueOrDefault<string>() ?? string.Empty))
            {
                result.AddError("--slice requires a slice name: lowercase letters and digits in words separated by '-'.");
            }
        });
        var rootOption = CreateRootOption();
        var selectionOption = CreateSelectionOption();
        var command = new Command("validate", "Restore, build, and test the selected test projects of one slice of the validation plan.")
        {
            sliceOption,
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
                    parseResult.GetRequiredValue(sliceOption),
                    run,
                    output,
                    error,
                    ct);
            },
            ct));
        return command;
    }

    private static Command CreateStageCandidate(string workingDirectory, CandidateStagingServices services)
    {
        var pinOption = PathOption(
            "--pin", "Candidate manifest that pins the library revision, the package version, and the package hashes. Relative to the root.", "file");
        pinOption.Required = true;
        var libraryRootOption = PathOption(
            "--library-root", "Clean SharedKernel checkout, at its top level, of the pinned revision. Relative to the root.", "path");
        libraryRootOption.Required = true;
        var outputOption = PathOption("--output", "Local feed that receives the packages and candidate.json. Relative to the root.", "directory");
        outputOption.Required = true;
        var repositoryOption = new Option<string>("--repository")
        {
            Description = "GitHub repository whose v<version> release supplies the packages.",
            HelpName = "owner/name",
        };

        // The value becomes part of an https://github.com/ address, so it may not add path segments or a host.
        repositoryOption.Validators.Add(static result =>
        {
            var repository = result.GetValueOrDefault<string>() ?? string.Empty;
            if (!Repository().IsMatch(repository) || repository.Split('/')[1] is "." or "..")
            {
                result.AddError(
                    "--repository requires owner/name: letters, digits, and '-' in the owner, also '.' and '_' in the name, which cannot be '.' or '..'.");
            }
        });
        var feedOption = PathOption("--feed", "Directory that holds the packages. Relative to the root.", "directory");
        var rootOption = CreateRootOption();
        var command = new Command(
            "stage-candidate",
            "Verify a pinned candidate against a clean library checkout, then stage its packages from a release or a directory in a local feed.")
        {
            pinOption,
            libraryRootOption,
            outputOption,
            repositoryOption,
            feedOption,
            rootOption,
        };
        command.Validators.Add(result =>
        {
            var hasRepository = result.GetResult(repositoryOption) is not null;
            if (hasRepository == (result.GetResult(feedOption) is not null))
            {
                result.AddError(hasRepository
                    ? $"Options '{repositoryOption.Name}' and '{feedOption.Name}' cannot be combined."
                    : $"Option '{repositoryOption.Name}' or '{feedOption.Name}' is required.");
            }
        });
        command.SetAction((parseResult, ct) => Execute(
            parseResult,
            (output, _) =>
            {
                var root = Path.GetFullPath(parseResult.GetValue(rootOption) ?? ".", workingDirectory);
                var feed = parseResult.GetValue(feedOption);
                return CandidateStaging.Run(
                    new CandidateStagingRequest(
                        root,
                        Path.GetFullPath(parseResult.GetRequiredValue(pinOption), root),
                        Path.GetFullPath(parseResult.GetRequiredValue(libraryRootOption), root),
                        Path.GetFullPath(parseResult.GetRequiredValue(outputOption), root),
                        parseResult.GetValue(repositoryOption),
                        feed is null ? null : Path.GetFullPath(feed, root)),
                    services,
                    output,
                    ct);
            },
            ct));
        return command;
    }

    private static Command CreateVerifyCommitMessage(string workingDirectory)
    {
        var eventOption = PathOption(
            "--event",
            "GitHub pull_request event JSON file whose title is the header and whose body holds the footers. Specify this or --file.",
            "path");
        var fileOption = PathOption(
            "--file",
            "Git commit message file, such as the one a commit-msg hook receives. Specify this or --event.",
            "path");
        var command = new Command(
            "verify-commit-message",
            "Verify that a pull request title or commit message header is a Conventional Commit header and that breaking-change footers have a description.")
        {
            eventOption,
            fileOption,
        };
        command.Validators.Add(result =>
        {
            var hasEvent = result.GetResult(eventOption) is not null;
            if (hasEvent == (result.GetResult(fileOption) is not null))
            {
                result.AddError(hasEvent
                    ? $"Options '{eventOption.Name}' and '{fileOption.Name}' cannot be combined."
                    : $"Option '{eventOption.Name}' or '{fileOption.Name}' is required.");
            }
        });
        command.SetAction((parseResult, ct) => Execute(
            parseResult,
            (_, _) => parseResult.GetValue(eventOption) is { } eventPath
                ? CommitMessageVerification.VerifyEvent(Path.GetFullPath(eventPath, workingDirectory), ct)
                : CommitMessageVerification.VerifyFile(Path.GetFullPath(parseResult.GetRequiredValue(fileOption), workingDirectory), ct),
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
        catch (Exception exception) when (exception is ArgumentException
            or IOException
            or InvalidOperationException
            or XmlException
            or JsonException
            or KeyNotFoundException
            or InvalidDataException
            or UnauthorizedAccessException
            or TimeoutException
            or HttpRequestException)
        {
            // Messages carry text from files, processes, and the network. Each line is escaped on its own, so process
            // output keeps its lines; messages escape the pin, global.json, and release values they embed, so such a
            // value cannot start a line.
            foreach (var line in exception.Message.TrimEnd('\r', '\n').Split('\n'))
            {
                await error.WriteLineAsync(ControlCharacters.Escape(line.TrimEnd('\r'))).ConfigureAwait(false);
            }

            return FailureExitCode;
        }
    }

    [GeneratedRegex(@"\A[A-Za-z0-9-]+/[A-Za-z0-9._-]+\z", RegexOptions.CultureInvariant)]
    private static partial Regex Repository();
}
