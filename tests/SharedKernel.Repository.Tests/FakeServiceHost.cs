using System.Diagnostics;
using System.Globalization;
using System.Runtime.Versioning;

namespace PandaBear.Repository.Tests;

/// <summary>
/// Stands in for the processes that <c>dotnet run --project &lt;service&gt;</c> would start. Each service project gets a POSIX
/// shell script that records its process ID and its environment before it runs its body.
/// </summary>
[UnsupportedOSPlatform("windows")]
internal sealed class FakeServiceHost
{
    private readonly string directory;
    private readonly Dictionary<string, string> names = new(StringComparer.Ordinal);

    /// <summary>Creates <paramref name="directory"/>, where the scripts and their recorded process IDs and environments are written.</summary>
    public FakeServiceHost(string directory)
    {
        this.directory = directory;
        Directory.CreateDirectory(directory);
    }

    /// <summary>Gets the start information of every start, in order, before the script replaced the command.</summary>
    public List<StartedService> Starts { get; } = [];

    /// <summary>Writes the script that runs when the tool starts <paramref name="project"/>.</summary>
    public void Add(string project, string body)
    {
        var name = "service-" + names.Count.ToString(CultureInfo.InvariantCulture);
        names[project] = name;
        FakeExecutable.WriteScript(
            Path.Combine(directory, name),
            $"printf '%s' \"$$\" > {Quote(Path.Combine(directory, name + ".pid"))}\n/usr/bin/env > {Quote(Path.Combine(directory, name + ".env"))}\n{body}");
    }

    /// <summary>Returns the path of the script that stands in for <paramref name="project"/>.</summary>
    public string ScriptPath(string project) => Path.Combine(directory, names[project]);

    /// <summary>Starts the script of the service project that the start information names, in place of <c>dotnet run</c>.</summary>
    public Process Start(ProcessStartInfo startInfo)
    {
        // The arguments are run --project <project> ...
        var project = startInfo.ArgumentList[2];
        Starts.Add(new StartedService(
            startInfo.FileName,
            [.. startInfo.ArgumentList],
            startInfo.WorkingDirectory,
            startInfo.RedirectStandardInput,
            startInfo.RedirectStandardOutput,
            startInfo.RedirectStandardError,
            startInfo.UseShellExecute));
        startInfo.FileName = Path.Combine(directory, names[project]);
        startInfo.ArgumentList.Clear();
        return Process.Start(startInfo) ?? throw new InvalidOperationException("The fake service did not start.");
    }

    /// <summary>Returns the process ID that the script of <paramref name="project"/> recorded.</summary>
    public int ProcessId(string project) => int.Parse(File.ReadAllText(Path.Combine(directory, names[project] + ".pid")), CultureInfo.InvariantCulture);

    /// <summary>Reports whether the script of <paramref name="project"/> has started.</summary>
    public bool HasStarted(string project) => File.Exists(Path.Combine(directory, names[project] + ".env"));

    /// <summary>Returns the environment that the script of <paramref name="project"/> recorded.</summary>
    public Dictionary<string, string> Environment(string project) =>
        File.ReadAllLines(Path.Combine(directory, names[project] + ".env"))
            .Select(static line => line.Split('=', 2))
            .Where(static pair => pair.Length == 2)
            .ToDictionary(static pair => pair[0], static pair => pair[1], StringComparer.Ordinal);

    /// <summary>Kills the process tree of the script of <paramref name="project"/> unless it has not started or has exited.</summary>
    public void Kill(string project)
    {
        if (!HasStarted(project))
        {
            return;
        }

        try
        {
            using var process = Process.GetProcessById(ProcessId(project));
            process.Kill(entireProcessTree: true);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            // The process exited between the lookup and the kill.
        }
    }

    /// <summary>Reports whether the process of the script of <paramref name="project"/> is still running.</summary>
    public bool IsRunning(string project)
    {
        try
        {
            using var process = Process.GetProcessById(ProcessId(project));
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <summary>Writes <paramref name="value"/> to the file that <paramref name="readyVariable"/> names, atomically as the protocol requires.</summary>
    public static string Ready(string value, string readyVariable = "FAKE_READY_FILE") =>
        $"printf '%s\\n' {Quote(value)} > \"${readyVariable}.tmp\"\nmv \"${readyVariable}.tmp\" \"${readyVariable}\"\n";

    /// <summary>Blocks until the standard input reaches end-of-file.</summary>
    public const string WaitForEndOfInput = "cat > /dev/null\n";

    /// <summary>Quotes <paramref name="value"/> for a POSIX shell.</summary>
    public static string Quote(string value) => "'" + value.Replace("'", "'\\''", StringComparison.Ordinal) + "'";
}
