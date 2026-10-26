using System.CommandLine;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using PandaBear.Tooling;

namespace PandaBear.CI.Tool;

/// <summary>Builds the <c>sharedkernel-ci</c> command tree.</summary>
internal static partial class RepositoryCommandLine
{
    /// <summary>Exit code for a failed selection, run, generation, installation, coverage check, staging, commit message check, or mutation run.</summary>
    private const int FailureExitCode = 2;

    /// <summary>Creates the command tree with the Git, network, and clock access that the tool uses.</summary>
    /// <param name="workingDirectory">Directory that relative <c>--root</c>, <c>--event</c>, and <c>--file</c> paths resolve from.</param>
    /// <param name="run">Runs a child process and returns its standard output.</param>
    /// <param name="runInstaller">Runs the Playwright installer, whose output is long; none runs it with <paramref name="run"/>.</param>
    public static RootCommand Create(
        string workingDirectory,
        Func<string, string[], string, CancellationToken, Task<string>> run,
        Func<string, string[], string, CancellationToken, Task<string>>? runInstaller = null) =>
        Create(workingDirectory, run, ProcessRunner.RunConfigured, CandidateStagingServices.Default, runInstaller: runInstaller);

    /// <summary>Creates the command tree.</summary>
    /// <param name="workingDirectory">Directory that relative <c>--root</c>, <c>--event</c>, and <c>--file</c> paths resolve from.</param>
    /// <param name="run">Runs a child process and returns its standard output.</param>
    /// <param name="runConfigured">Runs a child process after its start information is adjusted, such as OpenAPI generation with its own environment.</param>
    /// <param name="staging">Supplies what <c>stage-candidate</c> uses to reach Git, the network, and the clock.</param>
    /// <param name="startService">Starts the process of a slice's service from its start information; none starts it with <see cref="Process.Start(ProcessStartInfo)"/>.</param>
    /// <param name="runInstaller">Runs the Playwright installer, whose output is long; none runs it with <paramref name="run"/>.</param>
    public static RootCommand Create(
        string workingDirectory,
        Func<string, string[], string, CancellationToken, Task<string>> run,
        Func<string, string[], string, Action<ProcessStartInfo>, CancellationToken, Task<string>> runConfigured,
        CandidateStagingServices staging,
        Func<ProcessStartInfo, Process>? startService = null,
        Func<string, string[], string, CancellationToken, Task<string>>? runInstaller = null)
    {
        runInstaller ??= run;
        var root = new RootCommand(
            "Selects and runs affected test projects, generates OpenAPI documents, installs Playwright browsers, verifies changed-code coverage, stages package candidates, verifies commit messages, and runs mutation tests.");

        // Consumer caller tests parse command lines with [diagram] without running them.
        root.Directives.Add(new DiagramDirective());
        root.Subcommands.Add(CreateSelect(workingDirectory, run));
        root.Subcommands.Add(CreateRun(workingDirectory, run, runConfigured, startService, runInstaller));
        root.Subcommands.Add(CreateOpenApi(workingDirectory, runConfigured));
        root.Subcommands.Add(CreateInstallPlaywright(workingDirectory, runInstaller));
        root.Subcommands.Add(CreateCoverage(workingDirectory, run));
        root.Subcommands.Add(CreateStageCandidate(workingDirectory, staging));
        root.Subcommands.Add(CreateVerifyCommitMessage(workingDirectory));
        root.Subcommands.Add(CreateVerifyRestoreWarnings(workingDirectory));
        root.Subcommands.Add(CreateWarningSummary(workingDirectory));
        root.Subcommands.Add(CreateWarningReport(workingDirectory));
        root.Subcommands.Add(CreateRunMutationTests(workingDirectory, run));
        return root;
    }

    private static Command CreateRunMutationTests(string workingDirectory, Func<string, string[], string, CancellationToken, Task<string>> run)
    {
        var rootOption = CreateRootOption();
        var command = new Command(
            "run-mutation-tests",
            "Run each Stryker.NET config of .config/sharedkernel-ci.json in order, with the configured dotnet-stryker installed on demand, and stop at the first failure.")
        {
            rootOption,
        };
        command.SetAction((parseResult, ct) => Execute(
            parseResult,
            async (output, _) =>
            {
                var root = Path.GetFullPath(parseResult.GetValue(rootOption) ?? ".", workingDirectory);
                var configuration = await CiConfigurationFile.Read(root, ct).ConfigureAwait(false);
                var mutation = configuration.Mutation ?? throw CiConfigurationFile.Invalid("$.mutation is required by run-mutation-tests.");
                await MutationTestRun.Run(root, mutation, run, output, ct).ConfigureAwait(false);
            },
            ct));
        return command;
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

    private static Command CreateRun(
        string workingDirectory,
        Func<string, string[], string, CancellationToken, Task<string>> run,
        Func<string, string[], string, Action<ProcessStartInfo>, CancellationToken, Task<string>> runConfigured,
        Func<ProcessStartInfo, Process>? startService,
        Func<string, string[], string, CancellationToken, Task<string>> runInstaller)
    {
        var sliceOption = new Option<string[]>("--slice")
        {
            Description = "Slice whose selected test projects run; repeat it to run several slices in one test run. Without it, every required slice runs.",
            HelpName = "name",
        };
        sliceOption.Validators.Add(static result =>
        {
            if (result.GetValueOrDefault<string[]>() is { } slices && slices.Any(static slice => !CiConfigurationFile.IsSliceName(slice)))
            {
                result.AddError("--slice requires a slice name: lowercase letters and digits in words separated by '-'.");
            }
        });
        var configurationOption = CreateConfigurationOption("Release");
        var coverageOption = new Option<bool>("--coverage")
        {
            Description = "Collect Cobertura coverage per test project and fail when a selected test project produced no report.",
        };
        var playwrightOption = new Option<bool>("--install-playwright")
        {
            Description = "Install the Playwright browsers that the selected slices configure before the tests run.",
        };
        var dependenciesOption = new Option<bool>("--playwright-dependencies")
        {
            Description = "Also install the operating system dependencies of the Playwright browsers. Requires --install-playwright.",
        };
        var excludeTraitOption = new Option<string[]>("--exclude-trait")
        {
            Description = "Leave out the tests with this xUnit trait, as name=value. Repeat for several traits.",
            HelpName = "name=value",
            AllowMultipleArgumentsPerToken = false,
        };
        excludeTraitOption.Validators.Add(static result =>
        {
            if (result.GetValueOrDefault<string[]>() is { } traits && traits.Any(static trait => !IsTraitFilter(trait)))
            {
                result.AddError("--exclude-trait requires name=value: a non-empty name and value without whitespace, and a name that does not start with '-'.");
            }
        });
        var summaryOption = PathOption("--github-summary", "GitHub Actions step summary file that receives the phases. Relative to the root.", "path");
        var rootOption = CreateRootOption();
        var selectionOption = CreateSelectionOption();
        var command = new Command(
            "run",
            "Restore, build, and test the selected test projects of the validation plan through one solution filter, after the slices' OpenAPI targets and Playwright browsers, with the slices' services running during the tests.")
        {
            sliceOption,
            configurationOption,
            coverageOption,
            playwrightOption,
            dependenciesOption,
            excludeTraitOption,
            summaryOption,
            rootOption,
            selectionOption,
        };
        command.Validators.Add(result =>
        {
            if (result.GetValue(dependenciesOption) && !result.GetValue(playwrightOption))
            {
                result.AddError("--playwright-dependencies requires --install-playwright.");
            }

            if (result.GetValue(sliceOption) is { } slices && slices.Distinct(StringComparer.Ordinal).Count() != slices.Length)
            {
                result.AddError("--slice repeats a slice.");
            }
        });
        command.SetAction((parseResult, ct) => Execute(
            parseResult,
            (output, error) =>
            {
                var root = Path.GetFullPath(parseResult.GetValue(rootOption) ?? ".", workingDirectory);
                return SliceRun.Run(
                    root,
                    Path.GetFullPath(parseResult.GetRequiredValue(selectionOption), root),
                    new SliceRunOptions(
                        parseResult.GetValue(sliceOption) ?? [],
                        parseResult.GetRequiredValue(configurationOption),
                        parseResult.GetValue(coverageOption),
                        parseResult.GetValue(playwrightOption),
                        parseResult.GetValue(dependenciesOption),
                        parseResult.GetValue(excludeTraitOption) ?? [],
                        parseResult.GetValue(summaryOption)),
                    run,
                    runConfigured,
                    output,
                    error,
                    ct,
                    startService: startService,
                    runInstaller: runInstaller);
            },
            ct));
        return command;
    }

    private static bool IsTraitFilter(string trait)
    {
        var separator = trait.IndexOf('=', StringComparison.Ordinal);
        return separator > 0
            && separator < trait.Length - 1
            && trait[0] != '-'
            && !trait.Any(char.IsWhiteSpace);
    }

    private static Command CreateOpenApi(
        string workingDirectory, Func<string, string[], string, Action<ProcessStartInfo>, CancellationToken, Task<string>> runConfigured)
    {
        var command = new Command("openapi", "Generate OpenAPI documents, or refresh their committed copies, with the targets in .config/sharedkernel-ci.json.");
        command.Subcommands.Add(CreateOpenApiAction("generate", "Build the OpenAPI targets with their generate property.", refresh: false));
        command.Subcommands.Add(CreateOpenApiAction("refresh", "Build the OpenAPI targets with their refresh property, which also updates the committed copies.", refresh: true));
        return command;

        Command CreateOpenApiAction(string name, string description, bool refresh)
        {
            var targetOption = new Option<string[]>("--target")
            {
                Description = "OpenAPI target to build; repeat it for several. Without it, every configured target is built.",
                HelpName = "name",
            };
            targetOption.Validators.Add(static result =>
            {
                if (result.GetValueOrDefault<string[]>() is { } targets && targets.Any(static target => !CiConfigurationFile.IsName(target)))
                {
                    result.AddError("--target requires a target name: lowercase letters and digits in words separated by '-'.");
                }
            });
            var rootOption = CreateRootOption();
            var action = new Command(name, description) { targetOption, rootOption };
            action.SetAction((parseResult, ct) => Execute(
                parseResult,
                async (_, error) =>
                {
                    var root = Path.GetFullPath(parseResult.GetValue(rootOption) ?? ".", workingDirectory);
                    var configuration = await CiConfigurationFile.Read(root, ct).ConfigureAwait(false);
                    await OpenApiGeneration.Run(root, configuration, parseResult.GetValue(targetOption) ?? [], refresh, null, runConfigured, error, ct).ConfigureAwait(false);
                },
                ct));
            return action;
        }
    }

    private static Command CreateInstallPlaywright(string workingDirectory, Func<string, string[], string, CancellationToken, Task<string>> run)
    {
        var projectOption = PathOption("--project", "Built project whose output holds playwright.ps1. Relative to the root.", "path");
        projectOption.Required = true;
        var browserOption = new Option<string[]>("--browser")
        {
            Description = "Browser to install, such as chromium; repeat it for several. Without it, Playwright installs its default browsers.",
            HelpName = "name",
        };
        browserOption.Validators.Add(static result =>
        {
            if (result.GetValueOrDefault<string[]>() is { } browsers && browsers.Any(static browser => !CiConfigurationFile.IsBrowser(browser)))
            {
                result.AddError("--browser requires a browser name: lowercase letters, digits, and '-', starting with a letter.");
            }
        });
        var dependenciesOption = new Option<bool>("--with-dependencies")
        {
            Description = "Also install the operating system dependencies of the browsers.",
        };
        var onlyShellOption = new Option<bool>("--only-shell")
        {
            Description = "Install only the headless shell of Chromium, not the full browser. For tests that never run headed.",
        };
        var configurationOption = CreateConfigurationOption(null);
        var rootOption = CreateRootOption();
        var command = new Command(
            "install-playwright", "Install Playwright browsers with the playwright.ps1 in the build output of a project, found through its evaluated OutputPath.")
        {
            projectOption,
            browserOption,
            dependenciesOption,
            onlyShellOption,
            configurationOption,
            rootOption,
        };
        command.SetAction((parseResult, ct) => Execute(
            parseResult,
            async (_, error) =>
            {
                var root = Path.GetFullPath(parseResult.GetValue(rootOption) ?? ".", workingDirectory);
                await error.WriteAsync(await PlaywrightInstallation.Run(
                    root,
                    parseResult.GetRequiredValue(projectOption),
                    parseResult.GetValue(browserOption) ?? [],
                    parseResult.GetValue(dependenciesOption),
                    parseResult.GetValue(configurationOption),
                    run,
                    ct,
                    onlyShell: parseResult.GetValue(onlyShellOption)).ConfigureAwait(false)).ConfigureAwait(false);
            },
            ct));
        return command;
    }

    private static Command CreateCoverage(string workingDirectory, Func<string, string[], string, CancellationToken, Task<string>> run)
    {
        var command = new Command(
            "coverage", "Record the Release symbols of a build, and verify the changed-code coverage of Cobertura reports with the coverage policy of .config/sharedkernel-ci.json.");

        var recordOutput = PathOption("--output", "Symbol evidence file to write. Relative to the root.", "file");
        recordOutput.DefaultValueFactory = static _ => SymbolEvidence.DefaultPath;
        var recordRoot = CreateRootOption();
        var record = new Command(
            "record-symbols", "Record HEAD and the SHA-256 hash of every Release portable PDB under the configured symbol directories.")
        {
            recordOutput,
            recordRoot,
        };
        record.SetAction((parseResult, ct) => Execute(
            parseResult,
            async (output, _) =>
            {
                var root = Path.GetFullPath(parseResult.GetValue(recordRoot) ?? ".", workingDirectory);
                var configuration = await CiConfigurationFile.Read(root, ct).ConfigureAwait(false);
                var coverage = configuration.Coverage ?? throw CiConfigurationFile.Invalid("$.coverage is required by the coverage commands.");
                await SymbolEvidence.Record(root, coverage, parseResult.GetRequiredValue(recordOutput), run, output, ct).ConfigureAwait(false);
            },
            ct));

        var reportOption = new Option<string[]>("--report")
        {
            Description = "Cobertura report, or a directory searched recursively for " + CoberturaReport.FilePattern + " reports; repeat it for several. Relative to the root.",
            HelpName = "path",
            Required = true,
        };
        reportOption.Validators.Add(static result =>
        {
            if (result.GetValueOrDefault<string[]>() is { } reports && reports.Any(string.IsNullOrWhiteSpace))
            {
                result.AddError("--report requires a path.");
            }
        });
        var baseOption = new Option<string>("--base")
        {
            Description = "Git object ID of the revision to compare with HEAD; the lines added in <base>...HEAD count.",
            HelpName = "object-id",
            Required = true,
        };

        var symbolsOption = PathOption("--symbols", "Symbol evidence that coverage record-symbols wrote for HEAD. Relative to the root.", "file");
        symbolsOption.Required = true;
        var verifyOutput = PathOption("--output", "Gate evidence file to write. Relative to the root.", "file");
        verifyOutput.DefaultValueFactory = static _ => CoverageGate.DefaultOutput;
        var verifyRoot = CreateRootOption();
        var verify = new Command(
            "verify",
            "Verify the symbol evidence, then require the configured share of the executable lines and conditions added since --base to be covered.")
        {
            reportOption,
            baseOption,
            symbolsOption,
            verifyOutput,
            verifyRoot,
        };
        verify.SetAction((parseResult, ct) => Execute(
            parseResult,
            (output, _) =>
            {
                var root = Path.GetFullPath(parseResult.GetValue(verifyRoot) ?? ".", workingDirectory);
                return CoverageGate.Verify(
                    root,
                    parseResult.GetRequiredValue(reportOption),
                    parseResult.GetRequiredValue(baseOption),
                    parseResult.GetRequiredValue(symbolsOption),
                    parseResult.GetRequiredValue(verifyOutput),
                    run,
                    output,
                    ct);
            },
            ct));

        command.Subcommands.Add(record);
        command.Subcommands.Add(verify);
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

    private static Command CreateVerifyRestoreWarnings(string workingDirectory)
    {
        var logOption = PathOption("--log", "Output of a dotnet restore run that reports NuGet warnings.", "path");
        logOption.Required = true;
        var summaryOption = PathOption("--summary", "GitHub Actions step summary file that receives the classification. Appended to.", "path");
        var reportOption = PathOption("--report", "File that receives the warning report for warning-summary. Replaced if it exists.", "path");
        var exceptionsOption = PathOption("--exceptions", "Warning exception ledger JSON. Findings it covers count as accepted, an expired exception is reported as blocking.", "path");
        var command = new Command(
            "verify-restore-warnings",
            "Fail when a dotnet restore log reports a high or critical NuGet vulnerability advisory; lower severities and other NuGet warnings stay advisory in the summary.")
        {
            logOption,
            summaryOption,
            reportOption,
            exceptionsOption,
        };
        command.SetAction((parseResult, ct) => Execute(
            parseResult,
            (_, _) => RestoreWarningVerification.Verify(
                Path.GetFullPath(parseResult.GetRequiredValue(logOption), workingDirectory),
                parseResult.GetValue(summaryOption) is { } summary ? Path.GetFullPath(summary, workingDirectory) : null,
                parseResult.GetValue(reportOption) is { } report ? Path.GetFullPath(report, workingDirectory) : null,
                parseResult.GetValue(exceptionsOption) is { } exceptions ? Path.GetFullPath(exceptions, workingDirectory) : null,
                ct),
            ct));
        return command;
    }

    private static Command CreateWarningReport(string workingDirectory)
    {
        var toolOption = new Option<string>("--tool") { Description = "Name of the producing tool, such as 'Markdown lint'.", HelpName = "name", Required = true };
        var outcomeOption = new Option<string>("--outcome")
        {
            Description = "Outcome of the step: success or failure. Pass steps.<id>.outcome; a skipped or cancelled step fails instead of reading as a pass.",
            HelpName = "outcome",
            Required = true,
        };
        var advisoryOption = new Option<int>("--advisory")
        {
            Description = "Number of advisory findings the step reported.",
            HelpName = "count",
            DefaultValueFactory = _ => 0,
        };
        advisoryOption.Validators.Add(static result =>
        {
            if (result.GetValueOrDefault<int>() < 0)
            {
                result.AddError("Option '--advisory' cannot be negative.");
            }
        });
        var reportOption = PathOption("--report", "File that receives the warning report for warning-summary. Replaced if it exists.", "path");
        reportOption.Required = true;
        var command = new Command(
            "warning-report",
            "Write the warning report of a step without a log to classify, such as a linter or a test run, for warning-summary.")
        {
            toolOption,
            outcomeOption,
            advisoryOption,
            reportOption,
        };
        command.SetAction((parseResult, ct) => Execute(
            parseResult,
            (_, _) => WarningProducer.Write(
                parseResult.GetRequiredValue(toolOption),
                parseResult.GetRequiredValue(outcomeOption),
                parseResult.GetValue(advisoryOption),
                Path.GetFullPath(parseResult.GetRequiredValue(reportOption), workingDirectory),
                ct),
            ct));
        return command;
    }

    private static Command CreateWarningSummary(string workingDirectory)
    {
        var reportOption = new Option<string[]>("--report")
        {
            Description = "Warning report JSON file of one tool, such as the one verify-restore-warnings --report writes. Repeat for several tools.",
            HelpName = "path",
            Required = true,
            AllowMultipleArgumentsPerToken = false,
        };
        var summaryOption = PathOption("--summary", "GitHub Actions step summary file that receives the table. Appended to; standard output without it.", "path");
        var command = new Command(
            "warning-summary",
            "Render the warning reports of several CI tools as one table of blocking, advisory, and accepted totals, with the blocking findings.")
        {
            reportOption,
            summaryOption,
        };
        command.SetAction((parseResult, ct) => Execute(
            parseResult,
            (output, _) => WarningSummary.Write(
                [.. parseResult.GetRequiredValue(reportOption).Select(path => Path.GetFullPath(path, workingDirectory))],
                parseResult.GetValue(summaryOption) is { } summary ? Path.GetFullPath(summary, workingDirectory) : null,
                output,
                ct),
            ct));
        return command;
    }

    private static Option<string> CreateConfigurationOption(string? defaultValue)
    {
        var option = new Option<string>("--configuration")
        {
            Description = defaultValue is null ? "Build configuration. Defaults to the project's own." : "Build configuration.",
            HelpName = "name",
        };
        if (defaultValue is not null)
        {
            option.DefaultValueFactory = _ => defaultValue;
        }

        // The value reaches dotnet as -c <name> or -p:Configuration=<name>, so it cannot carry an option or another property.
        option.Validators.Add(static result =>
        {
            if (!ConfigurationName().IsMatch(result.GetValueOrDefault<string>() ?? string.Empty))
            {
                result.AddError("--configuration requires a configuration name: letters, digits, '_', '.', and '-', starting with a letter.");
            }
        });
        return option;
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

    [GeneratedRegex(@"\A[A-Za-z][A-Za-z0-9_.-]*\z", RegexOptions.CultureInvariant)]
    private static partial Regex ConfigurationName();
}
