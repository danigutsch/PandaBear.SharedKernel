using System.Reflection;

namespace SharedKernel.Testing.CodeFixRunner;

internal static class ProgramEntryPoint
{
    public static async Task<int> Run(string[] args, TextWriter output, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        if (args is ["--help"] or ["-h"])
        {
            await output.WriteLineAsync(CodeFixRunnerOptions.Usage).ConfigureAwait(false);
            return 0;
        }

        if (args is ["--version"])
        {
            await output.WriteLineAsync(GetVersion()).ConfigureAwait(false);
            return 0;
        }

        var options = CodeFixRunnerOptions.Parse(args);
        if (options is null)
        {
            await error.WriteLineAsync(CodeFixRunnerOptions.Usage).ConfigureAwait(false);
            return 2;
        }

        var fixedCount = await CodeFixRunEngine.Run(options, error).ConfigureAwait(false);
        await output.WriteLineAsync($"Fixed {fixedCount} {options.DiagnosticId} diagnostic(s).").ConfigureAwait(false);
        return 0;
    }

    private static string GetVersion()
    {
        var version = typeof(ProgramEntryPoint).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        return string.IsNullOrWhiteSpace(version) ? "unknown" : version;
    }
}
