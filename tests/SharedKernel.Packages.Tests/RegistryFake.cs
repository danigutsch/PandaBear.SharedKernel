namespace PandaBear.Packages.Tests;

/// <summary>Builds registry sources over fake handlers.</summary>
internal static class RegistryFake
{
    /// <summary>Creates the source over <paramref name="client"/>.</summary>
    public static RegistryReleaseSource Source(HttpClient client, TimeProvider? time = null, TimeSpan? lifetime = null, TimeSpan? timeout = null) =>
        new(client, time ?? new TestTimeProvider(OsvFake.Start), lifetime, timeout);
}
