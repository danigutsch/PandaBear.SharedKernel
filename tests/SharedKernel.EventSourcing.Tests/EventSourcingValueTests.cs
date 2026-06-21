namespace SharedKernel.EventSourcing.Tests;

public sealed class EventSourcingValueTests
{
    [Fact]
    public void StreamId_From_Trims_Value()
    {
        // Arrange
        const string value = " catalog-tour-tour-1 ";

        // Act
        var streamId = StreamId.From(value);

        // Assert
        Assert.Equal("catalog-tour-tour-1", streamId.Value);
        Assert.Equal("catalog-tour-tour-1", streamId.ToString());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void StreamRevision_From_Rejects_Non_Positive_Values(long value)
    {
        // Arrange, Act, Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => StreamRevision.From(value));
    }

    [Fact]
    public void ExpectedStreamRevision_From_Represents_Specific_Revision()
    {
        // Arrange
        var revision = StreamRevision.From(3);

        // Act
        var expectedRevision = ExpectedStreamRevision.From(revision);

        // Assert
        Assert.Equal(3, expectedRevision.Value);
        Assert.False(expectedRevision.RequiresEmptyStream);
    }
}
