using static PandaBear.Offerings.Tests.OfferingFixtures;

namespace PandaBear.Offerings.Tests;

/// <summary>Tests grouping a listing of offerings by year, with the offerings on request last.</summary>
[Trait(Testing.TestTraitNames.ScopeName, Testing.SharedKernelTestTraitNames.UnitScope)]
[Trait("Capability", "Offerings")]
public sealed class OfferingListingTests
{
    [Fact]
    public void Offerings_are_grouped_by_year_ascending_then_on_request_ordered_by_sort_order_start_and_undated_last()
    {
        // Arrange
        TourView[] views =
        [
            ListedView("on request", OfferingAvailability.OnRequest, 1, null, null),
            ListedView("2028 undated", OfferingAvailability.DatesToBeConfirmed, 1, null, 2028),
            ListedView("2027 late", OfferingAvailability.Scheduled, 1, new DateOnly(2027, 10, 1), null),
            ListedView("2027 sold out", OfferingAvailability.SoldOut, 1, new DateOnly(2027, 5, 1), null),
            ListedView("2027 first", OfferingAvailability.Scheduled, 0, new DateOnly(2027, 12, 1), null),
            ListedView("2028 dated", OfferingAvailability.DatesToBeConfirmed, 1, new DateOnly(2028, 2, 1), null),
            ListedView("on request first", OfferingAvailability.OnRequest, 0, null, null),
        ];

        // Act
        var groups = OfferingListing.Group(views);

        // Assert
        groups.Select(group => (group.Kind, group.Year)).ShouldBe(
            [(OfferingGroupKind.Year, 2027), (OfferingGroupKind.Year, 2028), (OfferingGroupKind.OnRequest, (int?)null)]);
        groups[0].Items.Select(view => view.Title).ShouldBe(["2027 first", "2027 sold out", "2027 late"]);
        groups[1].Items.Select(view => view.Title).ShouldBe(["2028 dated", "2028 undated"]);
        groups[2].Items.Select(view => view.Title).ShouldBe(["on request first", "on request"]);
    }

    [Fact]
    public void An_empty_listing_has_no_group_and_a_listing_without_offerings_on_request_has_no_such_group()
    {
        // Act
        var empty = OfferingListing.Group(Array.Empty<TourView>());
        var years = OfferingListing.Group([ListedView("a", OfferingAvailability.Scheduled, 0, new DateOnly(2027, 1, 1), null)]);

        // Assert
        empty.ShouldBeEmpty();
        years.ShouldHaveSingleItem().Kind.ShouldBe(OfferingGroupKind.Year);
    }

    [Fact]
    public void Application_items_are_grouped_through_their_schedule_and_sort_order()
    {
        // Arrange
        var items = new[]
        {
            (Name: "b", Order: 2, Schedule: new OfferingSchedule(OfferingAvailability.Scheduled, DateOnlyRange.Create(new DateOnly(2027, 1, 1), new DateOnly(2027, 1, 2)).Value, 2027, 2)),
            (Name: "a", Order: 1, Schedule: new OfferingSchedule(OfferingAvailability.OnRequest, null, null, 2)),
        };

        // Act
        var groups = OfferingListing.Group(items, item => item.Order, item => item.Schedule);

        // Assert
        groups.Select(group => group.Items.ShouldHaveSingleItem().Name).ShouldBe(["b", "a"]);
    }

    [Fact]
    public void Grouping_refuses_missing_arguments()
    {
        // Act
        Action noViews = () => OfferingListing.Group<Guid, TourPriceKind, TourMetric>(null!);
        Action noSortOrder = () => OfferingListing.Group(Array.Empty<int>(), null!, _ => null!);
        Action noSchedule = () => OfferingListing.Group(Array.Empty<int>(), _ => 0, null!);

        // Assert
        noViews.ShouldThrow<ArgumentNullException>();
        noSortOrder.ShouldThrow<ArgumentNullException>();
        noSchedule.ShouldThrow<ArgumentNullException>();
    }
}
