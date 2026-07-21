using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SharedKernel.Mediator.GeneratorTests;

[Trait(Testing.SharedKernelTestTraitNames.CapabilityName, TestTraits.DispatchCapability)]
public sealed class GeneratorDispatchTests
{
    [Fact]
    public void Omit_domain_event_notification_discovery_and_registration()
    {
        // Arrange
        const string source = """
            using SharedKernel.Domain;
            using SharedKernel.Mediator;

            [assembly: MediatorModule]

            namespace SharedKernel.DomainEvents
            {
                public interface IDomainEventHandler<in TDomainEvent>
                    where TDomainEvent : IDomainEvent;
            }

            namespace Demo
            {
                public sealed record TourCreated(Guid TourId) : IDomainEvent;

                public sealed class TourCreatedHandler : SharedKernel.DomainEvents.IDomainEventHandler<TourCreated>
                {
                    public ValueTask Handle(TourCreated domainEvent, CancellationToken ct) => ValueTask.CompletedTask;
                }
            }
            """;
        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(Domain.IDomainEvent).Assembly.Location),
        };
        var compilation = GeneratorTestHarness.CreateCompilation(source, additionalReferences: references);

        // Act
        var runResult = GeneratorTestHarness.RunGeneratorDriver(compilation);
        var generatedHintNames = runResult.Results
            .SelectMany(static result => result.GeneratedSources)
            .Select(static generatedSource => generatedSource.HintName)
            .ToArray();
        var domainEventDispatcherRegistrations = runResult.Results
            .SelectMany(static result => result.GeneratedSources)
            .SelectMany(static generatedSource => generatedSource.SourceText.Lines)
            .Select(static line => line.ToString())
            .Count(static line =>
                line.Contains("ServiceCollectionDescriptorExtensions.TryAdd", StringComparison.Ordinal) &&
                line.Contains("IDomainEventDispatcher", StringComparison.Ordinal));
        var dependencyInjectionSource = GeneratorTestHarness.GetGeneratedSource(
            runResult,
            GeneratedHintNames.DependencyInjection);
        // Assert
        generatedHintNames.ShouldNotContain("SharedKernel.DomainEvents.Generated.DomainEventNotifications.g.cs");
        domainEventDispatcherRegistrations.ShouldBe(0);
        dependencyInjectionSource.ShouldNotContain("SharedKernel.DomainEvents", StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_appmediator_shell()
    {
        // Arrange
        const string source = """
            using SharedKernel.Mediator;

            [assembly: MediatorModule]

            namespace Demo;

            public sealed class LookupTour : IRequest<string>
            {
                public LookupTour(string code) => Code = code;

                public string Code { get; }
            }

            public sealed class LookupTourHandler : IRequestHandler<LookupTour, string>
            {
                public ValueTask<string> Handle(LookupTour request, CancellationToken ct) => ValueTask.FromResult(request.Code);
            }

            public sealed record CreateTour(string Name) : ICommand<int>;

            public sealed class CreateTourHandler : ICommandHandler<CreateTour, int>
            {
                public ValueTask<int> Handle(CreateTour request, CancellationToken ct) => ValueTask.FromResult(42);
            }

            public sealed record DeleteTour(int Id) : ICommand;

            public sealed class DeleteTourHandler : ICommandHandler<DeleteTour>
            {
                public ValueTask<Unit> Handle(DeleteTour request, CancellationToken ct) => ValueTask.FromResult(Unit.Value);
            }

            public readonly record struct GetTourById(int Id) : IQuery<string>;

            public sealed class GetTourByIdHandler : IQueryHandler<GetTourById, string>
            {
                public ValueTask<string> Handle(GetTourById request, CancellationToken ct) => ValueTask.FromResult(request.Id.ToString());
            }

            public sealed record TourCreated(int Id) : INotification;

            public sealed class TourCreatedHandler : INotificationHandler<TourCreated>
            {
                public ValueTask Handle(TourCreated notification, CancellationToken ct) => ValueTask.CompletedTask;
            }

            public sealed record StreamTours(int Count) : IStreamRequest<string>;

            public sealed class StreamToursHandler : IStreamRequestHandler<StreamTours, string>
            {
                public async IAsyncEnumerable<string> Handle(StreamTours request, CancellationToken ct)
                {
                    await Task.Yield();
                    yield return request.Count.ToString();
                }
            }

            public sealed record MissingTour(int Id) : IQuery<string>;
            """;
        var compilation = GeneratorTestHarness.CreateCompilation(source);

        // Act
        var runResult = GeneratorTestHarness.RunGeneratorDriver(compilation);
        var generatedSource = GeneratorTestHarness.GetGeneratedSource(
            runResult,
            GeneratedHintNames.AppMediator);
        var generatedDispatchSource = GeneratorTestHarness.GetGeneratedSource(
            runResult,
            GeneratedHintNames.GeneratedDispatch);
        var generatedPipelinesSource = GeneratorTestHarness.GetGeneratedSource(
            runResult,
            GeneratedHintNames.GeneratedPipelines);
        var generatedDispatchFiles = new[]
        {
            generatedSource,
            generatedDispatchSource,
            generatedPipelinesSource
        };
        var generatedRoots = generatedDispatchFiles
            .Select(sourceText => CSharpSyntaxTree.ParseText(
                sourceText,
                cancellationToken: TestContext.Current.CancellationToken).GetRoot(TestContext.Current.CancellationToken))
            .ToArray();
        var generatedTypes = generatedRoots
            .SelectMany(static root => root.DescendantNodes().OfType<TypeDeclarationSyntax>())
            .ToArray();
        var topLevelTypeCount = generatedTypes.Count(static type =>
            type.Parent is CompilationUnitSyntax or BaseNamespaceDeclarationSyntax);
        var nestedTypeCount = generatedTypes.Length - topLevelTypeCount;
        var registrationMethodCount = generatedRoots
            .SelectMany(static root => root.DescendantNodes().OfType<MethodDeclarationSyntax>())
            .Count(static method => method.Identifier.ValueText.StartsWith("Add", StringComparison.Ordinal));
        var nonBlankLineCount = generatedDispatchFiles.Sum(static sourceText => sourceText.Split('\n')
            .Count(static line => !string.IsNullOrWhiteSpace(line)));
        var serviceProviderSiteCount = generatedDispatchFiles.Sum(static sourceText =>
            sourceText.Split("IServiceProvider").Length - 1
            + sourceText.Split("GetRequiredService").Length - 1);

        // Assert
        GeneratorSnapshotVerifier.Verify(generatedSource);
        GeneratorSnapshotVerifier.Verify(generatedDispatchSource, testName: "Generate_GeneratedDispatch_Shell");
        (generatedSource).ShouldContain("internal sealed partial class AppMediator : IMediator", StringComparison.Ordinal);
        (generatedSource).ShouldNotContain("global::System.IServiceProvider", StringComparison.Ordinal);
        (generatedSource).ShouldNotContain("GetRequiredService", StringComparison.Ordinal);
        (generatedSource).ShouldContain("public async global::System.Threading.Tasks.ValueTask<string> Send(global::Demo.LookupTour request,", StringComparison.Ordinal);
        (generatedSource).ShouldContain("public async global::System.Threading.Tasks.ValueTask<int> Send(global::Demo.CreateTour request,", StringComparison.Ordinal);
        (generatedSource).ShouldContain("public async global::System.Threading.Tasks.ValueTask<global::SharedKernel.Mediator.Unit> Send(global::Demo.DeleteTour request,", StringComparison.Ordinal);
        (generatedSource).ShouldContain("public async global::System.Threading.Tasks.ValueTask<string> Send(global::Demo.GetTourById request,", StringComparison.Ordinal);
        (generatedSource).ShouldContain("public global::System.Collections.Generic.IAsyncEnumerable<string> Send(global::Demo.StreamTours request,", StringComparison.Ordinal);
        (generatedSource).ShouldContain("public global::System.Collections.Generic.IAsyncEnumerable<TResponse> Send<TResponse>(", StringComparison.Ordinal);
        (generatedSource).ShouldContain("return GeneratedDispatch.Send<TResponse>(this, request, ct);", StringComparison.Ordinal);
        (generatedSource).ShouldContain("return GeneratedDispatch.Send<TResponse>(this, request, ct);", StringComparison.Ordinal);
        (generatedSource).ShouldContain("return GeneratedDispatch.Publish(this, notification, ct);", StringComparison.Ordinal);
        (generatedSource).ShouldContain("public global::System.Threading.Tasks.ValueTask<object?> SendObject(", StringComparison.Ordinal);
        (generatedSource).ShouldContain("throw new global::System.NotSupportedException($\"Generated request dispatch is not available for request type '{request.GetType().FullName}'.\");", StringComparison.Ordinal);
        (generatedSource).ShouldContain("global::SharedKernel.Mediator.MediatorTelemetry.ActivitySend", StringComparison.Ordinal);
        (generatedSource).ShouldContain("global::SharedKernel.Mediator.MediatorTelemetry.TagRequestName", StringComparison.Ordinal);
        (generatedSource).ShouldContain("global::SharedKernel.Mediator.MediatorTelemetry.TagOutcome", StringComparison.Ordinal);
        (generatedSource).ShouldContain("global::SharedKernel.Mediator.MediatorTelemetry.OutcomeSuccess", StringComparison.Ordinal);
        (generatedSource).ShouldNotContain("internal static class GeneratedDispatch", StringComparison.Ordinal);
        (generatedDispatchSource).ShouldContain("internal static class GeneratedDispatch", StringComparison.Ordinal);
        (generatedPipelinesSource).ShouldContain("internal static class GeneratedPipelines", StringComparison.Ordinal);
        (generatedDispatchSource).ShouldContain("global::Demo.LookupTour typed => Cast<string, TResponse>(mediator.Send(typed, ct)),", StringComparison.Ordinal);
        (generatedDispatchSource).ShouldContain("global::Demo.CreateTour typed => Cast<int, TResponse>(mediator.Send(typed, ct)),", StringComparison.Ordinal);
        (generatedDispatchSource).ShouldContain("global::Demo.DeleteTour typed => Cast<global::SharedKernel.Mediator.Unit, TResponse>(mediator.Send(typed, ct)),", StringComparison.Ordinal);
        (generatedDispatchSource).ShouldContain("global::Demo.GetTourById typed => Cast<string, TResponse>(mediator.Send(typed, ct)),", StringComparison.Ordinal);
        (generatedDispatchSource).ShouldContain("global::Demo.LookupTour typed => Box<string>(mediator.Send(typed, ct)),", StringComparison.Ordinal);
        (generatedDispatchSource).ShouldContain("public static global::System.Collections.Generic.IAsyncEnumerable<TResponse> Send<TResponse>(", StringComparison.Ordinal);
        (generatedDispatchSource).ShouldContain("global::Demo.StreamTours typed => CastStream<string, TResponse>(mediator.Send(typed, ct), ct),", StringComparison.Ordinal);
        (generatedDispatchSource).ShouldContain("public static global::System.Threading.Tasks.ValueTask Publish<TNotification>(", StringComparison.Ordinal);
        (generatedDispatchSource).ShouldContain("global::Demo.TourCreated typed => Publish_0000(mediator, typed, ct),", StringComparison.Ordinal);
        (generatedDispatchSource).ShouldContain("await mediator.NotificationHandler_0000_0000.Handle(notification, ct).ConfigureAwait(false);", StringComparison.Ordinal);
        (generatedDispatchSource).ShouldNotContain("GetRequiredService", StringComparison.Ordinal);
        (generatedDispatchSource).ShouldContain("public static global::System.Threading.Tasks.ValueTask<TResponse> ThrowNoHandler<TResponse>(", StringComparison.Ordinal);
        (generatedDispatchSource).ShouldContain("public static global::System.Collections.Generic.IAsyncEnumerable<TResponse> ThrowNoStreamHandler<TResponse>(", StringComparison.Ordinal);
        (generatedDispatchSource).ShouldContain("public static global::System.Threading.Tasks.ValueTask<object?> ThrowUnknownRequestObject(", StringComparison.Ordinal);
        (generatedDispatchSource).ShouldContain("public static TTarget ThrowInvalidResponseCast<TSource, TTarget>()", StringComparison.Ordinal);
        generatedDispatchFiles.Length.ShouldBe(3);
        topLevelTypeCount.ShouldBe(3);
        nestedTypeCount.ShouldBe(1);
        registrationMethodCount.ShouldBe(0);
        nonBlankLineCount.ShouldBeGreaterThan(0);
        serviceProviderSiteCount.ShouldBe(0);
    }

    [Fact]
    public void Generate_appmediator_uses_generated_pipeline_helper_when_pipelines_exist()
    {
        // Arrange
        var source = TestSources.ModuleHeader
            + TestSources.CreateTourWithHandler
            + """
            [PipelineOrder(PipelineStage.Validation, Order = 5)]
            public sealed class ValidationBehavior : IPipelineBehavior<CreateTour, int>
            {
                public ValueTask<int> Handle(CreateTour request, RequestHandlerContinuation<int> next, CancellationToken ct) => next();
            }
            """;
        var compilation = GeneratorTestHarness.CreateCompilation(source);

        // Act
        var runResult = GeneratorTestHarness.RunGeneratorDriver(compilation);
        var generatedMediatorSource = GeneratorTestHarness.GetGeneratedSource(
            runResult,
            GeneratedHintNames.AppMediator);
        var generatedPipelinesSource = GeneratorTestHarness.GetGeneratedSource(
            runResult,
            GeneratedHintNames.GeneratedPipelines);

        // Assert
        GeneratorSnapshotVerifier.Verify(generatedPipelinesSource);
        (generatedMediatorSource).ShouldContain("var result = await GeneratedPipelines.Invoke_0000(this, request, ct).ConfigureAwait(false);", StringComparison.Ordinal);
        (generatedPipelinesSource).ShouldContain("public static global::System.Threading.Tasks.ValueTask<int> Invoke_0000(AppMediator mediator, global::Demo.CreateTour request,", StringComparison.Ordinal);
        (generatedPipelinesSource).ShouldContain("var pipeline0 = mediator.RequestPipeline_0000_0000;", StringComparison.Ordinal);
        (generatedPipelinesSource).ShouldContain("var handler = mediator.RequestHandler_0000;", StringComparison.Ordinal);
        (generatedPipelinesSource).ShouldNotContain("GetRequiredService", StringComparison.Ordinal);
        (generatedPipelinesSource).ShouldContain("return pipeline0.Handle(request, () => handler.Handle(request, ct), ct);", StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_appmediator_uses_generated_stream_pipeline_helper_when_stream_pipelines_exist()
    {
        // Arrange
        var source = TestSources.ModuleHeader
            + TestSources.StreamToursWithHandler
            + """
            [PipelineOrder(PipelineStage.Validation, Order = 5)]
            public sealed class ValidationBehavior : IStreamPipelineBehavior<StreamTours, string>
            {
                public IAsyncEnumerable<string> Handle(StreamTours request, StreamHandlerContinuation<string> next, CancellationToken ct) => next();
            }
            """;
        var compilation = GeneratorTestHarness.CreateCompilation(source);

        // Act
        var runResult = GeneratorTestHarness.RunGeneratorDriver(compilation);
        var generatedMediatorSource = GeneratorTestHarness.GetGeneratedSource(
            runResult,
            GeneratedHintNames.AppMediator);
        var generatedPipelinesSource = GeneratorTestHarness.GetGeneratedSource(
            runResult,
            GeneratedHintNames.GeneratedPipelines);

        // Assert
        GeneratorSnapshotVerifier.Verify(generatedPipelinesSource);
        (generatedMediatorSource).ShouldContain("var enumerator = GeneratedPipelines.InvokeStream_0000(this, request, ct).GetAsyncEnumerator(ct);", StringComparison.Ordinal);
        (generatedPipelinesSource).ShouldContain("public static global::System.Collections.Generic.IAsyncEnumerable<string> InvokeStream_0000(AppMediator mediator, global::Demo.StreamTours request,", StringComparison.Ordinal);
        (generatedPipelinesSource).ShouldContain("var pipeline0 = mediator.StreamPipeline_0000_0000;", StringComparison.Ordinal);
        (generatedPipelinesSource).ShouldContain("var handler = mediator.StreamHandler_0000;", StringComparison.Ordinal);
        (generatedPipelinesSource).ShouldNotContain("GetRequiredService", StringComparison.Ordinal);
        (generatedPipelinesSource).ShouldContain("return pipeline0.Handle(request, () => handler.Handle(request, ct), ct);", StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_appmediator_uses_Task_when_all_for_parallel_notification_strategy()
    {
        // Arrange
        var source = TestSources.ModuleHeader
            + """
            [NotificationDispatch(NotificationDispatchStrategy.Parallel)]
            public sealed record TourCreated(int Id) : INotification;

            public sealed class TourCreatedHandlerOne : INotificationHandler<TourCreated>
            {
                public ValueTask Handle(TourCreated notification, CancellationToken ct) => ValueTask.CompletedTask;
            }

            public sealed class TourCreatedHandlerTwo : INotificationHandler<TourCreated>
            {
                public ValueTask Handle(TourCreated notification, CancellationToken ct) => ValueTask.CompletedTask;
            }
            """;
        var compilation = GeneratorTestHarness.CreateCompilation(source);

        // Act
        var runResult = GeneratorTestHarness.RunGeneratorDriver(compilation);
        var generatedDispatchSource = GeneratorTestHarness.GetGeneratedSource(
            runResult,
            GeneratedHintNames.GeneratedDispatch);

        // Assert
        GeneratorSnapshotVerifier.Verify(generatedDispatchSource);
        (generatedDispatchSource).ShouldContain("var handler0 = mediator.NotificationHandler_0000_0000.Handle(notification, ct);", StringComparison.Ordinal);
        (generatedDispatchSource).ShouldContain("var handler1 = mediator.NotificationHandler_0000_0001.Handle(notification, ct);", StringComparison.Ordinal);
        (generatedDispatchSource).ShouldNotContain("GetRequiredService", StringComparison.Ordinal);
        (generatedDispatchSource).ShouldContain("await global::System.Threading.Tasks.Task.WhenAll(handler0.AsTask(), handler1.AsTask()).ConfigureAwait(false);", StringComparison.Ordinal);
    }
}
