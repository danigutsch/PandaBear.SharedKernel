namespace PandaBear.IntegrationTesting;

/// <summary>How an <see cref="AspireAppHostFixture{TAppHost}"/> starts one profile of an AppHost.</summary>
/// <param name="arguments">The AppHost configuration arguments, such as <c>--Section:Key=value</c>.</param>
/// <param name="healthyResources">The resources that must report healthy before tests use the application.</param>
/// <param name="startupTimeout">How long the start and the health wait may take together.</param>
/// <param name="configuration">
/// AppHost configuration values, such as connection strings of servers the tests own or a profile's data folder; they win over the
/// AppHost's appsettings, user secrets and environment variables, and the arguments win over them. <see langword="null"/> means none; a
/// <see langword="null"/> value contributes nothing. A key must not be empty, white-space, or contain <c>=</c>.
/// </param>
/// <exception cref="ArgumentNullException">Thrown when <paramref name="arguments"/> or <paramref name="healthyResources"/> is <see langword="null"/>.</exception>
/// <exception cref="ArgumentException">Thrown when a key of <paramref name="configuration"/> is empty, white-space, or contains <c>=</c>.</exception>
public sealed class AppHostLaunch(
    IReadOnlyList<string> arguments,
    IReadOnlyList<string> healthyResources,
    TimeSpan startupTimeout,
    IReadOnlyDictionary<string, string?>? configuration = null)
{
    /// <summary>Gets the AppHost configuration arguments.</summary>
    public IReadOnlyList<string> Arguments { get; } = arguments ?? throw new ArgumentNullException(nameof(arguments));

    /// <summary>Gets the resources that must report healthy before tests use the application.</summary>
    public IReadOnlyList<string> HealthyResources { get; } = healthyResources ?? throw new ArgumentNullException(nameof(healthyResources));

    /// <summary>Gets how long the start and the health wait may take together.</summary>
    public TimeSpan StartupTimeout { get; } = startupTimeout;

    /// <summary>Gets the AppHost configuration values; they win over appsettings, user secrets and environment variables, and the arguments win over them.</summary>
    public IReadOnlyDictionary<string, string?> Configuration { get; } = AspireTestApplication.CheckConfigurationKeys(configuration ?? new Dictionary<string, string?>(), nameof(configuration));
}
