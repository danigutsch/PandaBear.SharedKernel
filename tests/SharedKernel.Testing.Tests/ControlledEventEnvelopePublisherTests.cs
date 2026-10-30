using PandaBear.Testing.Messaging;

namespace PandaBear.Testing.Tests;

/// <summary>Tests the shared controlled envelope publisher.</summary>
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
