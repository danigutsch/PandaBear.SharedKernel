using System.Diagnostics;
using SharedKernel.Processes;

namespace SharedKernel.Repository.Tests;

/// <summary>Records child process requests and answers them without starting processes.</summary>
internal sealed class ProcessLog(Func<string, string[], CancellationToken, Task<string>> respond)
{
    /// <summary>Answers <c>git rev-parse</c> with <c>fixture</c> and every other request with no output.</summary>
    public ProcessLog()
        : this(static (fileName, arguments, _) => Task.FromResult(fileName == "git" && arguments is ["rev-parse", ..] ? "fixture" : string.Empty))
    {
    }

    /// <summary>Gets every request that reached the log, in order.</summary>
    public List<ProcessCall> Calls { get; } = [];

    /// <summary>Gets each request as the file name followed by its space-joined arguments.</summary>
    public string[] Commands => [.. Calls.Select(static call => call.FileName + " " + string.Join(' ', call.Arguments))];

    /// <summary>Records the request and returns the answer that the response function gives for it.</summary>
    public Task<string> Run(string fileName, string[] arguments, string workingDirectory, CancellationToken ct)
    {
        Calls.Add(new ProcessCall(fileName, arguments, workingDirectory, ct));
        return respond(fileName, arguments, ct);
    }

    /// <summary>Records a request together with its environment when its start information changed the inherited one.</summary>
    public Task<string> Run(ProcessStartInfo startInfo, CancellationToken ct)
    {
        string[] arguments = [.. startInfo.ArgumentList];
        var inherited = new ProcessStartInfo().Environment;
        var configured = inherited.Count != startInfo.Environment.Count
            || startInfo.Environment.Any(variable => !inherited.TryGetValue(variable.Key, out var value) || value != variable.Value);
        Calls.Add(new ProcessCall(
            startInfo.FileName, arguments, startInfo.WorkingDirectory, ct, configured ? new Dictionary<string, string?>(startInfo.Environment) : null));
        return respond(startInfo.FileName, arguments, ct);
    }

    /// <summary>Creates a machine whose processes this log answers, with bare command names, and the clock and service start given.</summary>
    public CiMachine Machine(TimeProvider? timeProvider = null, Func<ProcessStartInfo, Process>? startService = null)
    {
        var machine = new CiMachine(new ScriptedProcessRunner(async (startInfo, _, ct) => new ProcessOutput(0, await Run(startInfo, ct))), new ExecutableMap(static command => command), timeProvider ?? TimeProvider.System);
        return startService is null ? machine : new CiMachine(machine.ProcessRunner, machine.ExecutableResolver, machine.TimeProvider) { StartService = startService };
    }
}
