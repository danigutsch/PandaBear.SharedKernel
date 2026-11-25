extern alias testinganalyzers;

using System.Collections.Immutable;
using SharedKernel.CodeFixes.MSBuild;
using SharedKernel.Testing.CodeFixes;

namespace SharedKernel.Testing.CodeFixRunner;

/// <summary>Wires the testing analyzer and code fix provider into the workspace runner.</summary>
internal static class CodeFixRunEngine
{
    /// <summary>The runner configured for the testing analyzer and its code fixes.</summary>
    internal static MSBuildCodeFixRunner Runner { get; } =
        new(new testinganalyzers::SharedKernel.Testing.Analyzers.TestingAnalyzer(), new TestingCodeFixProvider());

    /// <summary>Diagnostic IDs the testing code fix provider can fix.</summary>
    public static ImmutableArray<string> FixableDiagnosticIds => Runner.FixableDiagnosticIds;

    /// <summary>Applies testing code fixes for the requested target and diagnostic.</summary>
    /// <returns>The number of fixed diagnostics, or <see langword="null"/> for a load failure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> or <paramref name="error"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="ct"/> was canceled.</exception>
    public static Task<int?> Run(CodeFixRunnerOptions options, TextWriter error, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(options);
        return Runner.Run(options.TargetPath, options.DiagnosticId, error, ct);
    }
}
