using System.Runtime.CompilerServices;
using PandaBear.IntegrationTesting;

namespace PandaBear.Aspire.Hosting.Umami.Tests;

/// <summary>Starts Umami, its PostgreSQL server and one website through the Aspire application model, as an AppHost would.</summary>
internal static class UmamiTestApplication
{
    /// <summary>The name of the Umami resource.</summary>
    internal const string Umami = "umami";

    /// <summary>The name of the website resource.</summary>
    internal const string Website = "site";

    /// <summary>The website's domain.</summary>
    internal const string Domain = "viajantes.example";

    private static readonly TimeSpan StartupTimeout = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Starts the application and waits until Umami answers and the website is set up. The builder is created here, in the test
    /// assembly, on the synchronous path of the call, because Aspire finds its DCP metadata by walking the call stack.
    /// </summary>
    /// <param name="ct">Cancels the start.</param>
    /// <returns>The started application.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static Task<AspireTestApplication> Start(CancellationToken ct)
    {
        var builder = AspireTestApplication.CreateBuilder();
        builder.AddUmami(Umami).AddWebsite(Website, Domain);
        return AspireTestApplication.Start(builder, [Umami, Website], StartupTimeout, ct);
    }
}
