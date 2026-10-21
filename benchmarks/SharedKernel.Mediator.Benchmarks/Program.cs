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
    public static async Task<int> Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--analyzer-regression")
        {
            if (args.Length != 3)
            {
                await Console.Error.WriteLineAsync("Use --analyzer-regression collect|check <baseline.json>.").ConfigureAwait(false);
                return 2;
            }

            return await AnalyzerRegressionCommand.Run(args[1], args[2]).ConfigureAwait(false);
        }

        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
        return 0;
    }
}
