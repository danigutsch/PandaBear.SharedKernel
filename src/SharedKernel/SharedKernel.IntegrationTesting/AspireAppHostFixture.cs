namespace PandaBear.IntegrationTesting;

/// <summary>
/// An <see cref="AspireSystemFixture"/> that starts an AppHost project: each profile runs the AppHost with the arguments
/// <see cref="Launch"/> returns (typically the profile folder as a data-folder setting), waits for the named resources, and fails with their
/// state and log lines when one is not healthy in time.
/// </summary>
/// <typeparam name="TAppHost">The AppHost project's metadata type, such as <c>Projects.MyApp_AppHost</c>.</typeparam>
public abstract class AspireAppHostFixture<TAppHost> : AspireSystemFixture
    where TAppHost : class
{
    /// <summary>How to start a profile.</summary>
    /// <param name="profile">The profile.</param>
    /// <param name="folder">The profile's own empty folder of the workspace.</param>
    /// <returns>The launch settings.</returns>
    protected abstract AppHostLaunch Launch(string profile, string folder);

    /// <inheritdoc />
    protected sealed override Task<AspireTestApplication> Start(string profile, string folder, CancellationToken ct)
    {
        var launch = Launch(profile, folder);
        return AspireTestApplication.Start<TAppHost>(launch.HealthyResources, launch.StartupTimeout, [.. launch.Arguments], ct);
    }
}
