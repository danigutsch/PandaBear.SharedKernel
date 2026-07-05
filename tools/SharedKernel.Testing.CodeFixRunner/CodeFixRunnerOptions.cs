extern alias testinganalyzers;

namespace SharedKernel.Testing.CodeFixRunner;

internal sealed record CodeFixRunnerOptions(string TargetPath, string DiagnosticId)
{
    public const string Usage = """
        Usage:
          sharedkernel-codefix --help
          sharedkernel-codefix --version
          sharedkernel-codefix [--diagnostic <id>] <project-or-solution>

        Options:
          --diagnostic <id>  Diagnostic ID to fix. Defaults to SKTEST004.
          --help, -h         Print help and exit successfully.
          --version          Print version and exit successfully.

        Exit codes:
          0   Success.
          2   Invalid command, arguments, or input.
        """;

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
