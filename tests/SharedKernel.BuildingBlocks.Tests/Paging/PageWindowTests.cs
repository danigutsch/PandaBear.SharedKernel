namespace PandaBear.BuildingBlocks.Tests;

/// <summary>Tests the page window computed from a requested page number in the URL.</summary>
[Trait(Testing.TestTraitNames.ScopeName, Testing.SharedKernelTestTraitNames.UnitScope)]
[Trait("Capability", "Paging")]
public sealed class PageWindowTests
{
    [Theory]
    [InlineData(null, 1, false)]
    [InlineData("", 1, true)]
    [InlineData("abc", 1, true)]
    [InlineData("2.5", 1, true)]
    [InlineData(" 3", 1, true)]
    [InlineData("+3", 1, true)]
    [InlineData("0", 1, true)]
    [InlineData("-3", 1, true)]
    [InlineData("1", 1, false)]
    [InlineData("3", 3, false)]
    [InlineData("003", 3, false)]
    [InlineData("10", 10, false)]
    [InlineData("11", 10, true)]
    [InlineData("99999999999999999999", 10, true)]
    public void The_requested_page_text_is_clamped_to_the_pages_that_exist_and_a_page_that_does_not_exist_is_flagged(string? requested, int expected, bool outOfRange)
    {
        // Arrange & Act
        var window = PageWindow.Create(requested, 10, 95);

        // Assert
        window.Page.ShouldBe(expected);
        window.PageCount.ShouldBe(10);
        window.IsOutOfRange.ShouldBe(outOfRange);
    }

    [Theory]
    [InlineData(null, 1, false)]
    [InlineData(int.MinValue, 1, true)]
    [InlineData(0, 1, true)]
    [InlineData(4, 4, false)]
    [InlineData(11, 10, true)]
    [InlineData(int.MaxValue, 10, true)]
    public void The_requested_page_number_is_clamped_to_the_pages_that_exist_and_a_page_that_does_not_exist_is_flagged(int? requested, int expected, bool outOfRange)
    {
        // Arrange & Act
        var window = PageWindow.Create(requested, 10, 95);

        // Assert
        window.Page.ShouldBe(expected);
        window.IsOutOfRange.ShouldBe(outOfRange);
    }

    [Fact]
    public void Page_two_of_an_empty_list_is_out_of_range_while_page_one_is_not()
    {
        // Arrange & Act
        var first = PageWindow.Create(1, 10, 0);
        var second = PageWindow.Create(2, 10, 0);

        // Assert
        first.IsOutOfRange.ShouldBeFalse();
        second.IsOutOfRange.ShouldBeTrue();
        second.Page.ShouldBe(1);
    }

    [Fact]
    public void A_middle_page_skips_the_earlier_pages_and_links_both_neighbours()
    {
        // Arrange & Act
        var window = PageWindow.Create(3, 10, 95);

        // Assert
        window.PageSize.ShouldBe(10);
        window.Total.ShouldBe(95);
        window.Skip.ShouldBe(20);
        window.Take.ShouldBe(10);
        window.PreviousPage.ShouldBe(2);
        window.NextPage.ShouldBe(4);
        window.HasMultiplePages.ShouldBeTrue();
    }

    [Fact]
    public void The_first_page_has_no_previous_page_and_the_last_page_has_no_next_page()
    {
        // Arrange & Act
        var first = PageWindow.Create(1, 10, 95);
        var last = PageWindow.Create(10, 10, 95);

        // Assert
        first.PreviousPage.ShouldBeNull();
        first.NextPage.ShouldBe(2);
        first.Skip.ShouldBe(0);
        last.PreviousPage.ShouldBe(9);
        last.NextPage.ShouldBeNull();
        last.Skip.ShouldBe(90);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(10, 1)]
    [InlineData(11, 2)]
    [InlineData(100, 10)]
    public void The_page_count_covers_every_item_and_an_empty_list_has_one_page(int total, int expected)
    {
        // Arrange & Act
        var window = PageWindow.Create(5, 10, total);

        // Assert
        window.PageCount.ShouldBe(expected);
        window.HasMultiplePages.ShouldBe(expected > 1);
    }

    [Fact]
    public void An_empty_list_shows_its_only_page_without_neighbours()
    {
        // Arrange & Act
        var window = PageWindow.Create("7", 10, 0);

        // Assert
        window.Page.ShouldBe(1);
        window.Skip.ShouldBe(0);
        window.PreviousPage.ShouldBeNull();
        window.NextPage.ShouldBeNull();
    }

    [Fact]
    public void The_largest_total_does_not_overflow()
    {
        // Arrange & Act
        var window = PageWindow.Create(int.MaxValue, 1, int.MaxValue);
        var large = PageWindow.Create(int.MaxValue, int.MaxValue, int.MaxValue);

        // Assert
        window.PageCount.ShouldBe(int.MaxValue);
        window.Skip.ShouldBe(int.MaxValue - 1);
        large.PageCount.ShouldBe(1);
    }

    [Fact]
    public void A_non_positive_page_size_or_a_negative_total_is_rejected()
    {
        // Arrange
        Action zeroSize = () => PageWindow.Create(1, 0, 10);
        Action negativeSize = () => PageWindow.Create("1", -1, 10);
        Action negativeTotal = () => PageWindow.Create(1, 10, -1);

        // Act & Assert
        zeroSize.ShouldThrow<ArgumentOutOfRangeException>();
        negativeSize.ShouldThrow<ArgumentOutOfRangeException>();
        negativeTotal.ShouldThrow<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Windows_with_the_same_values_are_equal()
    {
        // Arrange & Act
        var first = PageWindow.Create("2", 10, 95);
        var second = PageWindow.Create(2, 10, 95);

        // Assert
        first.ShouldBe(second);
    }
}
