using System.Diagnostics;
using SharedKernel.Results;

namespace SharedKernel.Packages.Tests;

/// <summary>Simulates apt and cargo reads and mutations over a test-owned in-memory package state.</summary>
internal sealed class PackageConvergenceFixture : IProcessRunner
{
    private readonly Dictionary<(PackageManagerKind Manager, string Name), string> _installed = [];
    private readonly List<string> _events = [];
    private readonly List<PackageChange> _attempts = [];
    private readonly FakePackageChangeExecutor _standard;
    private readonly FakePackageChangeExecutor _elevated;
    private (string Package, bool AfterMutation)? _fault;

    /// <summary>Creates the fixture and its convergence engine.</summary>
    internal PackageConvergenceFixture()
    {
        _standard = new FakePackageChangeExecutor((manager, change, ct) => Apply(manager, change, "apply", ct));
        _elevated = new FakePackageChangeExecutor((manager, change, ct) => Apply(manager, change, "elevated", ct));
        Engine = new PackageConvergence(new PackageInventory(this,
            new ExecutableMap(static command => Path.Combine(Path.GetTempPath(), "convergence-fixture", command))));
    }

    /// <summary>Gets the convergence engine wired entirely to fake ports.</summary>
    internal PackageConvergence Engine { get; }

    /// <summary>Gets observations, classifications, approvals, and attempts in their observed order.</summary>
    internal IReadOnlyList<string> Events => _events;

    /// <summary>Gets the changes that reached an executor.</summary>
    internal IReadOnlyList<PackageChange> Attempts => _attempts;

    /// <summary>Gets or sets the manager whose observed state cannot be read.</summary>
    internal PackageManagerKind? Unreadable { get; set; }

    /// <summary>Gets or sets whether executors report success without changing state.</summary>
    internal bool AcknowledgeWithoutMutation { get; set; }

    /// <summary>Sets the current installed package version.</summary>
    internal void Install(PackageManagerKind manager, string name, string version) => _installed[(manager, name)] = version;

    /// <summary>Returns the installed version of a package, if present.</summary>
    internal string? InstalledVersion(PackageManagerKind manager, string name) => _installed.GetValueOrDefault((manager, name));

    /// <summary>Fails one attempt for a package before mutation or throws after it changed the observed state.</summary>
    internal void FailOnce(string package, bool afterMutation = false) => _fault = (package, afterMutation);

    /// <summary>Creates the fixture's classified execution ports.</summary>
    internal PackageApplyPorts Ports(
        Func<PackageManagerKind, PackageChange, bool>? approve = null,
        Func<PackageManagerKind, PackageChange, PackageChangePrivilege>? policy = null,
        bool withElevation = true) =>
        new(_standard, (manager, change) =>
        {
            Record("classify", manager, change);
            return policy?.Invoke(manager, change) ?? (manager == PackageManagerKind.Apt ? PackageChangePrivilege.Elevated : PackageChangePrivilege.Unprivileged);
        }, withElevation ? _elevated : null, approve is null ? null : (manager, change) =>
        {
            Record("approve", manager, change);
            return approve(manager, change);
        });

    /// <inheritdoc />
    public Task<ProcessOutput> Run(ProcessStartInfo startInfo, TextWriter? forward, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var manager = Path.GetFileName(startInfo.FileName) switch
        {
            "dpkg-query" => PackageManagerKind.Apt,
            "cargo" => PackageManagerKind.Cargo,
            _ => throw new InvalidOperationException("The fixture provides only apt and cargo observations."),
        };
        _events.Add($"read:{PackageManagers.Id(manager)}");
        if (manager == Unreadable)
        {
            return Task.FromResult(new ProcessOutput(1, string.Empty, "fixture read failed"));
        }

        var lines = _installed.Where(package => package.Key.Manager == manager)
            .Select(package => manager == PackageManagerKind.Apt
                ? $"{package.Key.Name}\t{package.Value}\tii \n"
                : $"{package.Key.Name} v{package.Value}:\n    {package.Key.Name}\n");
        return Task.FromResult(new ProcessOutput(0, string.Concat(lines)));
    }

    private Result Apply(PackageManagerKind manager, PackageChange change, string boundary, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Record(boundary, manager, change);
        _attempts.Add(change);
        var name = change.Requirement.Name.Value;
        var fail = _fault is { } fault && fault.Package == name;
        var afterMutation = fail && _fault.GetValueOrDefault().AfterMutation;
        if (fail)
        {
            _fault = null;
            if (!afterMutation)
            {
                return Result.Error("fixture apply failed before mutation");
            }
        }

        if (!AcknowledgeWithoutMutation)
        {
            Install(manager, name, change.Requirement.Version?.Value ?? "1.0.0");
        }

        if (afterMutation)
        {
            throw new TimeoutException("fixture apply timed out after mutation");
        }

        return Result.Ok();
    }

    private void Record(string operation, PackageManagerKind manager, PackageChange change) =>
        _events.Add($"{operation}:{PackageManagers.Id(manager)}:{change.Requirement.Name.Value}");
}
