using SharedKernel.Testing;

namespace SharedKernel.EntityFrameworkCore.Tests;

[Trait(SharedKernelTestTraitNames.CategoryName, TestTraits.CoreBehaviorCategory)]
[Trait(SharedKernelTestTraitNames.CapabilityName, TestTraits.IdempotencyCapability)]
public sealed class PostgreSqlScenarioCleanupTests
{
    [Fact]
    public async Task Setup_failure_cleanup_attempts_every_resource_and_preserves_failures()
    {
        // Arrange
        var operationFailure = new InvalidOperationException("setup failed");
        var cleanupFailure = new IOException("cleanup failed");
        var disposalOrder = new List<string>();
        await using var failingResource = new TrackingAsyncDisposable("first", disposalOrder, cleanupFailure);
        await using var succeedingResource = new TrackingAsyncDisposable("second", disposalOrder);

        // Act
        Func<Task> disposeResources = () => PostgreSqlScenarioCleanup.DisposeResources(
            operationFailure,
            failingResource,
            succeedingResource);
        var exception = await disposeResources.ShouldThrow<AggregateException>();

        // Assert
        failingResource.DisposeCalled.ShouldBeTrue();
        succeedingResource.DisposeCalled.ShouldBeTrue();
        disposalOrder.ShouldBe(["first", "second"]);
        exception.InnerExceptions.Count.ShouldBe(2);
        exception.InnerExceptions[0].ShouldBeSameAs(operationFailure);
        exception.InnerExceptions[1].ShouldBeSameAs(cleanupFailure);
    }
}
