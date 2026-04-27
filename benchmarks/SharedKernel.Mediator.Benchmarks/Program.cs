using BenchmarkDotNet.Running;

namespace SharedKernel.Mediator.Benchmarks;

/// <summary>
/// Runs the SharedKernel.Mediator benchmark suite.
/// </summary>
internal static class Program
{
    /// <summary>
    /// Starts the benchmark host.
    /// </summary>
    /// <param name="args">Command-line arguments forwarded to BenchmarkDotNet.</param>
    public static void Main(string[] args)
    {
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }
}
