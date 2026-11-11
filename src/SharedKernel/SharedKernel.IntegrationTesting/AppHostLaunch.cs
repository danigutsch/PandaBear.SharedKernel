namespace PandaBear.IntegrationTesting;

/// <summary>How an <see cref="AspireAppHostFixture{TAppHost}"/> starts one profile of an AppHost.</summary>
/// <param name="arguments">The AppHost configuration arguments, such as <c>--Section:Key=value</c>.</param>
/// <param name="healthyResources">The resources that must report healthy before tests use the application.</param>
/// <param name="startupTimeout">How long the start and the health wait may take together.</param>
public sealed class AppHostLaunch(IReadOnlyList<string> arguments, IReadOnlyList<string> healthyResources, TimeSpan startupTimeout)
{
    /// <summary>Gets the AppHost configuration arguments.</summary>
    public IReadOnlyList<string> Arguments { get; } = arguments ?? throw new ArgumentNullException(nameof(arguments));

    /// <summary>Gets the resources that must report healthy before tests use the application.</summary>
    public IReadOnlyList<string> HealthyResources { get; } = healthyResources ?? throw new ArgumentNullException(nameof(healthyResources));

    /// <summary>Gets how long the start and the health wait may take together.</summary>
    public TimeSpan StartupTimeout { get; } = startupTimeout;
}
