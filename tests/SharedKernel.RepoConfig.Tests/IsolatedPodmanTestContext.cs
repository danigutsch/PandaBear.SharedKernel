using System.Diagnostics;
using System.Runtime.Versioning;
using PandaBear.Processes;

namespace PandaBear.RepoConfig.Tests;

/// <summary>Records the private Podman scopes and scripts the local engine without touching the host container store.</summary>
internal sealed class IsolatedPodmanTestContext : IDisposable
{
    private readonly TemporaryDirectory _directory = new();
    private readonly List<string> _privateRoots = [];
    private readonly List<string> _privateRunRoots = [];
    private ReadOnlyDirectory? _imageLayerPermissions;
    private string? _imageLayer;

    /// <summary>Creates a fake rootless proc tree and a dummy image store.</summary>
    public IsolatedPodmanTestContext()
    {
        Directory.CreateDirectory(Path.Combine(_directory.Path, "self"));
        File.WriteAllText(Path.Combine(_directory.Path, "self", "status"), "Name:\ttest\nUid:\t1000\t1000\t1000\t1000\n");
        Host = new PodmanDnsHost(Path.Combine(_directory.Path, "podman"), null, _directory.Path, true, Podman);
    }

    /// <summary>Gets the existing directory supplied as the read-only image store.</summary>
    public string ImageStore => _directory.Path;

    /// <summary>Gets the absolute test executable path supplied to the command runner.</summary>
    public string Executable => Path.Combine(_directory.Path, "test-executable");

    /// <summary>Gets the host with scripted inspection and cleanup processes.</summary>
    public PodmanDnsHost Host { get; }

    /// <summary>Gets or sets whether the engine scope check reports a remote service.</summary>
    public bool InvalidScope { get; set; }

    /// <summary>Gets or sets the exact Podman command that returns an error.</summary>
    public string? FailOperation { get; set; }

    /// <summary>Gets the observed Podman operations in invocation order.</summary>
    public List<string> Operations { get; } = [];

    /// <summary>Gets the private engine roots observed during verification.</summary>
    public IReadOnlyList<string> Roots => _privateRoots;

    /// <summary>Gets the private run roots observed during verification.</summary>
    public IReadOnlyList<string> RunRoots => _privateRunRoots;

    /// <summary>Runs the opt-in CLI with the given child runner and captures its output.</summary>
    /// <param name="runner">Runner for the child executable.</param>
    /// <param name="arguments">Arguments for the child executable.</param>
    /// <param name="runtimeParent">Optional existing parent of the private runtime root.</param>
    /// <returns>The exit code and captured output.</returns>
    public async Task<(int ExitCode, string Output, string Error)> RunCommand(
        IProcessRunner runner,
        string[]? arguments = null,
        string? runtimeParent = null)
    {
        using var output = new InvariantStringWriter();
        using var error = new InvariantStringWriter();
        var environment = new RepoConfigToolEnvironment(_directory.Path) { PodmanHost = Host, IsolatedPodmanCommandRunner = runner };
        string[] runtimeArguments = runtimeParent is null ? [] : ["--runtime-parent", runtimeParent];
        var exitCode = await RepoConfigToolRunner.Run(
            ["run-isolated-podman", "--image-store", ImageStore, .. runtimeArguments, "--", Executable, .. arguments ?? []],
            output, error, environment, TestContext.Current.CancellationToken).ConfigureAwait(false);
        return (exitCode, output.ToString(), error.ToString());
    }

    /// <summary>Stages one private image layer with a directory the host user cannot write.</summary>
    [UnsupportedOSPlatform("windows")]
    public void StagePrivateImageLayer(ProcessStartInfo child)
    {
        var root = Path.GetDirectoryName(child.Environment["CONTAINERS_STORAGE_CONF"]!)!;
        _imageLayer = Path.Combine(root, "graph", "overlay", "owned-image", "diff");
        Directory.CreateDirectory(_imageLayer);
        File.WriteAllText(Path.Combine(_imageLayer, "image-entry"), "private image bytes");
        _imageLayerPermissions = new ReadOnlyDirectory(_imageLayer);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        RemovePrivateImageLayer();
        foreach (var root in _privateRoots.Where(Directory.Exists))
        {
            Directory.Delete(root, recursive: true);
        }
        _directory.Dispose();
    }

    private void RemovePrivateImageLayer()
    {
        if (_imageLayer is { } layer)
        {
            if (!OperatingSystem.IsWindows())
            {
                _imageLayerPermissions?.Dispose();
            }
            Directory.Delete(layer, recursive: true);
            _imageLayer = null;
            _imageLayerPermissions = null;
        }
    }

    private async Task<PodmanCommandResult> Podman(ProcessStartInfo startInfo, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        startInfo.ArgumentList[0].ShouldBe("--remote=false");
        var storage = startInfo.Environment["CONTAINERS_STORAGE_CONF"]!;
        var root = Path.GetDirectoryName(storage)!;
        var graph = Path.Combine(root, "graph");
        const string runRootPrefix = "runroot = \"";
        var storageLines = await File.ReadAllLinesAsync(storage, ct).ConfigureAwait(false);
        var runRootLine = storageLines.Single(line => line.StartsWith(runRootPrefix, StringComparison.Ordinal));
        var runRoot = runRootLine[runRootPrefix.Length..^1].Replace(@"\\", @"\", StringComparison.Ordinal);
        if (!_privateRoots.Contains(root, StringComparer.Ordinal))
        {
            _privateRoots.Add(root);
            _privateRunRoots.Add(runRoot);
        }
        var arguments = string.Join(' ', startInfo.ArgumentList.Skip(1));
        Operations.Add(arguments);
        if (string.Equals(arguments, "image rm --all", StringComparison.Ordinal) && FailOperation != arguments)
        {
            RemovePrivateImageLayer();
        }
        var output = arguments switch
        {
            "info --format {{.Host.ServiceIsRemote}}" => "false\n",
            "info --format {{.Host.Security.Rootless}}" => "true\n",
            "info --format {{.Store.RunRoot}}" => runRoot + "\n",
            "ps --quiet" or "ps --all --quiet" => string.Empty,
            "network ls --format {{.Name}}" => "podman\n",
            "pod rm --all --force --time 0" or "rm --all --force --time 0"
                or "network prune --force" or "volume rm --all --force" or "image rm --all" => string.Empty,
            _ when arguments.StartsWith("info --format {{.Host.ServiceIsRemote}}\n", StringComparison.Ordinal) =>
                InvalidScope ? "true\n" : $"false\ntrue\n{graph}\n{runRoot}\nnetavark\n{storage}\n{Path.Combine(graph, "volumes")}\n",
            _ => throw new InvalidOperationException($"Unexpected Podman operation: {arguments}"),
        };
        return string.Equals(arguments, FailOperation, StringComparison.Ordinal)
            ? new PodmanCommandResult(125, string.Empty, "raw engine output")
            : new PodmanCommandResult(0, output, string.Empty);
    }
}
