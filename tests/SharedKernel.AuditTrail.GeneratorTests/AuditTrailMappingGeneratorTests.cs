using Microsoft.CodeAnalysis;

namespace SharedKernel.AuditTrail.GeneratorTests;

public sealed class AuditTrailMappingGeneratorTests
{
    [Fact]
    public void Generates_compilable_dispatch_handler_for_a_valid_audit_mapping()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed record DocumentFinalizedDomainEvent(Guid DocumentId) : IDomainEvent;

            public sealed record DocumentAuditEntry(Guid DocumentId, DateTime OccurredAtUtc) : IAuditTrailEntry;

            public static class DocumentAuditMappings
            {
                [AuditTrailMapping]
                public static DocumentAuditEntry Map(DocumentFinalizedDomainEvent domainEvent, DateTimeOffset occurredAt) =>
                    new(domainEvent.DocumentId, occurredAt.UtcDateTime);
            }
            """;
        var compilation = AuditTrailGeneratorTestHarness.CreateCompilation(source);

        // Act
        var result = AuditTrailGeneratorTestHarness.RunGeneratorDriver(compilation);
        var generatedSource = AuditTrailGeneratorTestHarness.GetGeneratedSource(result.RunResult);
        var errors = result.OutputCompilation.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();

        // Assert
        result.Diagnostics.ShouldBeEmpty();
        errors.ShouldBeEmpty();
        generatedSource.ShouldContain("GeneratedAuditTrailDomainEventHandler", StringComparison.Ordinal);
        generatedSource.ShouldContain("IAuditTrailSink<global::Demo.DocumentAuditEntry>", StringComparison.Ordinal);
        generatedSource.ShouldContain("auditTrailSink.Append(", StringComparison.Ordinal);
        generatedSource.ShouldContain("IDomainEventDispatchHandler", StringComparison.Ordinal);
        generatedSource.ShouldContain("TryAddEnumerable", StringComparison.Ordinal);
        generatedSource.ShouldContain("TryAddScoped<global::SharedKernel.Domain.IDomainEventDispatcher", StringComparison.Ordinal);
        generatedSource.ShouldContain("ServiceDescriptor.Scoped<global::SharedKernel.Domain.IDomainEventDispatchHandler", StringComparison.Ordinal);
        generatedSource.ShouldNotContain("ServiceDescriptor.Singleton<global::SharedKernel.Domain.IDomainEventDispatchHandler", StringComparison.Ordinal);
        generatedSource.ShouldNotContain("IntegrationEvent", StringComparison.Ordinal);
        generatedSource.ShouldNotContain("CloudEvent", StringComparison.Ordinal);
    }

    [Fact]
    public void Reports_an_error_for_an_invalid_audit_mapping_signature()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed record DocumentFinalizedDomainEvent(Guid DocumentId) : IDomainEvent;

            public sealed record DocumentAuditEntry(Guid DocumentId, DateTime OccurredAtUtc) : IAuditTrailEntry;

            public static class DocumentAuditMappings
            {
                [AuditTrailMapping]
                public static DocumentAuditEntry Map(DocumentFinalizedDomainEvent domainEvent) =>
                    new(domainEvent.DocumentId, DateTime.UnixEpoch);
            }
            """;
        var compilation = AuditTrailGeneratorTestHarness.CreateCompilation(source);

        // Act
        var result = AuditTrailGeneratorTestHarness.RunGeneratorDriver(compilation);

        // Assert
        result.Diagnostics.ShouldContain(diagnostic => diagnostic.Id == "AUDIT001" && diagnostic.Severity == DiagnosticSeverity.Error);
        result.RunResult.Results.Single().GeneratedSources.ShouldBeEmpty();
    }

    [Fact]
    public void Reports_non_concrete_domain_event_mapping_parameters()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public interface IDocumentDomainEvent : IDomainEvent;

            public abstract record DocumentDomainEvent(Guid DocumentId) : IDomainEvent;

            public sealed record DocumentAuditEntry(Guid DocumentId, DateTime OccurredAtUtc) : IAuditTrailEntry;

            public static class DocumentAuditMappings
            {
                [AuditTrailMapping]
                public static DocumentAuditEntry MapInterface(IDocumentDomainEvent domainEvent, DateTimeOffset occurredAt) =>
                    new(Guid.Empty, occurredAt.UtcDateTime);

                [AuditTrailMapping]
                public static DocumentAuditEntry MapAbstract(DocumentDomainEvent domainEvent, DateTimeOffset occurredAt) =>
                    new(domainEvent.DocumentId, occurredAt.UtcDateTime);
            }
            """;
        var compilation = AuditTrailGeneratorTestHarness.CreateCompilation(source);

        // Act
        var result = AuditTrailGeneratorTestHarness.RunGeneratorDriver(compilation);
        var invalidDiagnostics = result.Diagnostics.Where(static diagnostic => diagnostic.Id == "AUDIT001").ToArray();

        // Assert
        invalidDiagnostics.ShouldHaveCount(2);
        result.RunResult.Results.Single().GeneratedSources.ShouldBeEmpty();
    }

    [Fact]
    public void Reports_duplicate_mappings_and_emits_no_handler_for_the_duplicated_event()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed record DocumentFinalizedDomainEvent(Guid DocumentId) : IDomainEvent;

            public sealed record DocumentAuditEntry(Guid DocumentId, DateTime OccurredAtUtc) : IAuditTrailEntry;

            public static class FirstMappings
            {
                [AuditTrailMapping]
                public static DocumentAuditEntry Map(DocumentFinalizedDomainEvent domainEvent, DateTimeOffset occurredAt) =>
                    new(domainEvent.DocumentId, occurredAt.UtcDateTime);
            }

            public static class SecondMappings
            {
                [AuditTrailMapping]
                public static DocumentAuditEntry Map(DocumentFinalizedDomainEvent domainEvent, DateTimeOffset occurredAt) =>
                    new(domainEvent.DocumentId, occurredAt.UtcDateTime);
            }
            """;
        var compilation = AuditTrailGeneratorTestHarness.CreateCompilation(source);

        // Act
        var result = AuditTrailGeneratorTestHarness.RunGeneratorDriver(compilation);
        var duplicateDiagnostics = result.Diagnostics.Where(static diagnostic => diagnostic.Id == "AUDIT002").ToArray();

        // Assert
        duplicateDiagnostics.ShouldHaveCount(2);
        duplicateDiagnostics.ShouldAllSatisfy(diagnostic => diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error));
        result.RunResult.Results.Single().GeneratedSources.ShouldBeEmpty();
    }

    [Fact]
    public void Reports_inaccessible_instance_and_generic_mapping_methods()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed record DocumentFinalizedDomainEvent(Guid DocumentId) : IDomainEvent;

            public sealed record DocumentAuditEntry(Guid DocumentId, DateTime OccurredAtUtc) : IAuditTrailEntry;

            public sealed class DocumentAuditMappings
            {
                [AuditTrailMapping]
                public DocumentAuditEntry MapInstance(DocumentFinalizedDomainEvent domainEvent, DateTimeOffset occurredAt) =>
                    new(domainEvent.DocumentId, occurredAt.UtcDateTime);

                [AuditTrailMapping]
                public static DocumentAuditEntry MapGeneric<T>(DocumentFinalizedDomainEvent domainEvent, DateTimeOffset occurredAt) =>
                    new(domainEvent.DocumentId, occurredAt.UtcDateTime);

                [AuditTrailMapping]
                private static DocumentAuditEntry MapPrivate(DocumentFinalizedDomainEvent domainEvent, DateTimeOffset occurredAt) =>
                    new(domainEvent.DocumentId, occurredAt.UtcDateTime);
            }
            """;
        var compilation = AuditTrailGeneratorTestHarness.CreateCompilation(source);

        // Act
        var result = AuditTrailGeneratorTestHarness.RunGeneratorDriver(compilation);
        var invalidDiagnostics = result.Diagnostics.Where(static diagnostic => diagnostic.Id == "AUDIT001").ToArray();

        // Assert
        invalidDiagnostics.ShouldHaveCount(3);
        result.RunResult.Results.Single().GeneratedSources.ShouldBeEmpty();
    }

    [Fact]
    public void Reports_a_mapping_declared_in_a_generic_containing_type()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed record DocumentFinalizedDomainEvent(Guid DocumentId) : IDomainEvent;
            public sealed record DocumentAuditEntry(Guid DocumentId, DateTime OccurredAtUtc) : IAuditTrailEntry;

            public static class DocumentAuditMappings<T>
            {
                [AuditTrailMapping]
                public static DocumentAuditEntry Map(DocumentFinalizedDomainEvent domainEvent, DateTimeOffset occurredAt) =>
                    new(domainEvent.DocumentId, occurredAt.UtcDateTime);
            }
            """;
        var compilation = AuditTrailGeneratorTestHarness.CreateCompilation(source);

        // Act
        var result = AuditTrailGeneratorTestHarness.RunGeneratorDriver(compilation);

        // Assert
        result.Diagnostics.ShouldContain(diagnostic => diagnostic.Id == "AUDIT001");
        result.RunResult.Results.Single().GeneratedSources.ShouldBeEmpty();
    }

    [Fact]
    public void Reports_a_mapping_declared_as_a_local_function()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed record DocumentFinalizedDomainEvent(Guid DocumentId) : IDomainEvent;
            public sealed record DocumentAuditEntry(Guid DocumentId, DateTime OccurredAtUtc) : IAuditTrailEntry;

            public static class DocumentAuditMappings
            {
                public static void Configure()
                {
                    [AuditTrailMapping]
                    static DocumentAuditEntry Map(DocumentFinalizedDomainEvent domainEvent, DateTimeOffset occurredAt) =>
                        new(domainEvent.DocumentId, occurredAt.UtcDateTime);
                }
            }
            """;
        var compilation = AuditTrailGeneratorTestHarness.CreateCompilation(source);

        // Act
        var result = AuditTrailGeneratorTestHarness.RunGeneratorDriver(compilation);

        // Assert
        result.Diagnostics.ShouldContain(diagnostic => diagnostic.Id == "AUDIT001");
        result.RunResult.Results.Single().GeneratedSources.ShouldBeEmpty();
    }

    [Fact]
    public void Reports_a_mapping_declared_as_a_static_abstract_interface_member()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed record DocumentFinalizedDomainEvent(Guid DocumentId) : IDomainEvent;
            public sealed record DocumentAuditEntry(Guid DocumentId, DateTime OccurredAtUtc) : IAuditTrailEntry;

            public interface IDocumentAuditMappings
            {
                [AuditTrailMapping]
                static abstract DocumentAuditEntry Map(
                    DocumentFinalizedDomainEvent domainEvent,
                    DateTimeOffset occurredAt);
            }
            """;
        var compilation = AuditTrailGeneratorTestHarness.CreateCompilation(source);

        // Act
        var result = AuditTrailGeneratorTestHarness.RunGeneratorDriver(compilation);

        // Assert
        result.Diagnostics.ShouldContain(diagnostic => diagnostic.Id == "AUDIT001");
        result.RunResult.Results.Single().GeneratedSources.ShouldBeEmpty();
    }

    [Fact]
    public void Reports_a_mapping_declared_as_a_static_virtual_interface_member()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed record DocumentFinalizedDomainEvent(Guid DocumentId) : IDomainEvent;
            public sealed record DocumentAuditEntry(Guid DocumentId, DateTime OccurredAtUtc) : IAuditTrailEntry;

            public interface IDocumentAuditMappings
            {
                [AuditTrailMapping]
                static virtual DocumentAuditEntry Map(
                    DocumentFinalizedDomainEvent domainEvent,
                    DateTimeOffset occurredAt) => new(domainEvent.DocumentId, occurredAt.UtcDateTime);
            }
            """;
        var compilation = AuditTrailGeneratorTestHarness.CreateCompilation(source);

        // Act
        var result = AuditTrailGeneratorTestHarness.RunGeneratorDriver(compilation);

        // Assert
        result.Diagnostics.ShouldContain(diagnostic => diagnostic.Id == "AUDIT001");
        result.RunResult.Results.Single().GeneratedSources.ShouldBeEmpty();
    }

    [Fact]
    public void Reports_nullable_event_and_entry_contracts()
    {
        // Arrange
        const string source = """
            #nullable enable
            namespace Demo;

            public sealed record FirstDomainEvent(Guid DocumentId) : IDomainEvent;
            public sealed record SecondDomainEvent(Guid DocumentId) : IDomainEvent;
            public sealed record DocumentAuditEntry(Guid DocumentId, DateTime OccurredAtUtc) : IAuditTrailEntry;

            public static class DocumentAuditMappings
            {
                [AuditTrailMapping]
                public static DocumentAuditEntry MapNullableEvent(FirstDomainEvent? domainEvent, DateTimeOffset occurredAt) =>
                    new(domainEvent!.DocumentId, occurredAt.UtcDateTime);

                [AuditTrailMapping]
                public static DocumentAuditEntry? MapNullableEntry(SecondDomainEvent domainEvent, DateTimeOffset occurredAt) =>
                    new(domainEvent.DocumentId, occurredAt.UtcDateTime);
            }
            """;
        var compilation = AuditTrailGeneratorTestHarness.CreateCompilation(source);

        // Act
        var result = AuditTrailGeneratorTestHarness.RunGeneratorDriver(compilation);
        var invalidDiagnostics = result.Diagnostics.Where(static diagnostic => diagnostic.Id == "AUDIT001").ToArray();

        // Assert
        invalidDiagnostics.ShouldHaveCount(2);
        result.RunResult.Results.Single().GeneratedSources.ShouldBeEmpty();
    }

    [Fact]
    public void Emits_global_escaped_identifiers()
    {
        // Arrange
        const string source = """
            namespace @event;

            public sealed record @class(Guid DocumentId) : IDomainEvent;
            public sealed record @struct(Guid DocumentId, DateTime OccurredAtUtc) : IAuditTrailEntry;

            public static class @internal
            {
                [AuditTrailMapping]
                public static @struct @return(@class domainEvent, DateTimeOffset occurredAt) =>
                    new(domainEvent.DocumentId, occurredAt.UtcDateTime);
            }
            """;
        var compilation = AuditTrailGeneratorTestHarness.CreateCompilation(source);

        // Act
        var result = AuditTrailGeneratorTestHarness.RunGeneratorDriver(compilation);
        var generatedSource = AuditTrailGeneratorTestHarness.GetGeneratedSource(result.RunResult);
        var errors = result.OutputCompilation.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();

        // Assert
        result.Diagnostics.ShouldBeEmpty();
        errors.ShouldBeEmpty();
        generatedSource.ShouldContain("global::@event.@internal.@return", StringComparison.Ordinal);
    }

    [Fact]
    public void Dispatches_base_and_derived_event_mappings_by_exact_runtime_type()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public record BaseDomainEvent(Guid DocumentId) : IDomainEvent;
            public sealed record DerivedDomainEvent(Guid DocumentId) : BaseDomainEvent(DocumentId);
            public sealed record BaseAuditEntry(Guid DocumentId, DateTime OccurredAtUtc) : IAuditTrailEntry;
            public sealed record DerivedAuditEntry(Guid DocumentId, DateTime OccurredAtUtc) : IAuditTrailEntry;

            public static class DocumentAuditMappings
            {
                [AuditTrailMapping]
                public static BaseAuditEntry MapBase(BaseDomainEvent domainEvent, DateTimeOffset occurredAt) =>
                    new(domainEvent.DocumentId, occurredAt.UtcDateTime);

                [AuditTrailMapping]
                public static DerivedAuditEntry MapDerived(DerivedDomainEvent domainEvent, DateTimeOffset occurredAt) =>
                    new(domainEvent.DocumentId, occurredAt.UtcDateTime);
            }
            """;
        var compilation = AuditTrailGeneratorTestHarness.CreateCompilation(source);

        // Act
        var result = AuditTrailGeneratorTestHarness.RunGeneratorDriver(compilation);
        var generatedSource = AuditTrailGeneratorTestHarness.GetGeneratedSource(result.RunResult);

        // Assert
        result.Diagnostics.ShouldBeEmpty();
        generatedSource.ShouldContain("domainEvent.GetType() == typeof(global::Demo.BaseDomainEvent)", StringComparison.Ordinal);
        generatedSource.ShouldContain("domainEvent.GetType() == typeof(global::Demo.DerivedDomainEvent)", StringComparison.Ordinal);
    }

    [Fact]
    public void Reports_invalid_event_entry_and_timestamp_contracts()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed record NotADomainEvent(Guid DocumentId);
            public sealed record DocumentFinalizedDomainEvent(Guid DocumentId) : IDomainEvent;
            public sealed record NotAnAuditEntry(Guid DocumentId);
            public sealed record DocumentAuditEntry(Guid DocumentId, DateTime OccurredAtUtc) : IAuditTrailEntry;

            public static class DocumentAuditMappings
            {
                [AuditTrailMapping]
                public static DocumentAuditEntry MapEvent(NotADomainEvent domainEvent, DateTimeOffset occurredAt) =>
                    new(domainEvent.DocumentId, occurredAt.UtcDateTime);

                [AuditTrailMapping]
                public static NotAnAuditEntry MapEntry(DocumentFinalizedDomainEvent domainEvent, DateTimeOffset occurredAt) =>
                    new(domainEvent.DocumentId);

                [AuditTrailMapping]
                public static DocumentAuditEntry MapTimestamp(DocumentFinalizedDomainEvent domainEvent, DateTime occurredAt) =>
                    new(domainEvent.DocumentId, occurredAt);
            }
            """;
        var compilation = AuditTrailGeneratorTestHarness.CreateCompilation(source);

        // Act
        var result = AuditTrailGeneratorTestHarness.RunGeneratorDriver(compilation);
        var invalidDiagnostics = result.Diagnostics.Where(static diagnostic => diagnostic.Id == "AUDIT001").ToArray();

        // Assert
        invalidDiagnostics.ShouldHaveCount(3);
        result.RunResult.Results.Single().GeneratedSources.ShouldBeEmpty();
    }

    [Fact]
    public void Emits_valid_handlers_when_invalid_mappings_are_also_present()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed record DocumentFinalizedDomainEvent(Guid DocumentId) : IDomainEvent;
            public sealed record DocumentAuditEntry(Guid DocumentId, DateTime OccurredAtUtc) : IAuditTrailEntry;

            public static class DocumentAuditMappings
            {
                [AuditTrailMapping]
                public static DocumentAuditEntry MapValid(DocumentFinalizedDomainEvent domainEvent, DateTimeOffset occurredAt) =>
                    new(domainEvent.DocumentId, occurredAt.UtcDateTime);

                [AuditTrailMapping]
                public static DocumentAuditEntry MapInvalid(DocumentFinalizedDomainEvent domainEvent) =>
                    new(domainEvent.DocumentId, DateTime.UnixEpoch);
            }
            """;
        var compilation = AuditTrailGeneratorTestHarness.CreateCompilation(source);

        // Act
        var result = AuditTrailGeneratorTestHarness.RunGeneratorDriver(compilation);
        var generatedSource = AuditTrailGeneratorTestHarness.GetGeneratedSource(result.RunResult);

        // Assert
        result.Diagnostics.ShouldContain(diagnostic => diagnostic.Id == "AUDIT001");
        generatedSource.ShouldContain("MapValid((global::Demo.DocumentFinalizedDomainEvent)domainEvent", StringComparison.Ordinal);
        generatedSource.ShouldNotContain("MapInvalid(", StringComparison.Ordinal);
    }

    [Fact]
    public void Generates_handlers_in_deterministic_mapping_order()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed record ZuluDomainEvent(Guid Id) : IDomainEvent;
            public sealed record AlphaDomainEvent(Guid Id) : IDomainEvent;
            public sealed record ZuluAuditEntry(Guid Id, DateTime OccurredAtUtc) : IAuditTrailEntry;
            public sealed record AlphaAuditEntry(Guid Id, DateTime OccurredAtUtc) : IAuditTrailEntry;

            public static class ZuluMappings
            {
                [AuditTrailMapping]
                public static ZuluAuditEntry Map(ZuluDomainEvent domainEvent, DateTimeOffset occurredAt) =>
                    new(domainEvent.Id, occurredAt.UtcDateTime);
            }

            public static class AlphaMappings
            {
                [AuditTrailMapping]
                public static AlphaAuditEntry Map(AlphaDomainEvent domainEvent, DateTimeOffset occurredAt) =>
                    new(domainEvent.Id, occurredAt.UtcDateTime);
            }
            """;
        var compilation = AuditTrailGeneratorTestHarness.CreateCompilation(source);

        // Act
        var result = AuditTrailGeneratorTestHarness.RunGeneratorDriver(compilation);
        var generatedSource = AuditTrailGeneratorTestHarness.GetGeneratedSource(result.RunResult);
        var alphaIndex = generatedSource.IndexOf("IAuditTrailSink<global::Demo.AlphaAuditEntry>", StringComparison.Ordinal);
        var zuluIndex = generatedSource.IndexOf("IAuditTrailSink<global::Demo.ZuluAuditEntry>", StringComparison.Ordinal);

        // Assert
        result.Diagnostics.ShouldBeEmpty();
        alphaIndex.ShouldBeGreaterThanOrEqualTo(0);
        zuluIndex.ShouldBeGreaterThan(alphaIndex);
    }

    [Fact]
    public void Emits_no_source_when_no_mapping_is_attributed()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed record DocumentFinalizedDomainEvent(Guid DocumentId) : IDomainEvent;
            """;
        var compilation = AuditTrailGeneratorTestHarness.CreateCompilation(source);

        // Act
        var result = AuditTrailGeneratorTestHarness.RunGeneratorDriver(compilation);

        // Assert
        result.Diagnostics.ShouldBeEmpty();
        result.RunResult.Results.Single().GeneratedSources.ShouldBeEmpty();
    }
}
