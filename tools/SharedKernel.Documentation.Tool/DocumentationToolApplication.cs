using System.Text.Json;

namespace SharedKernel.Documentation.Tool;

internal static class DocumentationToolApplication
{
    private const string Usage = "Usage: sharedkernel-docs generate --config <path> [--check]";

    public static async Task<int> Run(string[] args, TextWriter output, TextWriter error, string rootPath)
    {
        if (args is ["generate", .. var generateArgs])
        {
            return await RunGenerate(generateArgs, output, error, rootPath).ConfigureAwait(false);
        }

        if (args is ["check", .. var checkArgs])
        {
            return await RunCheck(checkArgs, output, error, rootPath).ConfigureAwait(false);
        }

        await WriteUsageError(error, args.Length == 0 ? "Missing command." : $"Unknown command: {args[0]}").ConfigureAwait(false);
        return 1;
    }

    private static async Task<int> RunCheck(string[] args, TextWriter output, TextWriter error, string rootPath)
    {
        if (!TryReadConfigPath(args, out var configPath, out var configError))
        {
            await WriteUsageError(error, configError).ConfigureAwait(false);
            return 1;
        }

        try
        {
            DocumentationConformanceChecker.Check(rootPath, configPath);
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or JsonException or UnauthorizedAccessException or InvalidOperationException)
        {
            await error.WriteLineAsync($"Documentation conformance failed: {exception.Message}").ConfigureAwait(false);
            return 1;
        }

        await output.WriteLineAsync("Documentation conformance checks passed.").ConfigureAwait(false);
        return 0;
    }

    private static async Task<int> RunGenerate(string[] args, TextWriter output, TextWriter error, string rootPath)
    {
        var configPath = string.Empty;
        var checkOnly = false;

        var index = 0;
        while (index < args.Length)
        {
            var arg = args[index];
            switch (arg)
            {
                case "--check":
                    checkOnly = true;
                    index++;
                    continue;

                case "--config" when HasConfigValue(args, index):
                    configPath = args[index + 1];
                    index += 2;
                    continue;

                case "--config":
                    await WriteUsageError(error, "Missing required value for --config.").ConfigureAwait(false);
                    return 1;

                default:
                    await WriteUsageError(error, $"Unknown argument: {arg}").ConfigureAwait(false);
                    return 1;
            }
        }

        if (string.IsNullOrWhiteSpace(configPath))
        {
            await WriteUsageError(error, "Missing required --config <path>.").ConfigureAwait(false);
            return 1;
        }

        DocumentationGenerationResult result;
        try
        {
            result = DocumentationGenerator.Run(rootPath, configPath, checkOnly);
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or JsonException or UnauthorizedAccessException or InvalidOperationException)
        {
            await error.WriteLineAsync($"Documentation generation failed: {exception.Message}").ConfigureAwait(false);
            return 1;
        }

        if (checkOnly && result.ChangedFiles.Count > 0)
        {
            await error.WriteLineAsync("Generated documentation is stale:").ConfigureAwait(false);
            foreach (var path in result.ChangedFiles)
            {
                await error.WriteLineAsync($"- {path}").ConfigureAwait(false);
            }

            await error.WriteLineAsync("Run without --check to refresh generated documentation.").ConfigureAwait(false);
            return 1;
        }

        if (result.ChangedFiles.Count == 0)
        {
            await output.WriteLineAsync("Generated documentation is current.").ConfigureAwait(false);
            return 0;
        }

        await output.WriteLineAsync("Updated generated documentation:").ConfigureAwait(false);
        foreach (var path in result.ChangedFiles)
        {
            await output.WriteLineAsync($"- {path}").ConfigureAwait(false);
        }

        return 0;
    }

    private static bool HasConfigValue(string[] args, int index) =>
        index + 1 < args.Length && !args[index + 1].StartsWith("--", StringComparison.Ordinal);

    private static bool TryReadConfigPath(string[] args, out string configPath, out string error)
    {
        configPath = string.Empty;
        error = string.Empty;
        if (args.Length == 0)
        {
            error = "Missing required --config <path>.";
            return false;
        }

        if (args[0] != "--config")
        {
            error = $"Unknown argument: {args[0]}";
            return false;
        }

        if (args.Length == 1 || args[1].StartsWith("--", StringComparison.Ordinal))
        {
            error = "Missing required value for --config.";
            return false;
        }

        if (args.Length > 2)
        {
            error = $"Unknown argument: {args[2]}";
            return false;
        }

        configPath = args[1];
        return true;
    }

    private static async Task WriteUsageError(TextWriter error, string message)
    {
        await error.WriteLineAsync(message).ConfigureAwait(false);
        await error.WriteLineAsync(Usage).ConfigureAwait(false);
        await error.WriteLineAsync("       sharedkernel-docs check --config <path>").ConfigureAwait(false);
    }
}
