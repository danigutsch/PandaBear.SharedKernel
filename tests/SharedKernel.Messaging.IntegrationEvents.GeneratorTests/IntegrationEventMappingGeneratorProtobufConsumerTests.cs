using Microsoft.CodeAnalysis;

namespace PandaBear.Messaging.IntegrationEvents.GeneratorTests;

/// <summary>Tests that IntegrationEventMappingGenerator delivers Protobuf consumers registered with AddProtobufIntegrationEventConsumer.</summary>
public sealed class IntegrationEventMappingGeneratorProtobufConsumerTests
{
    [Fact]
    public void Protobuf_consumer_generates_a_binary_delivery_branch_without_json_metadata()
    {
        // Arrange
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            using Microsoft.Extensions.DependencyInjection;
            using PandaBear.Messaging.Protobuf;
            using PandaBear.Messaging.Protobuf.TestContracts;

            namespace Demo;

            public sealed class TourCreatedHandler : IIntegrationEventHandler<TourCreated>
            {
                public ValueTask Handle(TourCreated integrationEvent, CancellationToken ct) => ValueTask.CompletedTask;
            }

            public static class Registration
            {
                public static IServiceCollection AddMessaging(IServiceCollection services) =>
                    services.AddProtobufIntegrationEventConsumer<TourCreated, TourCreatedHandler>();
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
        result.RunResult.Diagnostics.ShouldBeEmpty();
        generatedSource.ShouldContain("internal sealed class GeneratedIntegrationEventEnvelopePublisher(", StringComparison.Ordinal);
        generatedSource.ShouldNotContain("JsonTypeInfo<", StringComparison.Ordinal);
        generatedSource.ShouldContain("envelope.PayloadEncoding == global::PandaBear.Messaging.EventPayloadEncoding.Base64", StringComparison.Ordinal);
        generatedSource.ShouldContain("\"application/protobuf\", global::System.StringComparison.OrdinalIgnoreCase", StringComparison.Ordinal);
        generatedSource.ShouldContain("new global::PandaBear.Messaging.Protobuf.TestContracts.TourCreated()", StringComparison.Ordinal);
        generatedSource.ShouldContain("global::Google.Protobuf.MessageExtensions.MergeFrom(integrationEvent,", StringComparison.Ordinal);
        generatedSource.ShouldContain("TryAddScoped<global::PandaBear.Messaging.IntegrationEvents.IIntegrationEventHandler<global::PandaBear.Messaging.Protobuf.TestContracts.TourCreated>, global::Demo.TourCreatedHandler>", StringComparison.Ordinal);
        generatedSource.ShouldContain("new global::PandaBear.Messaging.Protobuf.ProtobufGeneratedConsumer(global::PandaBear.Messaging.Protobuf.TestContracts.TourCreated.EventType)", StringComparison.Ordinal);
        generatedSource.ShouldContain("ToByteArray(typed)", StringComparison.Ordinal);
    }

    [Fact]
    public void Protobuf_consumer_without_a_handler_reports_the_missing_handler()
    {
        // Arrange
        const string source = """
            using Microsoft.Extensions.DependencyInjection;
            using PandaBear.Messaging.Protobuf;
            using PandaBear.Messaging.Protobuf.TestContracts;

            namespace Demo;

            public static class Registration
            {
                public static IServiceCollection AddMessaging(IServiceCollection services) =>
                    services.AddProtobufIntegrationEventConsumer<TourCreated, NoHandler>();
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
    }

    [Fact]
    public void Protobuf_consumer_for_a_non_protobuf_event_reports_an_invalid_contract()
    {
        // Arrange
        const string source = """
            using Microsoft.Extensions.DependencyInjection;
            using PandaBear.Messaging.Protobuf;

            namespace Demo;

            public sealed record JsonOnlyIntegrationEvent(Guid EventId, DateTimeOffset OccurredAt) : IIntegrationEvent
            {
                public static string EventType => "json.only";
                public static int EventVersion => 1;
            }

            public static class Registration
            {
                public static IServiceCollection AddMessaging(IServiceCollection services) =>
                    services.AddProtobufIntegrationEventConsumer<JsonOnlyIntegrationEvent, NoHandler>();
            }
            """;
        var compilation = GeneratorTestHarness.CreateCompilation(source);

        // Act
        var runResult = GeneratorTestHarness.RunGeneratorDriver(compilation);
        var diagnosticIds = runResult.Diagnostics.Select(static diagnostic => diagnostic.Id).ToArray();

        // Assert
        diagnosticIds.ShouldContain("SKMSG006");
    }
}
