namespace SharedKernel.AspNetCore.WebAnalytics.Tests;

/// <summary>The Umami connection strings the test sites are configured with.</summary>
internal static class UmamiConnections
{
    /// <summary>The configuration key of the default Umami connection string, as an Aspire reference named <c>umami</c> sets it.</summary>
    internal const string Key = "ConnectionStrings:umami";

    /// <summary>The website id of every test connection string.</summary>
    internal static readonly Guid WebsiteId = Guid.Parse("8f1d8a3e-7c2b-4d5e-9f60-1a2b3c4d5e6f");

    /// <summary>Gets the connection string of the fake Umami at <c>http://umami.test/</c>.</summary>
    internal static string Default { get; } = For("http://umami.test/");

    /// <summary>Creates the connection string of an Umami at <paramref name="endpoint"/> with <see cref="WebsiteId"/>.</summary>
    /// <param name="endpoint">The base address of Umami.</param>
    /// <returns>The connection string.</returns>
    internal static string For(string endpoint) => $"Endpoint={endpoint};WebsiteId={WebsiteId}";
}
