using System.Text.Json;

namespace SharedKernel.Blazor.Tests;

/// <summary>Tests the bounded slice of a list that a search or a page returns.</summary>
[Testing.TestScope(Testing.TestScope.Unit)]
public sealed class ListSliceTests
{
    [Fact]
    public void A_slice_keeps_its_items_and_the_total_number_of_matches()
    {
        // Arrange & Act
        var slice = new ListSlice<string>(["a", "b"], 12);

        // Assert
        slice.Items.ShouldBe(["a", "b"]);
        slice.Total.ShouldBe(12);
    }

    [Fact]
    public void A_slice_round_trips_through_JSON_so_a_bounded_page_can_be_persisted()
    {
        // Arrange
        var slice = new ListSlice<string>(["a", "b"], 12);

        // Act
        var copy = JsonSerializer.Deserialize<ListSlice<string>>(JsonSerializer.Serialize(slice, JsonSerializerOptions.Web), JsonSerializerOptions.Web);

        // Assert
        copy.ShouldNotBeNull();
        copy.Items.ShouldBe(["a", "b"]);
        copy.Total.ShouldBe(12);
    }

    [Fact]
    public void A_total_below_the_number_of_items_or_missing_items_are_rejected()
    {
        // Arrange
        var noItems = () => new ListSlice<string>(null!, 0);
        var smallTotal = () => new ListSlice<string>(["a", "b"], 1);
        var negativeTotal = () => new ListSlice<string>([], -1);

        // Act & Assert
        noItems.ShouldThrow<ArgumentNullException>();
        smallTotal.ShouldThrow<ArgumentOutOfRangeException>();
        negativeTotal.ShouldThrow<ArgumentOutOfRangeException>();
    }
}
