extern alias testinganalyzers;

using System.CommandLine;
using System.CommandLine.Parsing;

namespace PandaBear.Testing.CodeFixRunner;

/// <summary>Command line of the code fix runner tool.</summary>
internal static class CodeFixRunnerCommandLine
{
    private const int LoadFailureExitCode = 2;

    /// <summary>Creates the command tree.</summary>
    /// <param name="workingDirectory">The directory that a relative project or solution path resolves against.</param>
    /// <param name="run">
    /// Fixes the target, writes diagnostics to the given error writer, and returns the number of fixed diagnostics, or
    /// <see langword="null"/> when MSBuild or the target did not load.
    /// </param>
    public static RootCommand Create(string workingDirectory, Func<CodeFixRunnerOptions, TextWriter, CancellationToken, Task<int?>> run)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        ArgumentNullException.ThrowIfNull(run);

        var baseDirectory = Path.GetFullPath(workingDirectory);
        var target = new Argument<FileInfo>("project-or-solution")
        {
            Description = "The .csproj, .sln, or .slnx file to fix in place.",
            CustomParser = result => ParseTarget(result, baseDirectory),
        };

        string[] fixableDiagnosticIds = [.. CodeFixRunEngine.FixableDiagnosticIds.Order(StringComparer.Ordinal)];
        var diagnostic = new Option<string>("--diagnostic")
        {
            Description = $"The diagnostic ID to fix: {string.Join(", ", fixableDiagnosticIds)}.",
            HelpName = "id",
            DefaultValueFactory = static _ => testinganalyzers::PandaBear.Testing.Analyzers.TestingDiagnosticIds.XunitTestClassHelperMethod,
        };

        // The runner calls the code-fix provider directly, so only IDs the provider declares fixable reach it.
        diagnostic.AcceptOnlyFromAmong(fixableDiagnosticIds);

        var rootCommand = new RootCommand("Applies SharedKernel testing analyzer code fixes to a project or solution in place.");
        rootCommand.Arguments.Add(target);
        rootCommand.Options.Add(diagnostic);
        rootCommand.SetAction(async (parseResult, ct) =>
        {
            var options = new CodeFixRunnerOptions(parseResult.GetRequiredValue(target).FullName, parseResult.GetRequiredValue(diagnostic));
            int? fixedCount;
            try
            {
                fixedCount = await run(options, parseResult.InvocationConfiguration.Error, ct).ConfigureAwait(false);
            }
            catch (Exception exception) when (ct.IsCancellationRequested)
            {
                // Ctrl+C and SIGTERM sent to the process group also stop the MSBuild build host, so a canceled run can fail with
                // a lost connection instead of a cancellation. System.CommandLine exits 1 without output for a cancellation.
                throw new OperationCanceledException("The code fix run was canceled.", exception, ct);
            }

            if (fixedCount is null)
            {
                return LoadFailureExitCode;
            }

            var output = parseResult.InvocationConfiguration.Output;
            await output.WriteLineAsync($"Fixed {fixedCount} {options.DiagnosticId} diagnostic(s).").ConfigureAwait(false);
            return 0;
        });

        return rootCommand;
    }

    // A relative target resolves against the injected working directory, not the process directory. System.CommandLine
    // calls this parser only when the argument has its one token.
    private static FileInfo? ParseTarget(ArgumentResult result, string workingDirectory)
    {
        var path = result.Tokens[0].Value;
        if (Path.GetExtension(path) is not (".csproj" or ".sln" or ".slnx"))
        {
            result.AddError($"'{path}' is not a .csproj, .sln, or .slnx file.");
            return null;
        }

        // A response file can supply a null character, which Path.GetFullPath rejects with an exception that would escape
        // parsing.
        var target = path.Contains('\0', StringComparison.Ordinal) ? null : new FileInfo(Path.GetFullPath(path, workingDirectory));
        if (target is not { Exists: true })
        {
            result.AddError($"File does not exist: '{path}'.");
            return null;
        }

        return target;
    }
}
