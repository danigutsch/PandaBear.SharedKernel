using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace SharedKernel.RepoConfig.Tests;

internal sealed class PodmanDnsPreflightTestContext : IDisposable
{
    private const int CurrentUserId = 1000;
    private readonly Dictionary<string, PodmanCommandResult> _responses = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Queue<PodmanCommandResult>> _responseQueues = new(StringComparer.Ordinal);
    private Action? _onContainerRecheck;
    private Action? _onFinalNamespaceCheck;
    private string? _systemProcessExecutable;
    private int _containerQueryCount;
    private int _namespaceQueryCount;

    public PodmanDnsPreflightTestContext()
    {
        RootPath = Path.Combine(Path.GetTempPath(), "podman-dns-preflight-tests", Guid.NewGuid().ToString("N"));
        ProcRoot = Path.Combine(RootPath, "proc");
        RunRoot = Path.Combine(RootPath, "runroot");
        AardvarkDirectory = Path.Combine(RunRoot, "networks", "aardvark-dns");
        Directory.CreateDirectory(Path.Combine(ProcRoot, "self"));
        Directory.CreateDirectory(RunRoot);
        File.WriteAllText(
            Path.Combine(ProcRoot, "self", "status"),
            $"Name:\ttest{Environment.NewLine}Uid:\t{CurrentUserId}\t{CurrentUserId}\t{CurrentUserId}\t{CurrentUserId}{Environment.NewLine}");
    }

    public string RootPath { get; }

    public string ProcRoot { get; }

    public string RunRoot { get; }

    public string AardvarkDirectory { get; }

    public string Networks { get; set; } = string.Empty;

    public string Containers { get; set; } = string.Empty;

    public List<string> Commands { get; } = [];

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

    public void WriteConfig(string name, string content = "config")
    {
        Directory.CreateDirectory(AardvarkDirectory);
        File.WriteAllText(Path.Combine(AardvarkDirectory, name), content);
    }

    public void WriteAardvarkDirectoryAsFile()
    {
        var networksDirectory = Path.Combine(RunRoot, "networks");
        Directory.CreateDirectory(networksDirectory);
        File.WriteAllText(AardvarkDirectory, "not a directory");
    }

    public void WriteNetworksDirectoryAsFile()
    {
        Directory.CreateDirectory(RunRoot);
        File.WriteAllText(Path.Combine(RunRoot, "networks"), "not a directory");
    }

    public void SetFailure(string arguments) =>
        _responses[arguments] = new PodmanCommandResult(1, string.Empty, "simulated failure");

    public void SetResponse(string arguments, string output) =>
        _responses[arguments] = new PodmanCommandResult(0, output, string.Empty);

    public void SetResponse(string arguments, string output, string error) =>
        _responses[arguments] = new PodmanCommandResult(0, output, error);

    public void SetResponses(string arguments, params string[] outputs) =>
        _responseQueues[arguments] = new Queue<PodmanCommandResult>(
            outputs.Select(output => new PodmanCommandResult(0, output, string.Empty)));

    public void UseSystemProcessFor(string executable) =>
        _systemProcessExecutable = executable;

    public void SetProcessNamespaceIdentity(int processId, string identity) =>
        File.WriteAllText(Path.Combine(ProcRoot, processId.ToString(CultureInfo.InvariantCulture), "ns", "net"), identity + "\n");

    public void DeleteProcessNamespaceIdentity(int processId) =>
        File.Delete(Path.Combine(ProcRoot, processId.ToString(CultureInfo.InvariantCulture), "ns", "net"));

    public void SetProcessStartTime(int processId, ulong startTime) =>
        WriteProcessStat(processId, startTime);

    public void ChangeProcessStartTimeOnRecheck(int processId, ulong startTime) =>
        _onContainerRecheck = () => SetProcessStartTime(processId, startTime);

    public void ChangeNamespaceIdentityOnRecheck(int processId, string identity) =>
        _onContainerRecheck = () => SetProcessNamespaceIdentity(processId, identity);

    public void ChangeProcessStartTimeOnFinalNamespaceCheck(int processId, ulong startTime) =>
        _onFinalNamespaceCheck = () => SetProcessStartTime(processId, startTime);

    public void ChangePidFileOnRecheck(string content) =>
        _onContainerRecheck = () => WriteConfig("aardvark.pid", content);

    public void ChangeRefCountOnRecheck(string content) =>
        _onContainerRecheck = () =>
            File.WriteAllText(Path.Combine(RunRoot, "networks", "rootless-netns", "ref-count"), content);

    public void ChangeConfigOnRecheck(string name) =>
        _onContainerRecheck = () => WriteConfig(name);

    public void CreateNetworksDirectoryOnRecheck() =>
        _onContainerRecheck = () => Directory.CreateDirectory(Path.Combine(RunRoot, "networks"));

    public void DeleteActiveNamespace() =>
        File.Delete(Path.Combine(RunRoot, "networks", "rootless-netns", "rootless-netns"));

    public void SetProcessName(int processId, string name) =>
        File.WriteAllText(
            Path.Combine(ProcRoot, processId.ToString(CultureInfo.InvariantCulture), "comm"),
            name + Environment.NewLine);

    public IReadOnlyDictionary<string, byte[]> Snapshot() =>
        Directory
            .EnumerateFiles(RootPath, "*", SearchOption.AllDirectories)
            .ToDictionary(
                path => Path.GetRelativePath(RootPath, path),
                File.ReadAllBytes,
                StringComparer.Ordinal);

    public async Task<(int ExitCode, string StandardOutput, string StandardError)> Run(
        string[]? arguments = null,
        string? podmanExecutable = "/usr/bin/podman",
        string? statExecutable = "/usr/bin/stat",
        bool isLinux = true)
    {
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        var exitCode = await PodmanDnsPreflightCommand.Run(
            arguments ?? [],
            output,
            error,
            podmanExecutable,
            statExecutable,
            ProcRoot,
            isLinux,
            RunProcess,
            TestContext.Current.CancellationToken);

        return (exitCode, output.ToString(), error.ToString());
    }

    public void Dispose()
    {
        if (Directory.Exists(RootPath))
        {
            Directory.Delete(RootPath, recursive: true);
        }
    }

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
