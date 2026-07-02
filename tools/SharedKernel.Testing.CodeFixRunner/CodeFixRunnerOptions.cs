extern alias testinganalyzers;

namespace SharedKernel.Testing.CodeFixRunner;

internal sealed record CodeFixRunnerOptions(string TargetPath, string DiagnosticId)
{
    public const string Usage = "Usage: dotnet run --project tools/SharedKernel.Testing.CodeFixRunner -- [--diagnostic <id>] <project-or-solution>";

    public static CodeFixRunnerOptions? Parse(string[] args)
    {
        if (args.Length == 1)
        {
            return new CodeFixRunnerOptions(
                Path.GetFullPath(args[0]),
                testinganalyzers::SharedKernel.Testing.Analyzers.TestingDiagnosticIds.XunitTestClassHelperMethod);
        }

        if (args.Length == 3 && string.Equals(args[0], "--diagnostic", StringComparison.Ordinal))
        {
            return new CodeFixRunnerOptions(Path.GetFullPath(args[2]), args[1]);
        }

        return null;
    }
}
