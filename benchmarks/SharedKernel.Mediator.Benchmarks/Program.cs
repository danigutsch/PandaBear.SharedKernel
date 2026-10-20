using BenchmarkDotNet.Running;

namespace PandaBear.Mediator.Benchmarks;

/// <summary>
/// Runs the PandaBear.Mediator benchmark suite.
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
