extern alias testinganalyzers;

using System.CommandLine;

namespace PandaBear.Testing.CodeFixRunner;

internal static class CodeFixRunnerCommandLine
{
    private const int LoadFailureExitCode = 2;

    public static RootCommand Create()
    {
        var target = new Argument<FileInfo>("project-or-solution")
        {
            Description = "The .csproj, .sln, or .slnx file to fix in place.",
        };
        target.AcceptExistingOnly();
        target.Validators.Add(static result =>
        {
            if (result.Tokens is [var token] && Path.GetExtension(token.Value) is not (".csproj" or ".sln" or ".slnx"))
            {
                result.AddError($"'{token.Value}' is not a .csproj, .sln, or .slnx file.");
            }
        });

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
            var fixedCount = await CodeFixRunEngine.Run(options, parseResult.InvocationConfiguration.Error, ct).ConfigureAwait(false);
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
}
