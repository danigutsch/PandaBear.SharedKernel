using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SharedKernel.Mediator.SourceGenerator;
using SharedKernel.Messaging.IntegrationEvents.SourceGenerator;

namespace SharedKernel.Messaging.IntegrationEvents.GeneratorTests;

public sealed class IntegrationEventMappingGeneratorTests
{
    [Fact]
    public void Reports_missing_handler_for_registered_consumer_contract()
    {
        // Arrange
        const string source = """
            using Microsoft.Extensions.DependencyInjection;

            namespace Demo;

            public sealed record TourCreatedIntegrationEvent(Guid EventId, DateTimeOffset OccurredAt) : IIntegrationEvent
            {
                public static string EventType => "tour.created";
                public static int EventVersion => 1;
            }

            public static class Registration
            {
                public static IServiceCollection AddMessaging(IServiceCollection services) =>
                    services.AddIntegrationEventConsumer<TourCreatedIntegrationEvent>(TourCreatedIntegrationEvent.EventType, null!);
            }
            """;
        var compilation = GeneratorTestHarness.CreateCompilation(source);

        // Act
        var runResult = GeneratorTestHarness.RunGeneratorDriver(compilation);
        var diagnosticIds = runResult.Diagnostics.Select(static diagnostic => diagnostic.Id).ToArray();
        var generatedSource = GeneratorTestHarness.GetGeneratedSource(runResult);

        // Assert
        diagnosticIds.ShouldContain("SKMSG001");
        generatedSource.ShouldNotContain("GeneratedIntegrationEventEnvelopePublisher", StringComparison.Ordinal);
        generatedSource.ShouldNotContain("IEventEnvelopePublisher", StringComparison.Ordinal);
    }

    [Fact]
    public void Reports_duplicate_handlers_for_registered_consumer_contract()
    {
        // Arrange
        const string source = """
            using Microsoft.Extensions.DependencyInjection;

            namespace Demo;

            public sealed record TourCreatedIntegrationEvent(Guid EventId, DateTimeOffset OccurredAt) : IIntegrationEvent
            {
                public static string EventType => "tour.created";
                public static int EventVersion => 1;
            }

            public sealed class FirstHandler : IIntegrationEventHandler<TourCreatedIntegrationEvent>
            {
                public ValueTask Handle(TourCreatedIntegrationEvent integrationEvent, CancellationToken ct) => ValueTask.CompletedTask;
            }

            public sealed class SecondHandler : IIntegrationEventHandler<TourCreatedIntegrationEvent>
            {
                public ValueTask Handle(TourCreatedIntegrationEvent integrationEvent, CancellationToken ct) => ValueTask.CompletedTask;
            }

            public static class Registration
            {
                public static IServiceCollection AddMessaging(IServiceCollection services) =>
                    services.AddIntegrationEventConsumer<TourCreatedIntegrationEvent>(TourCreatedIntegrationEvent.EventType, null!);
            }
            """;
        var compilation = GeneratorTestHarness.CreateCompilation(source);

        // Act
        var runResult = GeneratorTestHarness.RunGeneratorDriver(compilation);
        var diagnosticIds = runResult.Diagnostics.Select(static diagnostic => diagnostic.Id).ToArray();
        var generatedSource = GeneratorTestHarness.GetGeneratedSource(runResult);

        // Assert
        diagnosticIds.ShouldContain("SKMSG002");
        generatedSource.ShouldNotContain("GeneratedIntegrationEventEnvelopePublisher", StringComparison.Ordinal);
        generatedSource.ShouldNotContain("IEventEnvelopePublisher", StringComparison.Ordinal);
    }

    [Fact]
    public void Reports_duplicate_event_types_without_emitting_ambiguous_delivery()
    {
        // Arrange
        const string source = """
            using Microsoft.Extensions.DependencyInjection;

            namespace Demo;

            public sealed record TourCreatedIntegrationEvent(Guid EventId, DateTimeOffset OccurredAt) : IIntegrationEvent
            {
                public static string EventType => "tour.changed";
                public static int EventVersion => 1;
            }

            public sealed record TourUpdatedIntegrationEvent(Guid EventId, DateTimeOffset OccurredAt) : IIntegrationEvent
            {
                public static string EventType => "tour.changed";
                public static int EventVersion => 1;
            }

            public sealed class TourCreatedHandler : IIntegrationEventHandler<TourCreatedIntegrationEvent>
            {
                public ValueTask Handle(TourCreatedIntegrationEvent integrationEvent, CancellationToken ct) => ValueTask.CompletedTask;
            }

            public sealed class TourUpdatedHandler : IIntegrationEventHandler<TourUpdatedIntegrationEvent>
            {
                public ValueTask Handle(TourUpdatedIntegrationEvent integrationEvent, CancellationToken ct) => ValueTask.CompletedTask;
            }

            public static class Registration
            {
                public static IServiceCollection AddMessaging(IServiceCollection services)
                {
                    services.AddIntegrationEventConsumer<TourCreatedIntegrationEvent>(TourCreatedIntegrationEvent.EventType, null!);
                    services.AddIntegrationEventConsumer<TourUpdatedIntegrationEvent>(TourUpdatedIntegrationEvent.EventType, null!);
                    return services;
                }
            }
            """;
        var compilation = GeneratorTestHarness.CreateCompilation(source);

        // Act
        var runResult = GeneratorTestHarness.RunGeneratorDriver(compilation);
        var diagnosticIds = runResult.Diagnostics.Select(static diagnostic => diagnostic.Id).ToArray();
        var generatedSource = GeneratorTestHarness.GetGeneratedSource(runResult);

        // Assert
        diagnosticIds.ShouldContain("SKMSG003");
        generatedSource.ShouldNotContain("GeneratedIntegrationEventEnvelopePublisher", StringComparison.Ordinal);
        generatedSource.ShouldNotContain("IEventEnvelopePublisher", StringComparison.Ordinal);
    }

    [Fact]
    public void Generates_identical_output_when_source_file_order_is_reversed()
    {
        // Arrange
        const string firstSource = """
            using Microsoft.Extensions.DependencyInjection;

            namespace Demo;

            public sealed record AlphaIntegrationEvent(Guid EventId, DateTimeOffset OccurredAt) : IIntegrationEvent
            {
                public static string EventType => "alpha.created";
                public static int EventVersion => 1;
            }

            public sealed class AlphaHandler : IIntegrationEventHandler<AlphaIntegrationEvent>
            {
                public ValueTask Handle(AlphaIntegrationEvent integrationEvent, CancellationToken ct) => ValueTask.CompletedTask;
            }

            public static class AlphaRegistration
            {
                public static IServiceCollection AddAlpha(IServiceCollection services) =>
                    services.AddIntegrationEventConsumer<AlphaIntegrationEvent>(AlphaIntegrationEvent.EventType, null!);
            }
            """;
        const string secondSource = """
            using Microsoft.Extensions.DependencyInjection;

            namespace Demo;

            public sealed record BetaIntegrationEvent(Guid EventId, DateTimeOffset OccurredAt) : IIntegrationEvent
            {
                public static string EventType => "beta.created";
                public static int EventVersion => 1;
            }

            public sealed class BetaHandler : IIntegrationEventHandler<BetaIntegrationEvent>
            {
                public ValueTask Handle(BetaIntegrationEvent integrationEvent, CancellationToken ct) => ValueTask.CompletedTask;
            }

            public static class BetaRegistration
            {
                public static IServiceCollection AddBeta(IServiceCollection services) =>
                    services.AddIntegrationEventConsumer<BetaIntegrationEvent>(BetaIntegrationEvent.EventType, null!);
            }
            """;

        // Act
        var forward = GeneratorTestHarness.GetGeneratedSource(GeneratorTestHarness.RunGeneratorDriver(
            GeneratorTestHarness.CreateCompilation([firstSource, secondSource])));
        var reverse = GeneratorTestHarness.GetGeneratedSource(GeneratorTestHarness.RunGeneratorDriver(
            GeneratorTestHarness.CreateCompilation([secondSource, firstSource])));

        // Assert
        reverse.ShouldBe(forward);
    }

    [Fact]
    public void Partial_handler_declarations_count_as_one_structural_handler()
    {
        // Arrange
        const string firstSource = """
            using Microsoft.Extensions.DependencyInjection;

            namespace Demo;

            public sealed record TourCreatedIntegrationEvent(Guid EventId, DateTimeOffset OccurredAt) : IIntegrationEvent
            {
                public static string EventType => "tour.created";
                public static int EventVersion => 1;
            }

            public sealed partial class TourCreatedHandler : IIntegrationEventHandler<TourCreatedIntegrationEvent>
            {
                public ValueTask Handle(TourCreatedIntegrationEvent integrationEvent, CancellationToken ct) => ValueTask.CompletedTask;
            }

            public static class Registration
            {
                public static IServiceCollection AddMessaging(IServiceCollection services) =>
                    services.AddIntegrationEventConsumer<TourCreatedIntegrationEvent>(TourCreatedIntegrationEvent.EventType, null!);
            }
            """;
        const string secondSource = """
            namespace Demo;

            public sealed partial class TourCreatedHandler;
            """;
        var compilation = GeneratorTestHarness.CreateCompilation([firstSource, secondSource]);

        // Act
        var runResult = GeneratorTestHarness.RunGeneratorDriver(compilation);
        var diagnosticIds = runResult.Diagnostics.Select(static diagnostic => diagnostic.Id).ToArray();
        var generatedSource = GeneratorTestHarness.GetGeneratedSource(runResult);

        // Assert
        diagnosticIds.ShouldNotContain("SKMSG001");
        diagnosticIds.ShouldNotContain("SKMSG002");
        generatedSource.ShouldContain("GeneratedIntegrationEventEnvelopePublisher", StringComparison.Ordinal);
    }

    [Fact]
    public void Inaccessible_and_open_generic_handler_containers_do_not_enable_delivery()
    {
        // Arrange
        const string source = """
            using Microsoft.Extensions.DependencyInjection;

            namespace Demo;

            public sealed record PrivateContainerIntegrationEvent(Guid EventId, DateTimeOffset OccurredAt) : IIntegrationEvent
            {
                public static string EventType => "private.container";
                public static int EventVersion => 1;
            }

            public sealed record GenericContainerIntegrationEvent(Guid EventId, DateTimeOffset OccurredAt) : IIntegrationEvent
            {
                public static string EventType => "generic.container";
                public static int EventVersion => 1;
            }

            public static class PublicContainer
            {
                private static class PrivateContainer
                {
                    public sealed class Handler : IIntegrationEventHandler<PrivateContainerIntegrationEvent>
                    {
                        public ValueTask Handle(PrivateContainerIntegrationEvent integrationEvent, CancellationToken ct) => ValueTask.CompletedTask;
                    }
                }
            }

            public static class GenericContainer<T>
            {
                public sealed class Handler : IIntegrationEventHandler<GenericContainerIntegrationEvent>
                {
                    public ValueTask Handle(GenericContainerIntegrationEvent integrationEvent, CancellationToken ct) => ValueTask.CompletedTask;
                }
            }

            public static class Registration
            {
                public static IServiceCollection AddMessaging(IServiceCollection services)
                {
                    services.AddIntegrationEventConsumer<PrivateContainerIntegrationEvent>(PrivateContainerIntegrationEvent.EventType, null!);
                    services.AddIntegrationEventConsumer<GenericContainerIntegrationEvent>(GenericContainerIntegrationEvent.EventType, null!);
                    return services;
                }
            }
            """;
        var compilation = GeneratorTestHarness.CreateCompilation(source);

        // Act
        var runResult = GeneratorTestHarness.RunGeneratorDriver(compilation);
        var missingHandlerDiagnostics = runResult.Diagnostics
            .Where(static diagnostic => diagnostic.Id == "SKMSG001")
            .ToArray();
        var generatedSource = GeneratorTestHarness.GetGeneratedSource(runResult);

        // Assert
        missingHandlerDiagnostics.ShouldHaveCount(2);
        generatedSource.ShouldNotContain("GeneratedIntegrationEventEnvelopePublisher", StringComparison.Ordinal);
        generatedSource.ShouldNotContain("IEventEnvelopePublisher", StringComparison.Ordinal);
    }

    [Fact]
    public void Duplicate_mapping_pairs_report_one_stable_error_and_emit_no_ambiguous_dispatcher()
    {
        // Arrange
        const string firstSource = """
            namespace Demo;

            public sealed record TourCreatedDomainEvent(Guid TourId) : IDomainEvent;

            public sealed record TourCreatedIntegrationEvent(Guid EventId, DateTimeOffset OccurredAt, Guid TourId) : IIntegrationEvent
            {
                public static string EventType => "tour.created";
                public static int EventVersion => 1;
            }

            public static class AlphaMappings
            {
                [IntegrationEventMapping]
                public static TourCreatedIntegrationEvent Map(TourCreatedDomainEvent domainEvent, Guid eventId, DateTimeOffset occurredAt) =>
                    new(eventId, occurredAt, domainEvent.TourId);
            }
            """;
        const string secondSource = """
            using Microsoft.Extensions.DependencyInjection;

            namespace Demo;

            public static class BetaMappings
            {
                [IntegrationEventMapping]
                public static TourCreatedIntegrationEvent Map(TourCreatedDomainEvent domainEvent, Guid eventId, DateTimeOffset occurredAt) =>
                    new(eventId, occurredAt, domainEvent.TourId);
            }

            public static class Registration
            {
                public static IServiceCollection AddMessaging(IServiceCollection services) =>
                    services.AddIntegrationEventContract<TourCreatedIntegrationEvent>(TourCreatedIntegrationEvent.EventType, null!);
            }
            """;

        // Act
        var forwardResult = GeneratorTestHarness.RunGeneratorDriver(
            GeneratorTestHarness.CreateCompilation([firstSource, secondSource]));
        var reverseResult = GeneratorTestHarness.RunGeneratorDriver(
            GeneratorTestHarness.CreateCompilation([secondSource, firstSource]));
        var forwardDiagnostic = forwardResult.Diagnostics
            .Where(static diagnostic => diagnostic.Id == "SKMSG005")
            .ShouldHaveSingleItem();
        var reverseDiagnostic = reverseResult.Diagnostics
            .Where(static diagnostic => diagnostic.Id == "SKMSG005")
            .ShouldHaveSingleItem();
        var forward = GeneratorTestHarness.GetGeneratedSource(forwardResult);
        var reverse = GeneratorTestHarness.GetGeneratedSource(reverseResult);

        // Assert
        var forwardMessage = forwardDiagnostic.GetMessage(CultureInfo.InvariantCulture);
        var reverseMessage = reverseDiagnostic.GetMessage(CultureInfo.InvariantCulture);
        forwardMessage.ShouldContain("global::Demo.AlphaMappings.Map", StringComparison.Ordinal);
        forwardMessage.ShouldContain("global::Demo.BetaMappings.Map", StringComparison.Ordinal);
        reverseMessage.ShouldBe(forwardMessage);
        reverse.ShouldBe(forward);
        forward.ShouldNotContain("GeneratedIntegrationEventDomainEventDispatcher", StringComparison.Ordinal);
        forward.ShouldNotContain("AlphaMappings.Map", StringComparison.Ordinal);
        forward.ShouldNotContain("BetaMappings.Map", StringComparison.Ordinal);
    }

    [Fact]
    public void Unrelated_same_name_methods_are_not_integration_event_registrations()
    {
        // Arrange
        const string source = """
            using Microsoft.Extensions.DependencyInjection;

            namespace Demo;

            public sealed record TourCreatedIntegrationEvent(Guid EventId, DateTimeOffset OccurredAt) : IIntegrationEvent
            {
                public static string EventType => "tour.created";
                public static int EventVersion => 1;
            }

            public static class UnrelatedRegistrationMethods
            {
                public static IServiceCollection AddIntegrationEventContract<TIntegrationEvent>(IServiceCollection services)
                    where TIntegrationEvent : IIntegrationEvent => services;

                public static IServiceCollection AddIntegrationEventConsumer<TIntegrationEvent>(IServiceCollection services)
                    where TIntegrationEvent : IIntegrationEvent => services;
            }

            public static class Registration
            {
                public static IServiceCollection AddMessaging(IServiceCollection services)
                {
                    UnrelatedRegistrationMethods.AddIntegrationEventContract<TourCreatedIntegrationEvent>(services);
                    UnrelatedRegistrationMethods.AddIntegrationEventConsumer<TourCreatedIntegrationEvent>(services);
                    return services;
                }
            }
            """;
        var compilation = GeneratorTestHarness.CreateCompilation(source);

        // Act
        var runResult = GeneratorTestHarness.RunGeneratorDriver(compilation);

        // Assert
        runResult.Diagnostics.ShouldBeEmpty();
        runResult.Results.Single().GeneratedSources.ShouldBeEmpty();
    }

    [Fact]
    public void Exact_registration_api_with_an_inferred_type_argument_is_discovered()
    {
        // Arrange
        const string source = """
            using System.Text.Json.Serialization.Metadata;
            using Microsoft.Extensions.DependencyInjection;

            namespace Demo;

            public sealed record TourCreatedIntegrationEvent(Guid EventId, DateTimeOffset OccurredAt) : IIntegrationEvent
            {
                public static string EventType => "tour.created";
                public static int EventVersion => 1;
            }

            public sealed class TourCreatedHandler : IIntegrationEventHandler<TourCreatedIntegrationEvent>
            {
                public ValueTask Handle(TourCreatedIntegrationEvent integrationEvent, CancellationToken ct) => ValueTask.CompletedTask;
            }

            public static class Registration
            {
                public static IServiceCollection AddMessaging(
                    IServiceCollection services,
                    JsonTypeInfo<TourCreatedIntegrationEvent> jsonTypeInfo) =>
                    services.AddIntegrationEventConsumer(TourCreatedIntegrationEvent.EventType, jsonTypeInfo);
            }
            """;
        var compilation = GeneratorTestHarness.CreateCompilation(source);

        // Act
        var runResult = GeneratorTestHarness.RunGeneratorDriver(compilation);
        var generatedSource = GeneratorTestHarness.GetGeneratedSource(runResult);

        // Assert
        runResult.Diagnostics.ShouldBeEmpty();
        generatedSource.ShouldContain("GeneratedIntegrationEventEnvelopePublisher", StringComparison.Ordinal);
        generatedSource.ShouldContain("JsonTypeInfo<global::Demo.TourCreatedIntegrationEvent>", StringComparison.Ordinal);
    }

    [Fact]
    public void Generates_duplicate_event_type_validation_for_closed_contracts()
    {
        // Arrange
        const string source = """
            using Microsoft.Extensions.DependencyInjection;

            namespace Demo;

            public sealed record TourCreatedIntegrationEvent(Guid EventId, DateTimeOffset OccurredAt) : IIntegrationEvent
            {
                public static string EventType => "tour.changed";
                public static int EventVersion => 1;
            }

            public sealed record TourUpdatedIntegrationEvent(Guid EventId, DateTimeOffset OccurredAt) : IIntegrationEvent
            {
                public static string EventType => "tour.changed";
                public static int EventVersion => 1;
            }

            public static class Registration
            {
                public static IServiceCollection AddMessaging(IServiceCollection services)
                {
                    services.AddIntegrationEventContract<TourCreatedIntegrationEvent>(
                        TourCreatedIntegrationEvent.EventType,
                        null!);
                    services.AddIntegrationEventContract<TourUpdatedIntegrationEvent>(
                        TourUpdatedIntegrationEvent.EventType,
                        null!);
                    return services;
                }
            }
            """;
        var compilation = GeneratorTestHarness.CreateCompilation(source);

        // Act
        var runResult = GeneratorTestHarness.RunGeneratorDriver(compilation);
        var generatedSource = GeneratorTestHarness.GetGeneratedSource(runResult);
        var generatedTree = CSharpSyntaxTree.ParseText(
            generatedSource,
            new CSharpParseOptions(LanguageVersion.Preview),
            cancellationToken: TestContext.Current.CancellationToken);
        var compilationErrors = compilation.AddSyntaxTrees(generatedTree)
            .GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Select(static diagnostic => diagnostic.ToString())
            .ToArray();

        // Assert
        compilationErrors.ShouldBeEmpty();
        generatedSource.ShouldContain("declare duplicate event type", StringComparison.Ordinal);
        generatedSource.ShouldContain("TourCreatedIntegrationEvent.EventType", StringComparison.Ordinal);
        generatedSource.ShouldContain("TourUpdatedIntegrationEvent.EventType", StringComparison.Ordinal);
    }

    [Fact]
    public void Contract_registration_does_not_enable_envelope_delivery_when_a_handler_exists()
    {
        // Arrange
        const string source = """
            using Microsoft.Extensions.DependencyInjection;

            namespace Demo;

            public sealed record TourCreatedIntegrationEvent(Guid EventId, DateTimeOffset OccurredAt) : IIntegrationEvent
            {
                public static string EventType => "tour.created";
                public static int EventVersion => 1;
            }

            public sealed class TourCreatedIntegrationEventHandler : IIntegrationEventHandler<TourCreatedIntegrationEvent>
            {
                public ValueTask Handle(TourCreatedIntegrationEvent integrationEvent, CancellationToken ct) =>
                    ValueTask.CompletedTask;
            }

            public static class Registration
            {
                public static IServiceCollection AddMessaging(IServiceCollection services) =>
                    services.AddIntegrationEventContract<TourCreatedIntegrationEvent>(
                        TourCreatedIntegrationEvent.EventType,
                        null!);
            }
            """;
        var compilation = GeneratorTestHarness.CreateCompilation(source);

        // Act
        var runResult = GeneratorTestHarness.RunGeneratorDriver(compilation);
        var generatedSource = GeneratorTestHarness.GetGeneratedSource(runResult);

        // Assert
        generatedSource.ShouldContain("GeneratedIntegrationEventSerializer", StringComparison.Ordinal);
        generatedSource.ShouldNotContain("GeneratedIntegrationEventEnvelopePublisher", StringComparison.Ordinal);
        generatedSource.ShouldNotContain("IEventEnvelopePublisher", StringComparison.Ordinal);
    }

    [Fact]
    public void Combined_generators_register_only_the_transactional_outbox_dispatcher()
    {
        // Arrange
        const string source = """
            using SharedKernel.Domain;
            using SharedKernel.Mediator;
            using SharedKernel.Messaging.IntegrationEvents;
            using Microsoft.Extensions.DependencyInjection;

            [assembly: MediatorModule]

            namespace SharedKernel.DomainEvents
            {
                public interface IDomainEventHandler<in TDomainEvent>
                    where TDomainEvent : IDomainEvent;
            }

            namespace Demo
            {
                public sealed record TourCreatedDomainEvent(Guid TourId) : IDomainEvent;

                public sealed class TourCreatedDomainEventHandler : SharedKernel.DomainEvents.IDomainEventHandler<TourCreatedDomainEvent>
                {
                    public ValueTask Handle(TourCreatedDomainEvent domainEvent, CancellationToken ct) => ValueTask.CompletedTask;
                }

                public sealed record TourCreatedIntegrationEvent(Guid EventId, DateTimeOffset OccurredAt, Guid TourId) : IIntegrationEvent
                {
                    public static string EventType => "tour.created";

                    public static int EventVersion => 1;
                }

                public sealed class TourCreatedIntegrationEventHandler : IIntegrationEventHandler<TourCreatedIntegrationEvent>
                {
                    public ValueTask Handle(TourCreatedIntegrationEvent integrationEvent, CancellationToken ct) => ValueTask.CompletedTask;
                }

                public static class TourMappings
                {
                    [IntegrationEventMapping]
                    public static TourCreatedIntegrationEvent Map(TourCreatedDomainEvent domainEvent, Guid eventId, DateTimeOffset occurredAt) =>
                        new(eventId, occurredAt, domainEvent.TourId);
                }

                public static class IntegrationEventRegistration
                {
                    public static IServiceCollection AddTourIntegrationEvents(IServiceCollection services)
                    {
                        return services.AddIntegrationEventConsumer<TourCreatedIntegrationEvent>(
                            TourCreatedIntegrationEvent.EventType,
                            null!);
                    }
                }
            }
            """;
        var compilation = GeneratorTestHarness.CreateCompilation(source);

        // Act
        var runResult = GeneratorTestHarness.RunGeneratorDriver(
            compilation,
            new IntegrationEventMappingGenerator(),
            new SharedKernelMediatorGenerator());
        var dispatcherRegistrations = runResult.Results
            .SelectMany(static result => result.GeneratedSources)
            .SelectMany(static generatedSource => generatedSource.SourceText.Lines)
            .Select(static line => line.ToString())
            .Where(static line =>
                line.Contains("ServiceCollectionDescriptorExtensions.TryAdd", StringComparison.Ordinal) &&
                line.Contains("IDomainEventDispatcher", StringComparison.Ordinal))
            .ToArray();
        var generatedIntegrationEvents = GeneratorTestHarness.GetGeneratedSource(runResult);
        var generatedRoot = CSharpSyntaxTree.ParseText(
            generatedIntegrationEvents,
            cancellationToken: TestContext.Current.CancellationToken).GetRoot(TestContext.Current.CancellationToken);
        var generatedTypes = generatedRoot.DescendantNodes().OfType<TypeDeclarationSyntax>().ToArray();
        var topLevelTypeCount = generatedTypes.Count(static type =>
            type.Parent is CompilationUnitSyntax or BaseNamespaceDeclarationSyntax);
        var nestedTypeCount = generatedTypes.Length - topLevelTypeCount;
        var registrationMethodCount = generatedRoot.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Count(static method => method.Identifier.ValueText.StartsWith("Add", StringComparison.Ordinal));
        var serviceProviderSiteCount = generatedIntegrationEvents.Split("IServiceProvider").Length - 1
            + generatedIntegrationEvents.Split("GetRequiredService").Length - 1;
        var runtimeRecoveryTypeCount = generatedTypes.Count(static type =>
            type.Identifier.ValueText.EndsWith("Registration", StringComparison.Ordinal) ||
            type.Identifier.ValueText.EndsWith("Registry", StringComparison.Ordinal) ||
            type.Identifier.ValueText.EndsWith("Factory", StringComparison.Ordinal) ||
            type.Identifier.ValueText.EndsWith("Wrapper", StringComparison.Ordinal));

        // Assert
        generatedIntegrationEvents.ShouldContain("GeneratedIntegrationEventSerializer", StringComparison.Ordinal);
        generatedIntegrationEvents.ShouldContain("GeneratedIntegrationEventEnvelopePublisher", StringComparison.Ordinal);
        generatedIntegrationEvents.ShouldContain("JsonTypeInfo<global::Demo.TourCreatedIntegrationEvent>", StringComparison.Ordinal);
        generatedIntegrationEvents.ShouldContain("IIntegrationEventHandler<global::Demo.TourCreatedIntegrationEvent>", StringComparison.Ordinal);
        generatedIntegrationEvents.ShouldNotContain("IServiceProvider", StringComparison.Ordinal);
        generatedIntegrationEvents.ShouldNotContain("GetRequiredService", StringComparison.Ordinal);
        generatedIntegrationEvents.ShouldNotContain("ContractRegistration", StringComparison.Ordinal);
        generatedIntegrationEvents.ShouldNotContain("Dictionary", StringComparison.Ordinal);
        topLevelTypeCount.ShouldBe(5);
        nestedTypeCount.ShouldBe(0);
        registrationMethodCount.ShouldBe(1);
        serviceProviderSiteCount.ShouldBe(0);
        runtimeRecoveryTypeCount.ShouldBe(0);
        var dispatcherRegistration = dispatcherRegistrations.ShouldHaveSingleItem();
        dispatcherRegistration.ShouldContain("CompositeDomainEventDispatcher", StringComparison.Ordinal);
        generatedIntegrationEvents.ShouldContain(
            "IDomainEventDispatchHandler, global::SharedKernel.Messaging.IntegrationEvents.Generated.GeneratedIntegrationEventDomainEventDispatcher",
            StringComparison.Ordinal);
    }

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
    public void Reports_malformed_mapping_methods_at_the_attribute_without_emitting_dispatcher_code()
    {
        // Arrange
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
                public static TourCreatedIntegrationEvent MissingTimestamp(TourCreatedDomainEvent domainEvent, Guid eventId) =>
                    new(eventId, DateTimeOffset.UtcNow, domainEvent.TourId);

                [IntegrationEventMapping]
                public static TourCreatedIntegrationEvent WrongIdentifier(TourCreatedDomainEvent domainEvent, string eventId, DateTimeOffset occurredAt) =>
                    new(Guid.Empty, occurredAt, domainEvent.TourId);

                [IntegrationEventMapping]
                public TourCreatedIntegrationEvent Instance(TourCreatedDomainEvent domainEvent, Guid eventId, DateTimeOffset occurredAt) =>
                    new(eventId, occurredAt, domainEvent.TourId);

                [IntegrationEventMapping]
                public static TIntegrationEvent Generic<TIntegrationEvent>(TourCreatedDomainEvent domainEvent, Guid eventId, DateTimeOffset occurredAt)
                    where TIntegrationEvent : IIntegrationEvent => throw new NotSupportedException();
            }
            """;
        var compilation = GeneratorTestHarness.CreateCompilation(source);

        // Act
        var runResult = GeneratorTestHarness.RunGeneratorDriver(compilation);
        var diagnostics = runResult.Diagnostics
            .Where(static diagnostic => diagnostic.Id == "SKMSG004")
            .ToArray();

        // Assert
        diagnostics.ShouldHaveCount(4);
        foreach (var diagnostic in diagnostics)
        {
            diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
            diagnostic.Location.IsInSource.ShouldBeTrue();
            var sourceTree = diagnostic.Location.SourceTree.ShouldNotBeNull();
            var locationText = sourceTree.GetText(TestContext.Current.CancellationToken)
                .ToString(diagnostic.Location.SourceSpan);
            locationText.ShouldBe("IntegrationEventMapping");
        }
        runResult.Results.Single().GeneratedSources.ShouldBeEmpty();
    }

    [Fact]
    public void Reports_private_mapping_methods_at_the_attribute()
    {
        // Arrange
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

        // Act
        var runResult = GeneratorTestHarness.RunGeneratorDriver(compilation);
        var diagnostic = runResult.Diagnostics
            .Where(static item => item.Id == "SKMSG004")
            .ShouldHaveSingleItem();

        // Assert
        diagnostic.Location.IsInSource.ShouldBeTrue();
        runResult.Results.Single().GeneratedSources.ShouldBeEmpty();
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
            .Where(static diagnostic => diagnostic.Id == "SKMSG004")
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
            .Where(static diagnostic => diagnostic.Id == "SKMSG004")
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
            .Where(static diagnostic => diagnostic.Id == "SKMSG004")
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
            .Where(static diagnostic => diagnostic.Id == "SKMSG004")
            .ToArray();

        runResult.Results.Single().GeneratedSources.ShouldBeEmpty();
        diagnostics.Length.ShouldBe(2);
        diagnostics.All(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeTrue();
        diagnostics.All(static diagnostic => diagnostic.GetMessage(CultureInfo.InvariantCulture)
            .Contains("concrete IIntegrationEvent", StringComparison.Ordinal)).ShouldBeTrue();
    }

    [Fact]
    public void Reports_inaccessible_and_open_generic_mapping_containers_without_emitting_unsafe_dispatcher_cases()
    {
        // Arrange
        const string source = """
            using Microsoft.Extensions.DependencyInjection;

            namespace Demo;

            public sealed record TourCreatedDomainEvent(Guid TourId) : IDomainEvent;

            public sealed record TourCreatedIntegrationEvent(Guid EventId, DateTimeOffset OccurredAt, Guid TourId) : IIntegrationEvent
            {
                public static string EventType => "tour.created";
                public static int EventVersion => 1;
            }

            public static class PublicContainer
            {
                private static class PrivateMappings
                {
                    [IntegrationEventMapping]
                    public static TourCreatedIntegrationEvent Map(TourCreatedDomainEvent domainEvent, Guid eventId, DateTimeOffset occurredAt) =>
                        new(eventId, occurredAt, domainEvent.TourId);
                }
            }

            public static class GenericMappings<T>
            {
                [IntegrationEventMapping]
                public static TourCreatedIntegrationEvent Map(TourCreatedDomainEvent domainEvent, Guid eventId, DateTimeOffset occurredAt) =>
                    new(eventId, occurredAt, domainEvent.TourId);
            }

            public static class Registration
            {
                public static IServiceCollection AddMessaging(IServiceCollection services) =>
                    services.AddIntegrationEventContract<TourCreatedIntegrationEvent>(TourCreatedIntegrationEvent.EventType, null!);
            }
            """;
        var compilation = GeneratorTestHarness.CreateCompilation(source);

        // Act
        var runResult = GeneratorTestHarness.RunGeneratorDriver(compilation);
        var diagnostics = runResult.Diagnostics
            .Where(static diagnostic => diagnostic.Id == "SKMSG004")
            .ToArray();
        var generatedSource = GeneratorTestHarness.GetGeneratedSource(runResult);

        // Assert
        diagnostics.ShouldHaveCount(2);
        generatedSource.ShouldContain("GeneratedIntegrationEventSerializer", StringComparison.Ordinal);
        generatedSource.ShouldNotContain("GeneratedIntegrationEventDomainEventDispatcher", StringComparison.Ordinal);
        generatedSource.ShouldNotContain("PrivateMappings.Map", StringComparison.Ordinal);
        generatedSource.ShouldNotContain("GenericMappings", StringComparison.Ordinal);
    }
}
