using SharedKernel.Testing.Packaging;

namespace SharedKernel.AuditTrail.GeneratorTests;

/// <summary>
/// Writes the consumer project used by the audit trail package consumption tests.
/// </summary>
internal static class AuditTrailConsumerProject
{
    /// <summary>
    /// Writes a project for the framework of the running tests that references the audit trail source generator package, plus a Consumer.cs with a BookingConfirmed event, a BookingAuditEntry and an AuditTrailMapping method, into the workspace.
    /// </summary>
    public static void Write(PackageConsumptionWorkspace workspace) =>
        workspace.WriteProject(
            $$"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>{{TestTargetFramework.Moniker}}</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
                <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
                <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
                <CompilerGeneratedFilesOutputPath>Generated</CompilerGeneratedFilesOutputPath>
              </PropertyGroup>
              <ItemGroup>
                <Compile Remove="Generated/**/*.cs" />
              </ItemGroup>
              <ItemGroup>
                {{workspace.GetPackageReference("PandaBear.SharedKernel.AuditTrail.SourceGenerator", "PrivateAssets=\"all\"")}}
              </ItemGroup>
            </Project>
            """,
            ("Consumer.cs", """
            using Microsoft.Extensions.DependencyInjection;
            using SharedKernel.AuditTrail;
            using SharedKernel.Domain;

            namespace Consumer;

            public sealed record BookingConfirmed(Guid BookingId) : IDomainEvent;

            public sealed record BookingAuditEntry(Guid BookingId, DateTime OccurredAtUtc) : IAuditTrailEntry;

            public static class BookingAuditMappings
            {
                [AuditTrailMapping]
                public static BookingAuditEntry Map(BookingConfirmed domainEvent, DateTimeOffset occurredAt) =>
                    new(domainEvent.BookingId, occurredAt.UtcDateTime);

                public static IServiceCollection Register(IServiceCollection services) =>
                    services.AddGeneratedAuditTrailMappings();
            }
            """));
}
