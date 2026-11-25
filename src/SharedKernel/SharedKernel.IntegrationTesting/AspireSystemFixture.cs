using System.Diagnostics.CodeAnalysis;

namespace SharedKernel.IntegrationTesting;

/// <summary>
/// An xUnit assembly (or class) fixture for system tests: it starts an Aspire application per profile on the first demand, once per test
/// run, gives each profile its own folder of a private <see cref="TestWorkspace"/>, and on disposal stops every application and removes the
/// workspace. Nothing starts until a test asks, so tests that need no application pay nothing. Subclass it and implement
/// <see cref="Start"/>; for an AppHost project use <see cref="AspireAppHostFixture{TAppHost}"/>.
/// </summary>
public abstract class AspireSystemFixture : IAsyncDisposable
{
    /// <summary>The profile <see cref="Application(CancellationToken)"/> starts.</summary>
    public const string DefaultProfile = "default";

    private readonly OnDemandResources<AspireTestApplication> _applications;
    private readonly Lazy<TestWorkspace> _workspace;

    /// <summary>Initializes the fixture; the workspace is created with the first application.</summary>
    protected AspireSystemFixture()
    {
        _workspace = new Lazy<TestWorkspace>(() => TestWorkspace.Create(WorkspaceName), LazyThreadSafetyMode.ExecutionAndPublication);
        _applications = new OnDemandResources<AspireTestApplication>((profile, ct) => Start(profile, Workspace.Folder(profile), ct));
    }

    /// <summary>Gets the workspace of this run; created on first use.</summary>
    public TestWorkspace Workspace => _workspace.Value;

    /// <summary>Gets the readable prefix of the workspace folder name.</summary>
    protected abstract string WorkspaceName { get; }

    /// <summary>Gets the application of the default profile, starting it on the first demand.</summary>
    /// <param name="ct">The caller's token; cancelling it abandons only this wait.</param>
    /// <returns>The started application.</returns>
    public Task<AspireTestApplication> Application(CancellationToken ct) => Application(DefaultProfile, ct);

    /// <summary>Gets the application of a profile, starting it on the first demand; every later caller shares it.</summary>
    /// <param name="profile">The profile, which names its own application and folder.</param>
    /// <param name="ct">The caller's token; cancelling it abandons only this wait.</param>
    /// <returns>The started application.</returns>
    /// <exception cref="ArgumentException"><paramref name="profile"/> is blank.</exception>
    /// <exception cref="ObjectDisposedException">The fixture was disposed.</exception>
    public Task<AspireTestApplication> Application(string profile, CancellationToken ct) => _applications.Get(profile, ct);

    /// <summary>Stops every started application, then removes the workspace.</summary>
    /// <returns>A task that completes when everything is cleaned up.</returns>
    /// <exception cref="AggregateException">
    /// Stopping an application or removing the workspace failed; the workspace is removed even when stopping failed, and every failure is
    /// reported.
    /// </exception>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "The workspace is removed before stop failures are reported.")]
    public async ValueTask DisposeAsync()
    {
        Exception? stopFailure = null;
        try
        {
            await _applications.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception failure)
        {
            stopFailure = failure;
        }

        Exception? workspaceFailure = null;
        if (_workspace.IsValueCreated)
        {
            try
            {
                await _workspace.Value.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception failure)
            {
                workspaceFailure = failure;
            }
        }

        GC.SuppressFinalize(this);
        Exception[] failures = [.. new[] { stopFailure, workspaceFailure }.OfType<Exception>()];
        if (failures.Length > 0)
        {
            throw new AggregateException("Stopping the system test applications or removing their workspace failed.", failures);
        }
    }

    /// <summary>
    /// Starts the application of a profile. Called once per profile, synchronously on the first caller's thread (Aspire reads metadata from
    /// the synchronous call stack, so a resource-only builder must be created here, in the test assembly).
    /// </summary>
    /// <param name="profile">The profile.</param>
    /// <param name="folder">The profile's own empty folder of the workspace.</param>
    /// <param name="ct">A token owned by the fixture, cancelled when it is disposed.</param>
    /// <returns>The started application.</returns>
    protected abstract Task<AspireTestApplication> Start(string profile, string folder, CancellationToken ct);
}
