using SharedKernel.Aspire.Hosting.Umami.Testing;

namespace SharedKernel.Aspire.Hosting.Umami.Tests;

/// <summary>Stands in for the process environment, so a test never changes a process-wide variable.</summary>
internal static class UmamiTestEnvironment
{
    /// <summary>A website connection string as the owner of a shared Umami hands it over.</summary>
    public const string Website = "Endpoint=http://localhost:41300;WebsiteId=6f1c2d57-8a35-5b1e-9e43-2f6a2c9d4b10";

    /// <summary>
    /// Creates an environment in which only the borrowed-website variable is set, to <paramref name="value"/>. Reading any other variable
    /// fails, so a test notices code that reads more than the contract names.
    /// </summary>
    public static Func<string, string?> WithWebsite(string? value) =>
        name => string.Equals(name, UmamiTestWebsite.ConnectionStringVariable, StringComparison.Ordinal)
            ? value
            : throw new InvalidOperationException($"The test environment defines no variable '{name}'.");
}
