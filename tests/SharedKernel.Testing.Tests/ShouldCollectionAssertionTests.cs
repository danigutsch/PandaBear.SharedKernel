namespace SharedKernel.Testing.Tests;

public sealed class ShouldCollectionAssertionTests
{
    [Fact]
    public void ShouldBeEquivalentTo_ignores_order_and_preserves_multiplicity()
    {
        // Arrange
        string[] actual = ["second", "first", "first"];

        // Act
        actual.ShouldBeEquivalentTo("first", "second", "first");

        // Assert
        actual.ShouldHaveCount(3);
    }

    [Fact]
    public void ShouldBeEquivalentTo_rejects_different_multiplicity()
    {
        // Arrange
        string[] actual = ["first"];
        Action assertion = () => actual.ShouldBeEquivalentTo("first", "first");

        // Act
        var exception = assertion.ShouldThrow<Xunit.Sdk.EquivalentException>();

        // Assert
        exception.Message.ShouldNotBeEmpty();
    }

}
