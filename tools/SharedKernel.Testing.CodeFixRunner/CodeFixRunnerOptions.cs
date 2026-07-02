extern alias testinganalyzers;

namespace SharedKernel.Testing.CodeFixRunner;

internal sealed record CodeFixRunnerOptions(string TargetPath, string DiagnosticId)
{
    public const string Usage = "Usage: dotnet run --project tools/SharedKernel.Testing.CodeFixRunner -- [--diagnostic <id>] <project-or-solution>";

    public static CodeFixRunnerOptions? Parse(string[] args)
    {
        return args.Length switch
        {
            1 when !args[0].StartsWith("--", StringComparison.Ordinal) => new CodeFixRunnerOptions(
                Path.GetFullPath(args[0]),
                testinganalyzers::SharedKernel.Testing.Analyzers.TestingDiagnosticIds.XunitTestClassHelperMethod),
            3 when string.Equals(args[0], "--diagnostic", StringComparison.Ordinal) => new CodeFixRunnerOptions(Path.GetFullPath(args[2]), args[1]),
            _ => null,
        };
    }
}
