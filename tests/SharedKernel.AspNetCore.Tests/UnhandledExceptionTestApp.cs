using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace PandaBear.AspNetCore.Tests;

/// <summary>Starts a web application whose only route throws an exception that carries a sensitive message.</summary>
internal static class UnhandledExceptionTestApp
{
    /// <summary>The exception message that must never be exposed.</summary>
    public const string SensitiveMessage = "secret-connection-string password=hunter2";

    /// <summary>Starts the application with the unhandled-exception handler registered.</summary>
    /// <param name="meters">The meter factory and collector.</param>
    /// <param name="logger">The logger that records reporter output.</param>
    /// <returns>The started application.</returns>
    public static async Task<WebApplication> Start(MeterCollector meters, RecordingLogger<UnhandledExceptionReporter> logger)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton<IMeterFactory>(meters);
        builder.Services.AddSingleton<ILogger<UnhandledExceptionReporter>>(logger);
        builder.Services.AddUnhandledExceptionHandler();
        var app = builder.Build();
        app.UseExceptionHandler();
        app.MapGet("/orders/{id}", string (string id) => throw new InvalidOperationException(SensitiveMessage));
        app.MapPost("/upload", string () => throw new BadHttpRequestException(SensitiveMessage, StatusCodes.Status413PayloadTooLarge));
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }
}
