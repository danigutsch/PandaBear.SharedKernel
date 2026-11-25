using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;

namespace SharedKernel.ServiceDefaults.Tests;

/// <summary>Sends one request through a client of an application with the service defaults to a server that fails transiently.</summary>
internal static class TransientRequest
{
    private const string ClientName = "transient";

    /// <summary>Sends one request and reports the final status and how many attempts reached the server.</summary>
    /// <param name="method">The HTTP method.</param>
    /// <param name="failures">The number of attempts the server answers with 503 before it answers 200.</param>
    /// <param name="configureResilience">The value of <see cref="ServiceDefaultsOptions.ConfigureResilience"/>.</param>
    /// <returns>The final status and the number of attempts.</returns>
    internal static async Task<(HttpStatusCode Status, int Attempts)> Send(
        HttpMethod method,
        int failures,
        Action<HttpStandardResilienceOptions>? configureResilience = null)
    {
        using var handler = new TransientHandler(failures);
        await using var app = DefaultsApplication.Build(
            "Production",
            options => options.ConfigureResilience = configureResilience,
            builder => builder.Services.AddHttpClient(ClientName, client => client.BaseAddress = new Uri("https://service.example.test"))
                .ConfigurePrimaryHttpMessageHandler(() => handler));
        using var client = app.Services.GetRequiredService<IHttpClientFactory>().CreateClient(ClientName);
        using var request = new HttpRequestMessage(method, new Uri("/resource", UriKind.Relative));

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        return (response.StatusCode, handler.Attempts);
    }
}
