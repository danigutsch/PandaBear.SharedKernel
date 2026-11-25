using SharedKernel.IntegrationTesting;

namespace SharedKernel.Npgsql.Tests;

/// <summary>
/// A system fixture whose start leaves a folder the workspace cannot delete and then fails when the test says so, so disposal meets both a
/// start failure and a workspace failure.
/// </summary>
internal sealed class UndeletableWorkspaceFixture : AspireSystemFixture
{
    private readonly TaskCompletionSource<AspireTestApplication> _start = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private string? _lockedFolder;

    /// <inheritdoc />
    protected override string WorkspaceName => "sk-undeletable-fixture";

    /// <summary>Fails the pending start.</summary>
    /// <param name="failure">The failure.</param>
    internal void FailStart(Exception failure) => _start.SetException(failure);

    /// <summary>Makes the locked folder deletable again and removes the workspace.</summary>
    internal void Unlock()
    {
        if (_lockedFolder is null)
        {
            return;
        }

        if (OperatingSystem.IsWindows())
        {
            new DirectoryInfo(Path.Combine(_lockedFolder, "held")).Attributes = FileAttributes.Directory;
        }
        else
        {
            File.SetUnixFileMode(_lockedFolder, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        Directory.Delete(Workspace.Root, recursive: true);
    }

    /// <inheritdoc />
    protected override Task<AspireTestApplication> Start(string profile, string folder, CancellationToken ct)
    {
        var held = Directory.CreateDirectory(Path.Combine(folder, "held"));
        if (OperatingSystem.IsWindows())
        {
            held.Attributes |= FileAttributes.ReadOnly;
        }
        else
        {
            File.SetUnixFileMode(folder, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        }

        _lockedFolder = folder;
        return _start.Task;
    }
}
