using System.Diagnostics;
using PandaBear.Processes;

namespace PandaBear.LoadTesting.K6;

/// <summary>Builds the start information of local k6, of k6 in a container, and of the removal of that container.</summary>
internal static class K6StartInfo
{
    /// <summary>The file in the run directory that receives the end-of-test summary.</summary>
    public const string SummaryFile = "k6-summary.json";

    private const string ContainerScripts = "/k6";

    private const string ContainerResults = "/results";

    /// <summary>The variables that local k6 keeps from the environment, which Windows needs to start a process.</summary>
    private static readonly string[] PlatformVariables = ["PATHEXT", "SystemRoot", "WINDIR"];

    /// <summary>Starts local k6 with only the platform variables, so the script receives only the <c>-e</c> values.</summary>
    public static ProcessStartInfo Local(LoadGeneratorRun run)
    {
        var startInfo = new ProcessStartInfo(run.Executable) { UseShellExecute = false, WorkingDirectory = run.Root };

        // k6 receives only the -e values, never the caller's environment and its credentials.
        startInfo.Environment.Clear();
        foreach (var name in PlatformVariables)
        {
            if (run.Environment.GetValueOrDefault(name) is { } value && !string.IsNullOrWhiteSpace(value))
            {
                startInfo.Environment[name] = value;
            }
        }

        AddK6Arguments(startInfo, Path.Combine(run.RunDirectory, SummaryFile), run, run.ScriptPath);
        return startInfo;
    }

    /// <summary>Starts k6 in the container named <paramref name="name"/>, with the scripts read-only and the run directory writable.</summary>
    /// <exception cref="InvalidOperationException">A mounted path contains <c>,</c> or <c>"</c>.</exception>
    public static ProcessStartInfo Container(LoadGeneratorRun run, LoadTestContainer container, string name)
    {
        var startInfo = new ProcessStartInfo(run.Executable) { UseShellExecute = false, WorkingDirectory = run.Root };
        ProcessEnvironment.RemoveForgeTokens(startInfo);
        foreach (var argument in new[] { "run", "--rm", "--pull=never", "--name", name })
        {
            startInfo.ArgumentList.Add(argument);
        }

        // Podman adds the host alias itself; Docker maps it to the host gateway on request.
        if (container.Engine == "docker")
        {
            startInfo.ArgumentList.Add("--add-host");
            startInfo.ArgumentList.Add(LoadTestContainer.HostAlias + ":host-gateway");
        }

        startInfo.ArgumentList.Add("--mount");
        startInfo.ArgumentList.Add("type=bind,source=" + MountSource(run.ScriptDirectory) + ",target=" + ContainerScripts + ",readonly");
        startInfo.ArgumentList.Add("--mount");
        startInfo.ArgumentList.Add("type=bind,source=" + MountSource(run.RunDirectory) + ",target=" + ContainerResults);
        startInfo.ArgumentList.Add("-w");
        startInfo.ArgumentList.Add(ContainerScripts);
        startInfo.ArgumentList.Add(container.Image);
        AddK6Arguments(startInfo, ContainerResults + "/" + SummaryFile, run, ContainerScripts + "/" + run.Script);
        return startInfo;
    }

    /// <summary>Force-removes the container named <paramref name="name"/>, with its output collected and discarded.</summary>
    public static ProcessStartInfo ContainerRemoval(LoadGeneratorRun run, string name)
    {
        var startInfo = new ProcessStartInfo(run.Executable)
        {
            UseShellExecute = false,
            WorkingDirectory = run.Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        ProcessEnvironment.RemoveForgeTokens(startInfo);
        foreach (var argument in new[] { "rm", "--force", name })
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }

    private static void AddK6Arguments(ProcessStartInfo startInfo, string summary, LoadGeneratorRun run, string script)
    {
        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("--no-usage-report");
        startInfo.ArgumentList.Add("--summary-export");
        startInfo.ArgumentList.Add(summary);
        foreach (var variable in run.Variables)
        {
            startInfo.ArgumentList.Add("-e");
            startInfo.ArgumentList.Add(variable.Key + "=" + variable.Value);
        }

        foreach (var argument in run.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.ArgumentList.Add(script);
    }

    // A mount is a comma-separated list of key=value fields, so a comma or quote in a path would add a field.
    private static string MountSource(string path) =>
        path.AsSpan().IndexOfAny(',', '"') < 0
            ? path
            : throw new InvalidOperationException($"Container mounts cannot name {path}, because it contains ',' or '\"'.");
}
