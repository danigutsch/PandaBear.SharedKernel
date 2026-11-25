using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SharedKernel.EntityFrameworkCore.Sqlite.Tests;

/// <summary>Tests that ordered lists stored as JSON text columns round-trip and are change-tracked by content.</summary>
[Trait("Scope", "Integration")]
[Trait("Capability", "JsonColumns")]
public sealed class JsonColumnTests
{
    private static readonly Guid FirstImage = Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e");
    private static readonly Guid SecondImage = Guid.Parse("7c9e6679-7425-40de-944b-e07fc1f90ae7");

    [Fact]
    public async Task Lists_of_strings_numbers_IDs_and_records_round_trip_in_order()
    {
        // Arrange
        using var database = new ItineraryDatabase();
        var itinerary = JsonColumnSamples.Full();
        await database.Store(itinerary);
        await using var context = database.CreateContext();

        // Act
        var loaded = await context.Itineraries.SingleAsync(TestContext.Current.CancellationToken);

        // Assert
        loaded.Cities.ShouldBe(["Lisboa", "São Paulo"]);
        loaded.Prices.ShouldBe([1250.50m, 99m]);
        loaded.ImageIds.ShouldBe([FirstImage, SecondImage]);
        loaded.Stops.ShouldBe([new ItineraryStop(1, "Arrival"), new ItineraryStop(2, "Ride")]);
    }

    [Fact]
    public async Task The_column_holds_JSON_with_web_defaults()
    {
        // Arrange
        using var database = new ItineraryDatabase();
        var itinerary = JsonColumnSamples.Full();
        await database.Store(itinerary);

        // Act
        var stored = await database.Stored(itinerary.Id);

        // Assert
        stored.Cities.ShouldBe("[\"Lisboa\",\"S\\u00E3o Paulo\"]");
        stored.Prices.ShouldBe("[1250.50,99]");
        stored.ImageIds.ShouldBe($"[\"{FirstImage}\",\"{SecondImage}\"]");
        stored.Stops.ShouldBe("[{\"dayNumber\":1,\"title\":\"Arrival\"},{\"dayNumber\":2,\"title\":\"Ride\"}]");
    }

    [Fact]
    public async Task An_empty_list_is_stored_as_an_empty_array_in_a_required_column()
    {
        // Arrange
        using var database = new ItineraryDatabase();
        var itinerary = new Itinerary();
        await database.Store(itinerary);
        await using var context = database.CreateContext();

        // Act
        var stored = await database.Stored(itinerary.Id);
        var nullable = context.Model.FindEntityType(typeof(Itinerary))!.GetProperties().Where(property => !property.IsPrimaryKey()).Select(property => property.IsNullable);

        // Assert
        stored.ShouldBe(new StoredItinerary("[]", "[]", "[]", "[]"));
        nullable.ShouldNotContain(true);
    }

    [Fact]
    public async Task Assigning_an_equal_list_is_not_a_change()
    {
        // Arrange
        using var database = new ItineraryDatabase();
        await database.Store(JsonColumnSamples.Full());
        await using var context = database.CreateContext();
        var loaded = await context.Itineraries.SingleAsync(TestContext.Current.CancellationToken);
        loaded.Cities = ["Lisboa", "São Paulo"];
        loaded.Stops = [new ItineraryStop(1, "Arrival"), new ItineraryStop(2, "Ride")];

        // Act
        context.ChangeTracker.DetectChanges();

        // Assert
        context.Entry(loaded).State.ShouldBe(EntityState.Unchanged);
    }

    [Fact]
    public async Task A_reordered_list_is_saved()
    {
        // Arrange
        using var database = new ItineraryDatabase();
        var itinerary = JsonColumnSamples.Full();
        await database.Store(itinerary);
        await using (var context = database.CreateContext())
        {
            var loaded = await context.Itineraries.SingleAsync(TestContext.Current.CancellationToken);
            loaded.Cities = ["São Paulo", "Lisboa"];

            // Act
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Assert
        await using var check = database.CreateContext();
        var saved = await check.Itineraries.SingleAsync(TestContext.Current.CancellationToken);
        saved.Cities.ShouldBe(["São Paulo", "Lisboa"]);
    }

    [Fact]
    public async Task A_change_made_in_place_to_the_loaded_list_is_detected()
    {
        // Arrange
        using var database = new ItineraryDatabase();
        await database.Store(JsonColumnSamples.Full());
        await using var context = database.CreateContext();
        var loaded = await context.Itineraries.SingleAsync(TestContext.Current.CancellationToken);
        var cities = (List<string>)loaded.Cities;

        // Act
        cities.Add("Porto");
        context.ChangeTracker.DetectChanges();

        // Assert
        context.Entry(loaded).Property(entity => entity.Cities).IsModified.ShouldBeTrue();
    }

    [Fact]
    public void A_null_builder_is_rejected()
    {
        // Arrange
        Action map = () => JsonColumnPropertyBuilderExtensions.HasJsonConversion<string>(null!);

        // Act & Assert
        map.ShouldThrow<ArgumentNullException>();
    }

    [Fact]
    public void The_builder_is_returned_for_chaining()
    {
        // Arrange
        var builder = new ModelBuilder().Entity<Itinerary>().Property(entity => entity.Cities);

        // Act
        PropertyBuilder<IReadOnlyList<string>> returned = builder.HasJsonConversion();

        // Assert
        returned.ShouldBeSameAs(builder);
    }
}
