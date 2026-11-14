using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting;

/// <summary>AppHost builder helpers that are not tied to one container.</summary>
public static class SharedKernelAppHostExtensions
{
    /// <summary>The shortest generated secret <see cref="AddGeneratedSecret"/> accepts.</summary>
    private const int MinimumSecretLength = 16;

    /// <summary>
    /// Adds a secret parameter whose value is generated when configuration does not supply one: <c>Parameters:{name}</c> from user secrets,
    /// <c>appsettings</c>, an environment variable (<c>Parameters__{name}</c>) or the command line wins over the generated value.
    /// </summary>
    /// <param name="builder">The AppHost builder.</param>
    /// <param name="name">The parameter name.</param>
    /// <param name="minLength">The length of a generated value: letters and digits only, so it needs no escaping in a URL or a connection string. At least 16; 32 by default.</param>
    /// <returns>The parameter, marked secret so the dashboard masks it and a publish treats it as a secret.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="minLength"/> is less than 16.</exception>
    /// <remarks>
    /// Follows the Aspire rule to model API keys, tokens and passwords as secret parameters in the AppHost instead of values in <c>.env</c>
    /// files or code. A generated value is not saved: it changes with every AppHost run. Supply the value through configuration when a
    /// persistent store (a data volume or bind mount) already holds a value that must not change.
    /// </remarks>
    public static IResourceBuilder<ParameterResource> AddGeneratedSecret(this IDistributedApplicationBuilder builder, [ResourceName] string name, int minLength = 32)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfLessThan(minLength, MinimumSecretLength);

        return builder.AddParameter(name, new GenerateParameterDefault { MinLength = minLength, Special = false }, secret: true);
    }
}
