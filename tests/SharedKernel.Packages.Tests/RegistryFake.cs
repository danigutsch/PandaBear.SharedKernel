namespace PandaBear.Packages.Tests;

/// <summary>Builds registry sources over fake handlers.</summary>
internal static class RegistryFake
{
    /// <summary>Creates the source over <paramref name="handler"/>.</summary>
    public static RegistryReleaseSource Source(FakeHttpHandler handler, TimeProvider? time = null, TimeSpan? lifetime = null, TimeSpan? timeout = null) =>
        new(handler.Client, time ?? new TestTimeProvider(OsvFake.Start), lifetime, timeout);
}
