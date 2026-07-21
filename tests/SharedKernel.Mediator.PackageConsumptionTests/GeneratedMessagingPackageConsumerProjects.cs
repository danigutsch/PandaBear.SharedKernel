namespace SharedKernel.Mediator.PackageConsumptionTests;

internal static class GeneratedMessagingPackageConsumerProjects
{
    public static void Write(PackageConsumptionWorkspace workspace, MediatorPackageFeedFixture packageFeed)
    {
        workspace.WriteProject(
            $$"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
                <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
                <CompilerGeneratedFilesOutputPath>Generated</CompilerGeneratedFilesOutputPath>
              </PropertyGroup>
              <ItemGroup>
                {{workspace.GetPackageReference("SharedKernel.DomainEvents")}}
                {{workspace.GetPackageReference("SharedKernel.Mediator")}}
                {{workspace.GetPackageReference("SharedKernel.Mediator.SourceGenerator", "PrivateAssets=\"all\" IncludeAssets=\"build;analyzers;buildTransitive\"")}}
                {{workspace.GetPackageReference("SharedKernel.Messaging.IntegrationEvents")}}
                {{workspace.GetPackageReference("SharedKernel.Messaging.IntegrationEvents.SourceGenerator", "PrivateAssets=\"all\" IncludeAssets=\"build;analyzers;buildTransitive\"")}}
                <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="{{packageFeed.DependencyInjectionPackageVersion}}" />
              </ItemGroup>
            </Project>
            """,
            ("Consumer.cs", """
            using System.Text.Json.Serialization;
            using System.Text.Json.Serialization.Metadata;
            using Microsoft.Extensions.DependencyInjection;
            using SharedKernel.Domain;
            using SharedKernel.Mediator;
            using SharedKernel.Messaging.IntegrationEvents;

            [assembly: MediatorModule]

            namespace Consumer;

            public sealed record TourCreatedDomainEvent(Guid TourId) : IDomainEvent;

            public sealed record TourCreatedIntegrationEvent(Guid EventId, DateTimeOffset OccurredAt, Guid TourId) : IIntegrationEvent
            {
                public static string EventType => "tour.created";
                public static int EventVersion => 1;
            }

            public sealed class TourCreatedIntegrationEventHandler : IIntegrationEventHandler<TourCreatedIntegrationEvent>
            {
                public Guid? HandledTourId { get; private set; }

                public ValueTask Handle(TourCreatedIntegrationEvent integrationEvent, CancellationToken ct)
                {
                    HandledTourId = integrationEvent.TourId;
                    return ValueTask.CompletedTask;
                }
            }

            [JsonSerializable(typeof(TourCreatedIntegrationEvent))]
            internal partial class ConsumerJsonContext : JsonSerializerContext;

            public static class TourMappings
            {
                [IntegrationEventMapping]
                public static TourCreatedIntegrationEvent Map(TourCreatedDomainEvent domainEvent, Guid eventId, DateTimeOffset occurredAt) =>
                    new(eventId, occurredAt, domainEvent.TourId);
            }

            public static class Registration
            {
                public static IServiceCollection AddMessaging(IServiceCollection services)
                {
                    JsonTypeInfo<TourCreatedIntegrationEvent> jsonTypeInfo =
                        ConsumerJsonContext.Default.TourCreatedIntegrationEvent;
                    return services.AddIntegrationEventConsumer<TourCreatedIntegrationEvent>(
                        TourCreatedIntegrationEvent.EventType,
                        jsonTypeInfo);
                }
            }
            """),
            ("Program.cs", """
            using Consumer;
            using Microsoft.Extensions.DependencyInjection;
            using SharedKernel.Messaging;
            using SharedKernel.Messaging.IntegrationEvents;

            var services = new ServiceCollection();
            services.AddScoped<TourCreatedIntegrationEventHandler>();
            services.AddScoped<IIntegrationEventHandler<TourCreatedIntegrationEvent>>(static provider =>
                provider.GetRequiredService<TourCreatedIntegrationEventHandler>());
            Registration.AddMessaging(services);
            services.AddGeneratedIntegrationEvents();

            using var provider = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateScopes = true
            });
            using var scope = provider.CreateScope();
            var eventId = Guid.Parse("019bfab5-71f0-7d00-bc31-e8bf2f9c7812");
            var tourId = Guid.Parse("019bfab5-71f0-7d01-940b-e857478d0a32");
            var occurredAt = DateTimeOffset.Parse("2026-07-19T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
            var integrationEvent = new TourCreatedIntegrationEvent(eventId, occurredAt, tourId);
            var serializer = scope.ServiceProvider.GetRequiredService<IIntegrationEventSerializer>();
            var publisher = scope.ServiceProvider.GetRequiredService<IEventEnvelopePublisher>();
            var payload = serializer.Serialize(integrationEvent);
            var envelope = new EventEnvelope(
                "cloudevents",
                "1.0",
                eventId.ToString(),
                new Uri("urn:test:package-consumer"),
                TourCreatedIntegrationEvent.EventType,
                TourCreatedIntegrationEvent.EventVersion,
                occurredAt,
                null,
                "application/json",
                null,
                payload,
                EventPayloadEncoding.Json,
                null);

            await publisher.Publish(envelope, CancellationToken.None);
            var handler = scope.ServiceProvider.GetRequiredService<TourCreatedIntegrationEventHandler>();
            Console.WriteLine($"handled={handler.HandledTourId};payload={(payload.Contains(tourId.ToString(), StringComparison.Ordinal) ? "true" : "false")}");
            """));
    }
}
