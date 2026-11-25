using Microsoft.CodeAnalysis;

namespace SharedKernel.Mediator.SourceGenerator;

internal sealed record DuplicateRegistrationDiagnostic(
    string ServiceType,
    string ImplementationType,
    string ReportingMetadataName,
    Location Location);
