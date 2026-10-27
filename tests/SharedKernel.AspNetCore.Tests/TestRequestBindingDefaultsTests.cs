using System.Net;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using PandaBear.Testing;

namespace PandaBear.AspNetCore.Tests;

/// <summary>Tests the default request-binding behavior that <c>AddTestRequestBindingDefaults</c> configures for test hosts.</summary>
[Trait(TestTraitNames.CategoryName, TestTraitValues.EndpointCategory)]
[Trait(TestTraitNames.ScopeName, SharedKernelTestTraitNames.UnitScope)]
public sealed class TestRequestBindingDefaultsTests
{
    [Theory]
    [InlineData("Development", false, HttpStatusCode.BadRequest)]
    [InlineData("Production", false, HttpStatusCode.BadRequest)]
    [InlineData("Development", true, HttpStatusCode.InternalServerError)]
    public static async Task Invalid_bodies_are_rejected_before_execution_with_explicit_diagnostic_opt_out(
        string environment, bool enableDiagnosticExceptions, HttpStatusCode expected)
    {
        // Arrange
        ArgumentException.ThrowIfNullOrWhiteSpace(environment);
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });
        builder.WebHost.UseTestServer();
        builder.Services.AddProblemDetails();
        builder.Services.AddTestRequestBindingDefaults();
        if (enableDiagnosticExceptions)
        {
            builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);
        }
        await using var app = builder.Build();
        app.UseExceptionHandler();
        var calls = 0;
        app.MapPost("/", (int[] values) => { calls++; return Microsoft.AspNetCore.Http.Results.Ok(values.Length); });
        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = app.GetTestClient();
        using var body = new StringContent("{", Encoding.UTF8, "application/json");

        // Act
        using var response = await client.PostAsync(new Uri("/", UriKind.Relative), body, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(expected);
        calls.ShouldBe(0);
    }

    [Fact]
    public static void Null_service_collections_are_rejected()
    {
        // Arrange
        IServiceCollection services = null!;

        // Act
        var configure = () => services.AddTestRequestBindingDefaults();

        // Assert
        configure.ShouldThrow<ArgumentNullException>();
    }
}
