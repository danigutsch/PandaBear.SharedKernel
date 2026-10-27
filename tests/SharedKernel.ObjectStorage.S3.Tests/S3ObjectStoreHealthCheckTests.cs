using System.Net;
using Amazon.Runtime;
using Amazon.S3;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace PandaBear.ObjectStorage.S3.Tests;

/// <summary>Tests for the S3 object-store health check, covering readiness, failure reporting, and registration.</summary>
public sealed class S3ObjectStoreHealthCheckTests
{
    [Fact]
    public async Task Check_reports_healthy_when_the_bucket_lists()
    {
        // Arrange
        using var client = new FakeAmazonS3Client();
        var check = S3ObjectStoreTestFactory.CreateHealthCheck(client);

        // Act
        var result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        // Assert
        result.Status.ShouldBe(HealthStatus.Healthy);
        client.Operations.ShouldBe(["ListObjectsV2:media//"]);
    }

    [Fact]
    public async Task Check_reports_unhealthy_without_leaking_provider_details_when_the_bucket_is_unreachable()
    {
        // Arrange
        using var client = new FakeAmazonS3Client
        {
            ListObjectsException = new AmazonS3Exception("http://seaweedfs:8333/media secret-detail", ErrorType.Sender, "NoSuchBucket", "request-id", HttpStatusCode.NotFound)
        };
        var check = S3ObjectStoreTestFactory.CreateHealthCheck(client);

        // Act
        var result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        // Assert
        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Description.ShouldBe("Object storage is unavailable.");
        result.Exception.ShouldBeNull();
    }

    [Fact]
    public async Task Check_propagates_cancellation()
    {
        // Arrange
        using var client = new FakeAmazonS3Client { ListObjectsException = new OperationCanceledException() };
        var check = S3ObjectStoreTestFactory.CreateHealthCheck(client);

        // Act
        Func<Task> act = () => check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        // Assert
        await act.ShouldThrowAssignableTo<OperationCanceledException>();
    }

    [Fact]
    public async Task AddS3ObjectStoreHealthCheck_registers_a_check_that_runs_through_health_check_service()
    {
        // Arrange
        using var client = new FakeAmazonS3Client();
        var services = new ServiceCollection().AddLogging();
        services.AddSingleton<IAmazonS3>(client);
        services.AddSingleton(Options.Create(S3ObjectStoreOptionsTestFactory.CreateValidOptions()));
        services.AddHealthChecks().AddS3ObjectStoreHealthCheck();
        await using var provider = services.BuildServiceProvider();

        // Act
        var report = await provider.GetRequiredService<HealthCheckService>().CheckHealthAsync(TestContext.Current.CancellationToken);

        // Assert
        report.Entries.Keys.ShouldBe(["object-storage"]);
        report.Status.ShouldBe(HealthStatus.Healthy);
    }
}
