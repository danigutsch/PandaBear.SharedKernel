using SharedKernel.Testing.Assertions;

namespace SharedKernel.Messaging.IntegrationEvents.GeneratorTests;

public sealed class IntegrationEventMappingGeneratorTests
{
    [Fact]
    public void Generates_domain_dispatcher_for_mapping_methods()
    {
        const string source = """
            namespace Demo;

            public sealed record TourCreatedDomainEvent(Guid TourId) : IDomainEvent;

            public sealed record TourCreatedIntegrationEvent(Guid EventId, DateTimeOffset OccurredAt, Guid TourId) : IIntegrationEvent
            {
                public static string EventType => "tour.created";

                public static int EventVersion => 1;
            }

            public static class TourMappings
            {
                [IntegrationEventMapping]
                public static TourCreatedIntegrationEvent Map(TourCreatedDomainEvent domainEvent, Guid eventId, DateTimeOffset occurredAt) =>
                    new(eventId, occurredAt, domainEvent.TourId);
            }
            """;
        var compilation = GeneratorTestHarness.CreateCompilation(source);

        var runResult = GeneratorTestHarness.RunGeneratorDriver(compilation);
        var generatedSource = GeneratorTestHarness.GetGeneratedSource(runResult);

        generatedSource.ShouldContain("GeneratedIntegrationEventDomainEventDispatcher", StringComparison.Ordinal);
        generatedSource.ShouldContain("Dispatch(global::SharedKernel.Domain.IDomainEvent domainEvent", StringComparison.Ordinal);
        generatedSource.ShouldContain("Demo.TourCreatedDomainEvent typedDomainEvent", StringComparison.Ordinal);
        generatedSource.ShouldContain("outbox.Enqueue(", StringComparison.Ordinal);
        generatedSource.ShouldContain("IDomainEventIntegrationEventOutbox outbox", StringComparison.Ordinal);
        generatedSource.ShouldContain("TryAddSingleton<global::SharedKernel.DomainEvents.IDomainEventDispatcher", StringComparison.Ordinal);
        generatedSource.ShouldNotContain("IDomainEventHandler<", StringComparison.Ordinal);
    }

    [Fact]
    public void Ignores_invalid_mapping_methods()
    {
        const string source = """
            namespace Demo;

            public sealed record TourCreatedDomainEvent(Guid TourId) : IDomainEvent;

            public sealed record TourCreatedIntegrationEvent(Guid EventId, DateTimeOffset OccurredAt, Guid TourId) : IIntegrationEvent
            {
                public static string EventType => "tour.created";

                public static int EventVersion => 1;
            }

            public static class TourMappings
            {
                [IntegrationEventMapping]
                public static TourCreatedIntegrationEvent Map(TourCreatedDomainEvent domainEvent, Guid eventId) =>
                    new(eventId, DateTimeOffset.UtcNow, domainEvent.TourId);
            }
            """;
        var compilation = GeneratorTestHarness.CreateCompilation(source);

        var runResult = GeneratorTestHarness.RunGeneratorDriver(compilation);

        runResult.Results.Single().GeneratedSources.ShouldBeEmpty();
    }

    [Fact]
    public void Ignores_private_mapping_methods()
    {
        const string source = """
            namespace Demo;

            public sealed record TourCreatedDomainEvent(Guid TourId) : IDomainEvent;

            public sealed record TourCreatedIntegrationEvent(Guid EventId, DateTimeOffset OccurredAt, Guid TourId) : IIntegrationEvent
            {
                public static string EventType => "tour.created";

                public static int EventVersion => 1;
            }

            public static class TourMappings
            {
                [IntegrationEventMapping]
                private static TourCreatedIntegrationEvent Map(TourCreatedDomainEvent domainEvent, Guid eventId, DateTimeOffset occurredAt) =>
                    new(eventId, occurredAt, domainEvent.TourId);
            }
            """;
        var compilation = GeneratorTestHarness.CreateCompilation(source);

        var runResult = GeneratorTestHarness.RunGeneratorDriver(compilation);

        runResult.Results.Single().GeneratedSources.ShouldBeEmpty();
    }
}
