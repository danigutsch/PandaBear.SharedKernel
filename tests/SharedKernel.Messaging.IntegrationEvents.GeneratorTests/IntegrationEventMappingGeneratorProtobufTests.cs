using Microsoft.CodeAnalysis;

namespace PandaBear.Messaging.IntegrationEvents.GeneratorTests;

/// <summary>Tests that IntegrationEventMappingGenerator serializes mapped Protobuf messages as binary without JSON metadata.</summary>
public sealed class IntegrationEventMappingGeneratorProtobufTests
{
    [Fact]
    public void Mapped_protobuf_event_is_serialized_as_base64_without_json_metadata()
    {
        // Arrange
        const string source = """
            using PandaBear.Messaging.Protobuf.TestContracts;

            namespace Demo;

            public sealed record TourCreatedDomainEvent(string Name) : IDomainEvent;

            public static class TourMappings
            {
                [IntegrationEventMapping]
                public static TourCreated Map(TourCreatedDomainEvent domainEvent, Guid eventId, DateTimeOffset occurredAt) =>
                    TourCreated.Create(eventId, occurredAt, domainEvent.Name);
            }
            """;
        var compilation = GeneratorTestHarness.CreateCompilation(source);

        // Act
        var result = GeneratorTestHarness.RunGenerator(compilation);
        var generatedSource = GeneratorTestHarness.GetGeneratedSource(result.RunResult);
        var errors = result.OutputCompilation.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();

        // Assert
        errors.ShouldBeEmpty();
        generatedSource.ShouldContain("internal sealed class GeneratedIntegrationEventSerializer : global::PandaBear.Messaging.IntegrationEvents.IIntegrationEventSerializer", StringComparison.Ordinal);
        generatedSource.ShouldNotContain("JsonTypeInfo<", StringComparison.Ordinal);
        generatedSource.ShouldContain("global::System.Convert.ToBase64String(global::Google.Protobuf.MessageExtensions.ToByteArray(typed))", StringComparison.Ordinal);
        generatedSource.ShouldContain("SerializePayload<TIntegrationEvent>", StringComparison.Ordinal);
        generatedSource.ShouldContain("global::PandaBear.Messaging.EventPayloadEncoding.Base64, \"application/protobuf\"", StringComparison.Ordinal);
    }

    [Fact]
    public void Mixed_json_and_protobuf_mappings_take_json_metadata_only_for_the_json_event()
    {
        // Arrange
        const string source = """
            using PandaBear.Messaging.Protobuf.TestContracts;

            namespace Demo;

            public sealed record TourCreatedDomainEvent(string Name) : IDomainEvent;

            public sealed record OrderPlacedDomainEvent(string Number) : IDomainEvent;

            public sealed record InvoiceIssuedIntegrationEvent(Guid EventId, DateTimeOffset OccurredAt) : IIntegrationEvent
            {
                public static string EventType => "invoice.issued";
                public static int EventVersion => 1;
            }

            public static class Mappings
            {
                [IntegrationEventMapping]
                public static TourCreated Map(TourCreatedDomainEvent domainEvent, Guid eventId, DateTimeOffset occurredAt) =>
                    TourCreated.Create(eventId, occurredAt, domainEvent.Name);

                [IntegrationEventMapping]
                public static InvoiceIssuedIntegrationEvent Map(OrderPlacedDomainEvent domainEvent, Guid eventId, DateTimeOffset occurredAt) =>
                    new(eventId, occurredAt);
            }
            """;
        var compilation = GeneratorTestHarness.CreateCompilation(source);

        // Act
        var result = GeneratorTestHarness.RunGenerator(compilation);
        var generatedSource = GeneratorTestHarness.GetGeneratedSource(result.RunResult);
        var errors = result.OutputCompilation.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();

        // Assert
        errors.ShouldBeEmpty();
        generatedSource.ShouldContain("JsonTypeInfo<global::Demo.InvoiceIssuedIntegrationEvent>", StringComparison.Ordinal);
        generatedSource.ShouldNotContain("JsonTypeInfo<global::PandaBear.Messaging.Protobuf.TestContracts.TourCreated>", StringComparison.Ordinal);
        generatedSource.ShouldContain("ToByteArray(typed)", StringComparison.Ordinal);
        generatedSource.ShouldContain("JsonSerializer.Serialize(typed, jsonTypeInfo", StringComparison.Ordinal);
        generatedSource.ShouldContain("global::PandaBear.Messaging.EventPayloadEncoding.Json, \"application/json\"", StringComparison.Ordinal);
    }

    [Fact]
    public void Explicit_json_registration_keeps_a_mapped_protobuf_event_on_json()
    {
        // Arrange
        const string source = """
            using Microsoft.Extensions.DependencyInjection;
            using PandaBear.Messaging.Protobuf.TestContracts;

            namespace Demo;

            public sealed record TourCreatedDomainEvent(string Name) : IDomainEvent;

            public static class Mappings
            {
                [IntegrationEventMapping]
                public static TourCreated Map(TourCreatedDomainEvent domainEvent, Guid eventId, DateTimeOffset occurredAt) =>
                    TourCreated.Create(eventId, occurredAt, domainEvent.Name);

                public static IServiceCollection AddMessaging(IServiceCollection services) =>
                    services.AddIntegrationEventContract<TourCreated>(TourCreated.EventType, null!);
            }
            """;
        var compilation = GeneratorTestHarness.CreateCompilation(source);

        // Act
        var runResult = GeneratorTestHarness.RunGeneratorDriver(compilation);
        var generatedSource = GeneratorTestHarness.GetGeneratedSource(runResult);

        // Assert
        generatedSource.ShouldContain("JsonTypeInfo<global::PandaBear.Messaging.Protobuf.TestContracts.TourCreated>", StringComparison.Ordinal);
        generatedSource.ShouldNotContain("ToByteArray", StringComparison.Ordinal);
        generatedSource.ShouldNotContain("SerializePayload", StringComparison.Ordinal);
    }

    [Fact]
    public void Protobuf_and_json_contracts_with_one_event_type_still_fail_the_duplicate_check()
    {
        // Arrange
        const string source = """
            using PandaBear.Messaging.Protobuf.TestContracts;

            namespace Demo;

            public sealed record TourCreatedDomainEvent(string Name) : IDomainEvent;

            public sealed record LegacyTourCreatedIntegrationEvent(Guid EventId, DateTimeOffset OccurredAt) : IIntegrationEvent
            {
                public static string EventType => "test.tour.created";
                public static int EventVersion => 1;
            }

            public static class Mappings
            {
                [IntegrationEventMapping]
                public static TourCreated Map(TourCreatedDomainEvent domainEvent, Guid eventId, DateTimeOffset occurredAt) =>
                    TourCreated.Create(eventId, occurredAt, domainEvent.Name);

                [IntegrationEventMapping]
                public static LegacyTourCreatedIntegrationEvent MapLegacy(TourCreatedDomainEvent domainEvent, Guid eventId, DateTimeOffset occurredAt) =>
                    new(eventId, occurredAt);
            }
            """;
        var compilation = GeneratorTestHarness.CreateCompilation(source);

        // Act
        var runResult = GeneratorTestHarness.RunGeneratorDriver(compilation);
        var generatedSource = GeneratorTestHarness.GetGeneratedSource(runResult);

        // Assert
        generatedSource.ShouldContain("declare duplicate event type", StringComparison.Ordinal);
    }
}
