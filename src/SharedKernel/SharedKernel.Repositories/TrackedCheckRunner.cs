using System.Diagnostics;
using System.Text;
using System.Text.Json;
using SharedKernel.ConfigurationFiles;
using SharedKernel.InputNormalization;
using SharedKernel.IO;
using SharedKernel.Processes;

namespace SharedKernel.Repositories;

/// <summary>Runs configured checks on nonempty Git-index file selections.</summary>
public static class TrackedCheckRunner
{
    /// <summary>The owner-editable configuration that the scaffold can write.</summary>
    public const string ConfigurationPath = ".config/sharedkernel-checks.json";

    private const int OutputLimit = 8 * 1024 * 1024;
    private const int ToolUnavailable = 77;

    /// <summary>Runs each configured lane and reports checks, skips, and failures.</summary>
    /// <exception cref="ArgumentNullException">An output writer is null.</exception>
    /// <exception cref="InvalidOperationException">The configuration is too large or invalid.</exception>
    public static async Task<int> Run(string directory, string configuration, bool strict, TextWriter output, TextWriter error, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);
        var git = GitProcess.Default;
        var probe = git.CreateStartInfo(directory, ["rev-parse", "--show-toplevel"]);
        var repository = await git.Run(probe, 32 * 1024, ReadOnlyMemory<byte>.Empty, ct).ConfigureAwait(false);
        if (repository.ExitCode != 0)
        {
            await output.WriteLineAsync("FAIL: checks require a Git work tree".AsMemory(), ct).ConfigureAwait(false);
            await output.WriteLineAsync("ran 0, skipped 0".AsMemory(), ct).ConfigureAwait(false);
            return 1;
        }

        var root = Encoding.UTF8.GetString(repository.StandardOutput.Span).TrimEnd('\r', '\n');
        var path = RepositoryPath.Parse(configuration).ResolveUnder(root);
        var content = await BoundedReader.ReadFile(path, JsonConfigurationFile.MaxBytes, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The tracked-check configuration exceeds 1 MiB.");
        using var document = JsonDocument.Parse(content);
        var lanes = ReadLanes(document.RootElement);
        var process = new BoundedProcessRunner(TimeSpan.FromMinutes(30));
        var ran = 0;
        var skipped = 0;
        var failed = false;

        foreach (var lane in lanes)
        {
            var name = ControlCharacters.Escape(lane.Command[0]);
            string[] files;
            try
            {
                var matching = await GitTrackedFiles.Read(git, root, [lane.Pathspec], ct).ConfigureAwait(false);
                files = [.. matching.Where(file => PresentInCheckout(root, file))];
            }
            catch (Exception exception) when (exception is InvalidOperationException or TimeoutException && !ct.IsCancellationRequested)
            {
                await output.WriteLineAsync($"FAIL: {name} (Git index inspection failed)".AsMemory(), ct).ConfigureAwait(false);
                await error.WriteLineAsync(ControlCharacters.Escape(exception.Message).AsMemory(), ct).ConfigureAwait(false);
                failed = true;
                continue;
            }

            if (files.Length == 0)
            {
                await output.WriteLineAsync($"FAIL: {name} (no tracked files)".AsMemory(), ct).ConfigureAwait(false);
                failed = true;
                continue;
            }

            var executable = Resolve(lane.Command[0], root);
            var exitCode = executable is null
                ? ToolUnavailable
                : await RunLane(process, executable, root, lane.Command, files, output, error, ct).ConfigureAwait(false);
            switch (exitCode)
            {
                case ToolUnavailable when !strict:
                    await output.WriteLineAsync($"skip: {name} (tool unavailable)".AsMemory(), ct).ConfigureAwait(false);
                    skipped++;
                    break;
                case ToolUnavailable:
                    await output.WriteLineAsync($"FAIL: {name} (tool unavailable)".AsMemory(), ct).ConfigureAwait(false);
                    failed = true;
                    break;
                case 0:
                    await output.WriteLineAsync($"ok: {name}".AsMemory(), ct).ConfigureAwait(false);
                    ran++;
                    break;
                default:
                    await output.WriteLineAsync($"FAIL: {name} (exit {exitCode})".AsMemory(), ct).ConfigureAwait(false);
                    ran++;
                    failed = true;
                    break;
            }
        }

        if (ran == 0 && !failed)
        {
            await output.WriteLineAsync("FAIL: no checks ran".AsMemory(), ct).ConfigureAwait(false);
            failed = true;
        }

        await output.WriteLineAsync($"ran {ran}, skipped {skipped}".AsMemory(), ct).ConfigureAwait(false);
        return failed ? 1 : 0;
    }

    private static List<Lane> ReadLanes(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("lanes", out var array) || array.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("The tracked-check configuration requires a lanes array.");
        }

        var lanes = new List<Lane>(array.GetArrayLength());
        foreach (var lane in array.EnumerateArray())
        {
            if (lane.ValueKind != JsonValueKind.Object
                || !lane.TryGetProperty("pathspec", out var pathspecValue) || pathspecValue.ValueKind != JsonValueKind.String
                || pathspecValue.GetString() is not { } pathspec || string.IsNullOrWhiteSpace(pathspec) || pathspec.Contains('\0', StringComparison.Ordinal)
                || !lane.TryGetProperty("command", out var elements) || elements.ValueKind != JsonValueKind.Array || elements.GetArrayLength() == 0)
            {
                throw new InvalidOperationException($"Lane {lanes.Count + 1} needs a Git pathspec and a nonempty command array.");
            }

            var command = new List<string>(elements.GetArrayLength());
            foreach (var element in elements.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.String || element.GetString() is not { } argument || argument.Contains('\0', StringComparison.Ordinal))
                {
                    throw new InvalidOperationException($"Lane {lanes.Count + 1} command arguments must be strings without null characters.");
                }

                command.Add(argument);
            }

            if (string.IsNullOrWhiteSpace(command[0]))
            {
                throw new InvalidOperationException($"Lane {lanes.Count + 1} needs an executable name.");
            }

            lanes.Add(new Lane(pathspec, command));
        }

        return lanes;
    }

    private static string? Resolve(string command, string root)
    {
        if (!command.Contains('/', StringComparison.Ordinal) && !command.Contains('\\', StringComparison.Ordinal))
        {
            return PathExecutableResolver.Default.Find(command);
        }

        var path = Path.GetFullPath(command, root);
        return File.Exists(path) ? path : null;
    }

    private static bool PresentInCheckout(string root, string file)
    {
        var path = new FileInfo(Path.Combine(root, file));
        return path.Exists && path.LinkTarget is null;
    }

    private static async Task<int> RunLane(BoundedProcessRunner process, string executable, string root, IReadOnlyList<string> command,
        IReadOnlyList<string> files, TextWriter output, TextWriter error, CancellationToken ct)
    {
        var start = new ProcessStartInfo(executable) { WorkingDirectory = root };
        foreach (var argument in command.Skip(1))
        {
            start.ArgumentList.Add(argument);
        }

        var paths = Encoding.UTF8.GetBytes(string.Join('\0', files) + '\0');
        try
        {
            var result = await process.Capture(start, paths, OutputLimit, OutputLimit, ct).ConfigureAwait(false);
            if (!result.StandardOutput.IsEmpty)
            {
                var text = Encoding.UTF8.GetString(result.StandardOutput.Span).TrimEnd('\r', '\n');
                await output.WriteLineAsync(text.AsMemory(), ct).ConfigureAwait(false);
            }
            await error.WriteAsync(result.StandardError.AsMemory(), ct).ConfigureAwait(false);
            return result.ExitCode;
        }
        catch (Exception exception) when (exception is InvalidOperationException or TimeoutException && !ct.IsCancellationRequested)
        {
            await error.WriteLineAsync(ControlCharacters.Escape(exception.Message).AsMemory(), ct).ConfigureAwait(false);
            return 1;
        }
    }

    private sealed record Lane(string Pathspec, IReadOnlyList<string> Command);
}
