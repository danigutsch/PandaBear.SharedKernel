namespace SharedKernel.Mediator.PackageConsumptionTests;

internal static class GeneratedMediatorPackageConsumerProjects
{
    public static void Write(PackageConsumptionWorkspace workspace, MediatorPackageFeedFixture packageFeed)
    {
        var consumerFiles = new (string FileName, string Content)[]
        {
            ("Consumer.cs", """
            using System.Collections.Generic;
            using System.Runtime.CompilerServices;
            using Microsoft.Extensions.DependencyInjection;
            using SharedKernel.Mediator;

            namespace Consumer;

            public sealed record LookupTour(string Code) : IQuery<string>;

            public sealed record ResolveTourCode(string Code) : IQuery<string>;

            internal sealed class ResolveTourCodeHandler : IQueryHandler<ResolveTourCode, string>
            {
                public ValueTask<string> Handle(ResolveTourCode request, CancellationToken ct)
                {
                    return ValueTask.FromResult(request.Code);
                }
            }

            internal sealed class LookupTourHandler(ISender sender) : IQueryHandler<LookupTour, string>
            {
                public ValueTask<string> Handle(LookupTour request, CancellationToken ct)
                {
                    return sender.Send(new ResolveTourCode(request.Code), ct);
                }
            }

            public sealed record TourFound(string Code) : INotification;

            public sealed class TourFoundHandler : INotificationHandler<TourFound>
            {
                public ValueTask Handle(TourFound notification, CancellationToken ct)
                {
                    Console.WriteLine($"notification-handled={notification.Code}");
                    return ValueTask.CompletedTask;
                }
            }

            public sealed record StreamTourCodes(int Count) : IStreamQuery<string>;

            public sealed class StreamTourCodesHandler : IStreamRequestHandler<StreamTourCodes, string>
            {
                public async IAsyncEnumerable<string> Handle(StreamTourCodes request, [EnumeratorCancellation] CancellationToken ct)
                {
                    for (var i = 0; i < request.Count; i++)
                    {
                        yield return $"VT-{i:D4}";
                        await Task.Yield();
                    }
                }
            }

            public sealed class TimingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
                where TRequest : IRequest<TResponse>
            {
                public async ValueTask<TResponse> Handle(TRequest request, RequestHandlerContinuation<TResponse> next, CancellationToken ct)
                {
                    Console.WriteLine($"pipeline-before={typeof(TRequest).Name}");
                    var response = await next();
                    Console.WriteLine($"pipeline-after={typeof(TRequest).Name}");
                    return response;
                }
            }
            """),
            ("MediatorHarness.cs", MediatorConsumerSourceTemplates.CreateHarness("Consumer")),
            ("Program.cs", """
            using System.Diagnostics;
            using System.Globalization;
            using Consumer;
            using Microsoft.Extensions.DependencyInjection;
            using SharedKernel.Mediator;

            using var harness = MediatorHarness.Create();

            var firstDispatchStopwatch = Stopwatch.StartNew();
            var firstDispatchResult = await harness.Mediator.Send(new LookupTour("VT-42"), CancellationToken.None);
            firstDispatchStopwatch.Stop();

            await harness.Mediator.Publish(new TourFound(firstDispatchResult), CancellationToken.None);

            var streamCount = 0;
            await foreach (var code in harness.Mediator.Send(new StreamTourCodes(3), CancellationToken.None))
            {
                streamCount++;
                _ = code;
            }

            Console.WriteLine($"stream-count={streamCount}");

            const int steadyStateIterations = 200;
            var steadyStateStopwatch = Stopwatch.StartNew();

            for (var iteration = 0; iteration < steadyStateIterations; iteration++)
            {
                _ = await harness.Mediator.Send(new LookupTour("VT-42"), CancellationToken.None);
            }

            steadyStateStopwatch.Stop();

            Console.WriteLine($"result={firstDispatchResult}");
            Console.WriteLine(FormattableString.Invariant($"first-dispatch-ms={firstDispatchStopwatch.Elapsed.TotalMilliseconds:F4}"));
            Console.WriteLine(FormattableString.Invariant($"steady-state-dispatch-ms={steadyStateStopwatch.Elapsed.TotalMilliseconds / steadyStateIterations:F4}"));

            var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
            services.AddSharedKernelMediator();
            using var provider = services.BuildServiceProvider();
            using var firstScope = provider.CreateScope();
            var firstHandler = firstScope.ServiceProvider.GetRequiredService<LookupTourHandler>();
            var repeatedHandler = firstScope.ServiceProvider.GetRequiredService<LookupTourHandler>();
            var firstPipeline = firstScope.ServiceProvider.GetRequiredService<TimingBehavior<LookupTour, string>>();
            var repeatedPipeline = firstScope.ServiceProvider.GetRequiredService<TimingBehavior<LookupTour, string>>();
            using var secondScope = provider.CreateScope();
            var secondHandler = secondScope.ServiceProvider.GetRequiredService<LookupTourHandler>();
            var secondPipeline = secondScope.ServiceProvider.GetRequiredService<TimingBehavior<LookupTour, string>>();
            var sameWithinScope = ReferenceEquals(firstHandler, repeatedHandler) &&
                ReferenceEquals(firstPipeline, repeatedPipeline);
            var differentAcrossScopes = !ReferenceEquals(firstHandler, secondHandler) &&
                !ReferenceEquals(firstPipeline, secondPipeline);
            Console.WriteLine($"handler-pipeline-same-within-scope={(sameWithinScope ? "true" : "false")}");
            Console.WriteLine($"handler-pipeline-different-across-scopes={(differentAcrossScopes ? "true" : "false")}");
            """)
        };

        workspace.WriteProject(
            $$"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <OutputType>Exe</OutputType>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
                <IsAotCompatible>true</IsAotCompatible>
                <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
                <CompilerGeneratedFilesOutputPath>Generated</CompilerGeneratedFilesOutputPath>
              </PropertyGroup>
              <ItemGroup>
                {{workspace.GetPackageReference("SharedKernel.Mediator.Abstractions")}}
                {{workspace.GetPackageReference("SharedKernel.Mediator")}}
                {{workspace.GetPackageReference("SharedKernel.Mediator.SourceGenerator", "PrivateAssets=\"all\" IncludeAssets=\"build;analyzers;buildTransitive\"")}}
                <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="{{packageFeed.DependencyInjectionPackageVersion}}" />
              </ItemGroup>
            </Project>
            """,
            consumerFiles);
    }
}
