using Microsoft.CodeAnalysis;

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

        var result = GeneratorTestHarness.RunGenerator(compilation);
        var generatedSource = GeneratorTestHarness.GetGeneratedSource(result.RunResult);
        var errors = result.OutputCompilation.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();

        errors.ShouldBeEmpty();
        generatedSource.ShouldContain("GeneratedIntegrationEventDomainEventDispatcher", StringComparison.Ordinal);
        generatedSource.ShouldContain("Handle(global::SharedKernel.Domain.IDomainEvent domainEvent", StringComparison.Ordinal);
        generatedSource.ShouldContain("Demo.TourCreatedDomainEvent typedDomainEvent", StringComparison.Ordinal);
        generatedSource.ShouldContain("outbox.Enqueue(", StringComparison.Ordinal);
        generatedSource.ShouldContain("IDomainEventIntegrationEventOutbox outbox", StringComparison.Ordinal);
        generatedSource.ShouldContain("CompositeDomainEventDispatcher", StringComparison.Ordinal);
        generatedSource.ShouldContain("TryAddEnumerable", StringComparison.Ordinal);
        generatedSource.ShouldContain("TryAddScoped<global::SharedKernel.DomainEvents.IDomainEventDispatcher", StringComparison.Ordinal);
        generatedSource.ShouldNotContain("TryAddSingleton<global::SharedKernel.DomainEvents.IDomainEventDispatcher", StringComparison.Ordinal);
        generatedSource.ShouldContain("ServiceDescriptor.Scoped<global::SharedKernel.DomainEvents.IDomainEventDispatchHandler", StringComparison.Ordinal);
        generatedSource.ShouldContain("IDomainEventDispatchHandler", StringComparison.Ordinal);
        generatedSource.ShouldNotContain("IDomainEventHandler<", StringComparison.Ordinal);
    }

    [Fact]
    public void Reports_invalid_mapping_methods()
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
        var diagnostics = runResult.Diagnostics
            .Where(static diagnostic => diagnostic.Id == "INTEGRATIONEVENT001")
            .ToArray();

        runResult.Results.Single().GeneratedSources.ShouldBeEmpty();
        diagnostics.Length.ShouldBe(1);
    }

    [Fact]
    public void Reports_private_mapping_methods()
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
        var diagnostics = runResult.Diagnostics
            .Where(static diagnostic => diagnostic.Id == "INTEGRATIONEVENT001")
            .ToArray();

        runResult.Results.Single().GeneratedSources.ShouldBeEmpty();
        diagnostics.Length.ShouldBe(1);
    }

    [Fact]
    public void Reports_static_virtual_interface_mapping_methods()
    {
        const string source = """
            namespace Demo;

            public sealed record TourCreatedDomainEvent(Guid TourId) : IDomainEvent;

            public sealed record TourCreatedIntegrationEvent(Guid EventId, DateTimeOffset OccurredAt, Guid TourId) : IIntegrationEvent
            {
                public static string EventType => "tour.created";
                public static int EventVersion => 1;
            }

            public interface ITourMappings
            {
                [IntegrationEventMapping]
                static virtual TourCreatedIntegrationEvent Map(
                    TourCreatedDomainEvent domainEvent,
                    Guid eventId,
                    DateTimeOffset occurredAt) => new(eventId, occurredAt, domainEvent.TourId);
            }
            """;
        var compilation = GeneratorTestHarness.CreateCompilation(source);

        var runResult = GeneratorTestHarness.RunGeneratorDriver(compilation);
        var diagnostics = runResult.Diagnostics
            .Where(static diagnostic => diagnostic.Id == "INTEGRATIONEVENT001")
            .ToArray();

        runResult.Results.Single().GeneratedSources.ShouldBeEmpty();
        diagnostics.Length.ShouldBe(1);
    }

    [Fact]
    public void Dispatches_base_and_derived_event_mappings_by_exact_runtime_type()
    {
        const string source = """
            namespace Demo;

            public record BaseDomainEvent(Guid TourId) : IDomainEvent;
            public sealed record DerivedDomainEvent(Guid TourId) : BaseDomainEvent(TourId);

            public sealed record BaseIntegrationEvent(Guid EventId, DateTimeOffset OccurredAt, Guid TourId) : IIntegrationEvent
            {
                public static string EventType => "tour.base";
                public static int EventVersion => 1;
            }

            public sealed record DerivedIntegrationEvent(Guid EventId, DateTimeOffset OccurredAt, Guid TourId) : IIntegrationEvent
            {
                public static string EventType => "tour.derived";
                public static int EventVersion => 1;
            }

            public static class TourMappings
            {
                [IntegrationEventMapping]
                public static BaseIntegrationEvent MapBase(BaseDomainEvent domainEvent, Guid eventId, DateTimeOffset occurredAt) =>
                    new(eventId, occurredAt, domainEvent.TourId);

                [IntegrationEventMapping]
                public static DerivedIntegrationEvent MapDerived(DerivedDomainEvent domainEvent, Guid eventId, DateTimeOffset occurredAt) =>
                    new(eventId, occurredAt, domainEvent.TourId);
            }
            """;
        var compilation = GeneratorTestHarness.CreateCompilation(source);

        var runResult = GeneratorTestHarness.RunGeneratorDriver(compilation);
        var generatedSource = GeneratorTestHarness.GetGeneratedSource(runResult);

        generatedSource.ShouldContain("when domainEvent.GetType() == typeof(global::Demo.BaseDomainEvent)", StringComparison.Ordinal);
        generatedSource.ShouldContain("when domainEvent.GetType() == typeof(global::Demo.DerivedDomainEvent)", StringComparison.Ordinal);
    }

    [Fact]
    public void Emits_global_escaped_identifiers()
    {
        const string source = """
            namespace @event;

            public sealed record @class(Guid TourId) : IDomainEvent;

            public sealed record @struct(Guid EventId, DateTimeOffset OccurredAt, Guid TourId) : IIntegrationEvent
            {
                public static string EventType => "tour.created";
                public static int EventVersion => 1;
            }

            public static class @internal
            {
                [IntegrationEventMapping]
                public static @struct @return(@class domainEvent, Guid eventId, DateTimeOffset occurredAt) =>
                    new(eventId, occurredAt, domainEvent.TourId);
            }
            """;
        var compilation = GeneratorTestHarness.CreateCompilation(source);

        var result = GeneratorTestHarness.RunGenerator(compilation);
        var generatedSource = GeneratorTestHarness.GetGeneratedSource(result.RunResult);
        var errors = result.OutputCompilation.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();

        errors.ShouldBeEmpty();
        generatedSource.ShouldContain("global::@event.@internal.@return", StringComparison.Ordinal);
    }

    [Fact]
    public void Ignores_unsafe_mapping_shapes()
    {
        const string source = """
            #nullable enable
            namespace Demo;

            public sealed record FirstDomainEvent(Guid TourId) : IDomainEvent;
            public sealed record SecondDomainEvent(Guid TourId) : IDomainEvent;
            public sealed record ThirdDomainEvent(Guid TourId) : IDomainEvent;
            public sealed record FourthDomainEvent(Guid TourId) : IDomainEvent;
            public sealed record FifthDomainEvent(Guid TourId) : IDomainEvent;

            public sealed record FirstIntegrationEvent(Guid EventId, DateTimeOffset OccurredAt, Guid TourId) : IIntegrationEvent
            {
                public static string EventType => "tour.first";
                public static int EventVersion => 1;
            }

            public sealed record SecondIntegrationEvent(Guid EventId, DateTimeOffset OccurredAt, Guid TourId) : IIntegrationEvent
            {
                public static string EventType => "tour.second";
                public static int EventVersion => 1;
            }

            public static class GenericMappings<T>
            {
                [IntegrationEventMapping]
                public static FirstIntegrationEvent Map(FirstDomainEvent domainEvent, Guid eventId, DateTimeOffset occurredAt) =>
                    new(eventId, occurredAt, domainEvent.TourId);
            }

            public interface IAbstractMappings
            {
                [IntegrationEventMapping]
                static abstract SecondIntegrationEvent Map(SecondDomainEvent domainEvent, Guid eventId, DateTimeOffset occurredAt);
            }

            public static class NullableMappings
            {
                [IntegrationEventMapping]
                public static FirstIntegrationEvent? Map(ThirdDomainEvent? domainEvent, Guid eventId, DateTimeOffset occurredAt) =>
                    new(eventId, occurredAt, domainEvent!.TourId);
            }

            public static class RefMappings
            {
                [IntegrationEventMapping]
                public static SecondIntegrationEvent Map(ref FourthDomainEvent domainEvent, Guid eventId, DateTimeOffset occurredAt) =>
                    new(eventId, occurredAt, domainEvent.TourId);
            }

            public static class OuterMappings
            {
                private static class HiddenMappings
                {
                    [IntegrationEventMapping]
                    public static SecondIntegrationEvent Map(FifthDomainEvent domainEvent, Guid eventId, DateTimeOffset occurredAt) =>
                        new(eventId, occurredAt, domainEvent.TourId);
                }
            }
            """;
        var compilation = GeneratorTestHarness.CreateCompilation(source);

        var runResult = GeneratorTestHarness.RunGeneratorDriver(compilation);
        var diagnostics = runResult.Diagnostics
            .Where(static diagnostic => diagnostic.Id == "INTEGRATIONEVENT001")
            .ToArray();

        runResult.Results.Single().GeneratedSources.ShouldBeEmpty();
        diagnostics.Length.ShouldBe(5);
    }

    [Fact]
    public void Reports_non_concrete_domain_event_mapping_parameters()
    {
        const string source = """
            namespace Demo;

            public interface IAbstractDomainEvent : IDomainEvent { }
            public abstract record AbstractDomainEvent(Guid TourId) : IDomainEvent;

            public sealed record IntegrationEvent(Guid EventId, DateTimeOffset OccurredAt) : IIntegrationEvent
            {
                public static string EventType => "tour.abstract";
                public static int EventVersion => 1;
            }

            public static class Mappings
            {
                [IntegrationEventMapping]
                public static IntegrationEvent MapInterface(
                    IAbstractDomainEvent domainEvent,
                    Guid eventId,
                    DateTimeOffset occurredAt) => new(eventId, occurredAt);

                [IntegrationEventMapping]
                public static IntegrationEvent MapAbstract(
                    AbstractDomainEvent domainEvent,
                    Guid eventId,
                    DateTimeOffset occurredAt) => new(eventId, occurredAt);
            }
            """;
        var compilation = GeneratorTestHarness.CreateCompilation(source);

        var result = GeneratorTestHarness.RunGenerator(compilation);
        var diagnostics = result.RunResult.Diagnostics
            .Where(static diagnostic => diagnostic.Id == "INTEGRATIONEVENT001")
            .ToArray();
        var compilationErrors = result.OutputCompilation
            .GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();

        result.RunResult.Results.Single().GeneratedSources.ShouldBeEmpty();
        diagnostics.Length.ShouldBe(2);
        compilationErrors.ShouldBeEmpty();
    }

    [Fact]
    public void Reports_non_concrete_integration_event_returns_as_errors()
    {
        const string source = """
            namespace Demo;

            public sealed record TourCreatedDomainEvent(Guid TourId) : IDomainEvent;
            public sealed record TourUpdatedDomainEvent(Guid TourId) : IDomainEvent;
            public interface ITourCreatedIntegrationEvent : IIntegrationEvent;
            public abstract record AbstractTourUpdatedIntegrationEvent : IIntegrationEvent
            {
                public static string EventType => "tour.updated";
                public static int EventVersion => 1;
            }

            public static class TourMappings
            {
                [IntegrationEventMapping]
                public static ITourCreatedIntegrationEvent Map(
                    TourCreatedDomainEvent domainEvent,
                    Guid eventId,
                    DateTimeOffset occurredAt) => throw new NotSupportedException();

                [IntegrationEventMapping]
                public static AbstractTourUpdatedIntegrationEvent Map(
                    TourUpdatedDomainEvent domainEvent,
                    Guid eventId,
                    DateTimeOffset occurredAt) => throw new NotSupportedException();
            }
            """;
        var compilation = GeneratorTestHarness.CreateCompilation(source);

        var runResult = GeneratorTestHarness.RunGeneratorDriver(compilation);
        var diagnostics = runResult.Diagnostics
            .Where(static diagnostic => diagnostic.Id == "INTEGRATIONEVENT001")
            .ToArray();

        runResult.Results.Single().GeneratedSources.ShouldBeEmpty();
        diagnostics.Length.ShouldBe(2);
        diagnostics.All(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeTrue();
        diagnostics.All(static diagnostic => diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture)
            .Contains("concrete IIntegrationEvent", StringComparison.Ordinal)).ShouldBeTrue();
    }
}
