using SharedKernel.Testing;

namespace SharedKernel.EntityFrameworkCore.Tests;

[Trait(SharedKernelTestTraitNames.CategoryName, TestTraits.CoreBehaviorCategory)]
[Trait(SharedKernelTestTraitNames.CapabilityName, TestTraits.IntegrationEventTransportCapability)]
public sealed class ControlledEventEnvelopePublisherTests
{
    [Fact]
    public void Dispose_counts_each_invocation()
    {
        // Arrange
        var publisher = new ControlledEventEnvelopePublisher();

        // Act
        publisher.Dispose();
        publisher.Dispose();

        // Assert
        publisher.DisposeCount.ShouldBe(2);
    }
}
