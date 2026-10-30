using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace PandaBear.RepoConfig.Tests;

/// <summary>Builds a temporary fake proc and Podman run-root tree and scripts Podman and stat responses so the verify-podman-dns command can run without a real Podman.</summary>
internal sealed class PodmanDnsPreflightTestContext : IDisposable
{
    private const int CurrentUserId = 1000;
    private readonly TemporaryDirectory _directory = new();
    private readonly Dictionary<string, PodmanCommandResult> _responses = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Queue<PodmanCommandResult>> _responseQueues = new(StringComparer.Ordinal);
    private Action? _onContainerRecheck;
    private Action? _onFinalNamespaceCheck;
    private string? _systemProcessExecutable;
    private int _containerQueryCount;
    private int _namespaceQueryCount;

    /// <summary>Creates a unique temp root with a fake proc tree whose own status reports user id 1000 and an empty Podman run root.</summary>
    public PodmanDnsPreflightTestContext()
    {
        RootPath = _directory.Path;
        ProcRoot = Path.Combine(RootPath, "proc");
        RunRoot = Path.Combine(RootPath, "runroot");
        AardvarkDirectory = Path.Combine(RunRoot, "networks", "aardvark-dns");
        Directory.CreateDirectory(Path.Combine(ProcRoot, "self"));
        Directory.CreateDirectory(RunRoot);
        File.WriteAllText(
            Path.Combine(ProcRoot, "self", "status"),
            $"Name:\ttest{Environment.NewLine}Uid:\t{CurrentUserId}\t{CurrentUserId}\t{CurrentUserId}\t{CurrentUserId}{Environment.NewLine}");
    }

    /// <summary>Gets the unique temporary directory that holds the fake proc and run-root trees.</summary>
    public string RootPath
    {
        get;
    }

    /// <summary>Gets the fake proc directory that stands in for /proc.</summary>
    public string ProcRoot
    {
        get;
    }

    /// <summary>Gets the fake Podman run-root directory that the stubbed <c>info</c> query reports.</summary>
    public string RunRoot
    {
        get;
    }

    /// <summary>Gets the networks/aardvark-dns configuration directory under the run root.</summary>
    public string AardvarkDirectory
    {
        get;
    }

    /// <summary>Gets or sets the output returned for the <c>network ls</c> query, one network name per line.</summary>
    public string Networks { get; set; } = string.Empty;

    /// <summary>Gets or sets the output returned for the <c>ps --quiet</c> query, one container id per line.</summary>
    public string Containers { get; set; } = string.Empty;

    /// <summary>Gets the Podman argument strings that the command ran, in order, excluding stat calls.</summary>
    public List<string> Commands { get; } = [];

    /// <summary>Gets every process start info the command submitted, including stat calls, for asserting executables and environment.</summary>
    public List<ProcessStartInfo> ProcessStartInfos { get; } = [];

    /// <summary>Creates a fake aardvark-dns process under the proc root with comm, status, stat, cmdline and network namespace files, plus the optional pid file and ref-count file.</summary>
    /// <param name="processId">The process id of the fake daemon.</param>
    /// <param name="configDirectory">The directory passed to the daemon's <c>--config</c> argument, or the aardvark directory when null.</param>
    /// <param name="equalsForm">True to write <c>--config=dir</c> as one argument instead of two.</param>
    /// <param name="writePidFile">True to write aardvark.pid containing the process id.</param>
    /// <param name="refCount">The ref-count file content, or null to skip writing the file.</param>
    /// <param name="startTime">The start time written into the process stat file.</param>
    public void AddDaemon(
        int processId,
        string? configDirectory = null,
        bool equalsForm = false,
        bool writePidFile = true,
        string? refCount = "1",
        ulong startTime = 100)
    {
        configDirectory ??= AardvarkDirectory;
        var processDirectory = Path.Combine(ProcRoot, processId.ToString(CultureInfo.InvariantCulture));
        Directory.CreateDirectory(processDirectory);
        File.WriteAllText(Path.Combine(processDirectory, "comm"), $"aardvark-dns{Environment.NewLine}");
        File.WriteAllText(
            Path.Combine(processDirectory, "status"),
            $"Name:\taardvark-dns{Environment.NewLine}Uid:\t{CurrentUserId}\t{CurrentUserId}\t{CurrentUserId}\t{CurrentUserId}{Environment.NewLine}");
        WriteProcessStat(processId, startTime);
        var arguments = equalsForm
            ? new[] { "aardvark-dns", $"--config={configDirectory}" }
            : new[] { "aardvark-dns", "--config", configDirectory };
        File.WriteAllBytes(
            Path.Combine(processDirectory, "cmdline"),
            Encoding.UTF8.GetBytes(string.Join('\0', arguments) + '\0'));
        var processNamespaceDirectory = Path.Combine(processDirectory, "ns");
        Directory.CreateDirectory(processNamespaceDirectory);
        File.WriteAllText(Path.Combine(processNamespaceDirectory, "net"), "1:100\n");
        var activeNamespacePath = Path.Combine(RunRoot, "networks", "rootless-netns", "rootless-netns");
        Directory.CreateDirectory(Path.GetDirectoryName(activeNamespacePath) ?? RunRoot);
        if (!File.Exists(activeNamespacePath))
        {
            File.WriteAllText(activeNamespacePath, "1:100\n");
        }

        if (writePidFile)
        {
            WriteConfig("aardvark.pid", $"{processId.ToString(CultureInfo.InvariantCulture)}{Environment.NewLine}");
        }

        if (refCount is not null)
        {
            var refCountPath = Path.Combine(RunRoot, "networks", "rootless-netns", "ref-count");
            Directory.CreateDirectory(Path.GetDirectoryName(refCountPath) ?? RunRoot);
            File.WriteAllText(refCountPath, refCount + Environment.NewLine);
        }
    }

    /// <summary>Writes a file with the given content into the aardvark-dns configuration directory, creating the directory first.</summary>
    /// <param name="name">The file name inside the aardvark directory.</param>
    /// <param name="content">The file content.</param>
    public void WriteConfig(string name, string content = "config")
    {
        Directory.CreateDirectory(AardvarkDirectory);
        File.WriteAllText(Path.Combine(AardvarkDirectory, name), content);
    }

    /// <summary>Replaces the aardvark-dns directory path with a regular file so the preflight meets a non-directory there.</summary>
    public void WriteAardvarkDirectoryAsFile()
    {
        var networksDirectory = Path.Combine(RunRoot, "networks");
        Directory.CreateDirectory(networksDirectory);
        File.WriteAllText(AardvarkDirectory, "not a directory");
    }

    /// <summary>Writes a regular file at the run root's networks path so the preflight meets a non-directory there.</summary>
    public void WriteNetworksDirectoryAsFile()
    {
        Directory.CreateDirectory(RunRoot);
        File.WriteAllText(Path.Combine(RunRoot, "networks"), "not a directory");
    }

    /// <summary>Makes the Podman query with the given arguments fail with exit code 1 and the error "simulated failure".</summary>
    /// <param name="arguments">The Podman arguments after the executable name.</param>
    public void SetFailure(string arguments) =>
        _responses[arguments] = new PodmanCommandResult(1, string.Empty, "simulated failure");

    /// <summary>Makes the Podman query with the given arguments succeed with exit code 0 and the given standard output.</summary>
    /// <param name="arguments">The Podman arguments after the executable name.</param>
    /// <param name="output">The standard output to return.</param>
    public void SetResponse(string arguments, string output) =>
        _responses[arguments] = new PodmanCommandResult(0, output, string.Empty);

    /// <summary>Makes the Podman query with the given arguments succeed with exit code 0 while also writing the given standard error.</summary>
    /// <param name="arguments">The Podman arguments after the executable name.</param>
    /// <param name="output">The standard output to return.</param>
    /// <param name="error">The standard error to return.</param>
    public void SetResponse(string arguments, string output, string error) =>
        _responses[arguments] = new PodmanCommandResult(0, output, error);

    /// <summary>Queues successive standard outputs for repeated runs of the same Podman query, one consumed per call, before falling back to fixed responses.</summary>
    /// <param name="arguments">The Podman arguments after the executable name.</param>
    /// <param name="outputs">The outputs returned in order on each call.</param>
    public void SetResponses(string arguments, params string[] outputs) =>
        _responseQueues[arguments] = new Queue<PodmanCommandResult>(
            outputs.Select(output => new PodmanCommandResult(0, output, string.Empty)));

    /// <summary>Routes processes started with the given executable to the real process runner instead of the stub.</summary>
    /// <param name="executable">The executable path to run for real.</param>
    public void UseSystemProcessFor(string executable) =>
        _systemProcessExecutable = executable;

    /// <summary>Overwrites the network namespace identity (device:inode text) of a fake process.</summary>
    /// <param name="processId">The fake process id.</param>
    /// <param name="identity">The identity text the stubbed stat returns.</param>
    public void SetProcessNamespaceIdentity(int processId, string identity) =>
        File.WriteAllText(Path.Combine(ProcRoot, processId.ToString(CultureInfo.InvariantCulture), "ns", "net"), identity + "\n");

    /// <summary>Deletes the network namespace file of a fake process so its namespace cannot be read.</summary>
    /// <param name="processId">The fake process id.</param>
    public void DeleteProcessNamespaceIdentity(int processId) =>
        File.Delete(Path.Combine(ProcRoot, processId.ToString(CultureInfo.InvariantCulture), "ns", "net"));

    /// <summary>Rewrites the stat file of a fake process with a new start time.</summary>
    /// <param name="processId">The fake process id.</param>
    /// <param name="startTime">The start time to record.</param>
    public void SetProcessStartTime(int processId, ulong startTime) =>
        WriteProcessStat(processId, startTime);

    /// <summary>Changes a fake process's start time during the second container query, simulating a daemon restart between checks.</summary>
    /// <param name="processId">The fake process id.</param>
    /// <param name="startTime">The new start time.</param>
    public void ChangeProcessStartTimeOnRecheck(int processId, ulong startTime) =>
        _onContainerRecheck = () => SetProcessStartTime(processId, startTime);

    /// <summary>Changes a fake process's namespace identity during the second container query, simulating a namespace swap between checks.</summary>
    /// <param name="processId">The fake process id.</param>
    /// <param name="identity">The new identity text.</param>
    public void ChangeNamespaceIdentityOnRecheck(int processId, string identity) =>
        _onContainerRecheck = () => SetProcessNamespaceIdentity(processId, identity);

    /// <summary>Changes a fake process's start time when the fourth namespace stat query runs, simulating a restart just before the final check completes.</summary>
    /// <param name="processId">The fake process id.</param>
    /// <param name="startTime">The new start time.</param>
    public void ChangeProcessStartTimeOnFinalNamespaceCheck(int processId, ulong startTime) =>
        _onFinalNamespaceCheck = () => SetProcessStartTime(processId, startTime);

    /// <summary>Rewrites aardvark.pid with the given content during the second container query.</summary>
    /// <param name="content">The new pid file content.</param>
    public void ChangePidFileOnRecheck(string content) =>
        _onContainerRecheck = () => WriteConfig("aardvark.pid", content);

    /// <summary>Rewrites the rootless-netns ref-count file with the given content during the second container query.</summary>
    /// <param name="content">The new ref-count content.</param>
    public void ChangeRefCountOnRecheck(string content) =>
        _onContainerRecheck = () =>
            File.WriteAllText(Path.Combine(RunRoot, "networks", "rootless-netns", "ref-count"), content);

    /// <summary>Creates a new file in the aardvark directory during the second container query, simulating configuration appearing between checks.</summary>
    /// <param name="name">The config file name to create.</param>
    public void ChangeConfigOnRecheck(string name) =>
        _onContainerRecheck = () => WriteConfig(name);

    /// <summary>Creates the run root's networks directory during the second container query, simulating it appearing between checks.</summary>
    public void CreateNetworksDirectoryOnRecheck() =>
        _onContainerRecheck = () => Directory.CreateDirectory(Path.Combine(RunRoot, "networks"));

    /// <summary>Deletes the rootless-netns active namespace file.</summary>
    public void DeleteActiveNamespace() =>
        File.Delete(Path.Combine(RunRoot, "networks", "rootless-netns", "rootless-netns"));

    /// <summary>Overwrites the comm file of a fake process so it reports a different process name.</summary>
    /// <param name="processId">The fake process id.</param>
    /// <param name="name">The process name to record.</param>
    public void SetProcessName(int processId, string name) =>
        File.WriteAllText(
            Path.Combine(ProcRoot, processId.ToString(CultureInfo.InvariantCulture), "comm"),
            name + Environment.NewLine);

    /// <summary>Reads every file under the root into a dictionary of relative path to bytes so tests can assert the command changed nothing.</summary>
    /// <returns>The file contents keyed by path relative to the root.</returns>
    public IReadOnlyDictionary<string, byte[]> Snapshot() =>
        Directory
            .EnumerateFiles(RootPath, "*", SearchOption.AllDirectories)
            .ToDictionary(
                path => Path.GetRelativePath(RootPath, path),
                File.ReadAllBytes,
                StringComparer.Ordinal);

    /// <summary>Runs the verify-podman-dns command against this context's fake host and captures its exit code and output.</summary>
    /// <param name="arguments">Extra command-line arguments appended after the command name.</param>
    /// <param name="podmanExecutable">The Podman executable path the host reports, or null when Podman is missing.</param>
    /// <param name="statExecutable">The stat executable path the host reports, or null when stat is missing.</param>
    /// <param name="isLinux">Whether the host reports a Linux platform.</param>
    /// <returns>The exit code, standard output and standard error.</returns>
    public async Task<(int ExitCode, string StandardOutput, string StandardError)> Run(
        string[]? arguments = null,
        string? podmanExecutable = "/usr/bin/podman",
        string? statExecutable = "/usr/bin/stat",
        bool isLinux = true)
    {
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        var environment = new RepoConfigToolEnvironment(RootPath)
        {
            PodmanHost = new PodmanDnsHost(podmanExecutable, statExecutable, ProcRoot, isLinux, RunProcess)
        };
        var exitCode = await RepoConfigToolRunner.Run(
            ["verify-podman-dns", .. arguments ?? []],
            output,
            error,
            environment,
            TestContext.Current.CancellationToken);

        return (exitCode, output.ToString(), error.ToString());
    }

    /// <inheritdoc />
    public void Dispose() => _directory.Dispose();

    private void WriteProcessStat(int processId, ulong startTime)
    {
        var fields = Enumerable.Repeat("0", 20).ToArray();
        fields[0] = "S";
        fields[19] = startTime.ToString(CultureInfo.InvariantCulture);
        var processDirectory = Path.Combine(ProcRoot, processId.ToString(CultureInfo.InvariantCulture));
        Directory.CreateDirectory(processDirectory);
        File.WriteAllText(
            Path.Combine(processDirectory, "stat"),
            $"{processId.ToString(CultureInfo.InvariantCulture)} (aardvark-dns) {string.Join(' ', fields)}{Environment.NewLine}");
    }

    private async Task<PodmanCommandResult> RunProcess(ProcessStartInfo startInfo, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ProcessStartInfos.Add(startInfo);
        var arguments = startInfo.ArgumentList.ToArray();
        if (string.Equals(startInfo.FileName, _systemProcessExecutable, StringComparison.Ordinal))
        {
            return await PodmanDnsPreflightCommand.RunProcess(startInfo, cancellationToken).ConfigureAwait(false);
        }
        if (string.Equals(startInfo.FileName, "/usr/bin/stat", StringComparison.Ordinal))
        {
            if (arguments.Length != 5
                || !string.Equals(arguments[0], "-L", StringComparison.Ordinal)
                || !string.Equals(arguments[1], "-c", StringComparison.Ordinal)
                || !string.Equals(arguments[2], "%d:%i", StringComparison.Ordinal)
                || !string.Equals(arguments[3], "--", StringComparison.Ordinal)
                || !startInfo.Environment.TryGetValue("LC_ALL", out var locale)
                || !string.Equals(locale, "C", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Unexpected stat query shape.");
            }

            _namespaceQueryCount++;
            if (_namespaceQueryCount == 4)
            {
                _onFinalNamespaceCheck?.Invoke();
                _onFinalNamespaceCheck = null;
            }

            var path = arguments[^1];
            var identity = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            return new PodmanCommandResult(0, identity, string.Empty);
        }

        Commands.Add(string.Join(' ', arguments));
        var query = string.Join(' ', arguments.Skip(1));
        if (string.Equals(query, "ps --quiet", StringComparison.Ordinal))
        {
            _containerQueryCount++;
            if (_containerQueryCount == 2)
            {
                _onContainerRecheck?.Invoke();
                _onContainerRecheck = null;
            }
        }

        if (_responseQueues.TryGetValue(query, out var configuredResponses) && configuredResponses.Count > 0)
        {
            return configuredResponses.Dequeue();
        }

        if (_responses.TryGetValue(query, out var configuredResponse))
        {
            return configuredResponse;
        }

        var output = query switch
        {
            "info --format {{.Host.ServiceIsRemote}}" => "false\n",
            "info --format {{.Host.Security.Rootless}}" => "true\n",
            "info --format {{.Store.RunRoot}}" => RunRoot + "\n",
            "ps --quiet" => Containers,
            "network ls --format {{.Name}}" => Networks,
            _ => throw new InvalidOperationException($"Unexpected Podman query: {query}")
        };

        return new PodmanCommandResult(0, output, string.Empty);
    }
}
