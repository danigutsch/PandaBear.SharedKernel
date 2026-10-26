using PandaBear.Testing.Packaging;

namespace PandaBear.AuditTrail.GeneratorTests;

internal static class AuditTrailConsumerProject
{
    public static void Write(PackageConsumptionWorkspace workspace) =>
        workspace.WriteProject(
            $$"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
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
            using PandaBear.AuditTrail;
            using PandaBear.Domain;

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
