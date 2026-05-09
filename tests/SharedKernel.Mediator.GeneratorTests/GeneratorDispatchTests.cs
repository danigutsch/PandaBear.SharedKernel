namespace SharedKernel.Mediator.GeneratorTests;

[Trait(TestTraits.CapabilityName, TestTraits.DispatchCapability)]
public sealed class GeneratorDispatchTests
{
    [Fact]
    public void Generate_AppMediator_Shell()
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

        // Assert
        GeneratorSnapshotVerifier.Verify(generatedSource);
        GeneratorSnapshotVerifier.Verify(generatedDispatchSource, testName: "Generate_GeneratedDispatch_Shell");
        Assert.Contains("public sealed partial class AppMediator : IMediator", generatedSource, StringComparison.Ordinal);
        Assert.Contains("internal global::System.IServiceProvider Services { get; }", generatedSource, StringComparison.Ordinal);
        Assert.Contains("public global::System.Threading.Tasks.ValueTask<string> Send(global::Demo.LookupTour request,", generatedSource, StringComparison.Ordinal);
        Assert.Contains("public global::System.Threading.Tasks.ValueTask<int> Send(global::Demo.CreateTour request,", generatedSource, StringComparison.Ordinal);
        Assert.Contains("public global::System.Threading.Tasks.ValueTask<global::SharedKernel.Mediator.Unit> Send(global::Demo.DeleteTour request,", generatedSource, StringComparison.Ordinal);
        Assert.Contains("public global::System.Threading.Tasks.ValueTask<string> Send(global::Demo.GetTourById request,", generatedSource, StringComparison.Ordinal);
        Assert.Contains("public global::System.Collections.Generic.IAsyncEnumerable<string> Send(global::Demo.StreamTours request,", generatedSource, StringComparison.Ordinal);
        Assert.Contains("public global::System.Collections.Generic.IAsyncEnumerable<TResponse> Send<TResponse>(", generatedSource, StringComparison.Ordinal);
        Assert.Contains("return GeneratedDispatch.Send<TResponse>(this, request, ct);", generatedSource, StringComparison.Ordinal);
        Assert.Contains("return GeneratedDispatch.Send<TResponse>(this, request, ct);", generatedSource, StringComparison.Ordinal);
        Assert.Contains("return GeneratedDispatch.Publish(this, notification, ct);", generatedSource, StringComparison.Ordinal);
        Assert.Contains("public global::System.Threading.Tasks.ValueTask<object?> SendObject(", generatedSource, StringComparison.Ordinal);
        Assert.Contains("return GeneratedDispatch.ThrowNoHandler<string>(request);", generatedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("internal static class GeneratedDispatch", generatedSource, StringComparison.Ordinal);
        Assert.Contains("internal static class GeneratedDispatch", generatedDispatchSource, StringComparison.Ordinal);
        Assert.Contains("internal static class GeneratedPipelines", generatedPipelinesSource, StringComparison.Ordinal);
        Assert.Contains("global::Demo.LookupTour typed => Cast<string, TResponse>(mediator.Send(typed, ct)),", generatedDispatchSource, StringComparison.Ordinal);
        Assert.Contains("global::Demo.CreateTour typed => Cast<int, TResponse>(mediator.Send(typed, ct)),", generatedDispatchSource, StringComparison.Ordinal);
        Assert.Contains("global::Demo.DeleteTour typed => Cast<global::SharedKernel.Mediator.Unit, TResponse>(mediator.Send(typed, ct)),", generatedDispatchSource, StringComparison.Ordinal);
        Assert.Contains("global::Demo.GetTourById typed => Cast<string, TResponse>(mediator.Send(typed, ct)),", generatedDispatchSource, StringComparison.Ordinal);
        Assert.Contains("global::Demo.LookupTour typed => Box<string>(mediator.Send(typed, ct)),", generatedDispatchSource, StringComparison.Ordinal);
        Assert.Contains("public static global::System.Collections.Generic.IAsyncEnumerable<TResponse> Send<TResponse>(", generatedDispatchSource, StringComparison.Ordinal);
        Assert.Contains("global::Demo.StreamTours typed => CastStream<string, TResponse>(mediator.Send(typed, ct), ct),", generatedDispatchSource, StringComparison.Ordinal);
        Assert.Contains("public static global::System.Threading.Tasks.ValueTask Publish<TNotification>(", generatedDispatchSource, StringComparison.Ordinal);
        Assert.Contains("global::Demo.TourCreated typed => Publish_0000(mediator.Services, typed, ct),", generatedDispatchSource, StringComparison.Ordinal);
        Assert.Contains("return global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<global::Demo.TourCreatedHandler>(services).Handle(notification, ct);", generatedDispatchSource, StringComparison.Ordinal);
        Assert.Contains("public static global::System.Threading.Tasks.ValueTask<TResponse> ThrowNoHandler<TResponse>(", generatedDispatchSource, StringComparison.Ordinal);
        Assert.Contains("public static global::System.Collections.Generic.IAsyncEnumerable<TResponse> ThrowNoStreamHandler<TResponse>(", generatedDispatchSource, StringComparison.Ordinal);
        Assert.Contains("public static global::System.Threading.Tasks.ValueTask<object?> ThrowUnknownRequestObject(", generatedDispatchSource, StringComparison.Ordinal);
        Assert.Contains("public static TTarget ThrowInvalidResponseCast<TSource, TTarget>()", generatedDispatchSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_AppMediator_Uses_Generated_Pipeline_Helper_When_Pipelines_Exist()
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
        Assert.Contains("return GeneratedPipelines.Invoke_0000(this, request, ct);", generatedMediatorSource, StringComparison.Ordinal);
        Assert.Contains("public static global::System.Threading.Tasks.ValueTask<int> Invoke_0000(AppMediator mediator, global::Demo.CreateTour request,", generatedPipelinesSource, StringComparison.Ordinal);
        Assert.Contains("var pipeline0 = global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<global::Demo.ValidationBehavior>(mediator.Services);", generatedPipelinesSource, StringComparison.Ordinal);
        Assert.Contains("return pipeline0.Handle(request, () => handler.Handle(request, ct), ct);", generatedPipelinesSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_AppMediator_Uses_Generated_Stream_Pipeline_Helper_When_Stream_Pipelines_Exist()
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
        Assert.Contains("return GeneratedPipelines.InvokeStream_0000(this, request, ct);", generatedMediatorSource, StringComparison.Ordinal);
        Assert.Contains("public static global::System.Collections.Generic.IAsyncEnumerable<string> InvokeStream_0000(AppMediator mediator, global::Demo.StreamTours request,", generatedPipelinesSource, StringComparison.Ordinal);
        Assert.Contains("var pipeline0 = global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<global::Demo.ValidationBehavior>(mediator.Services);", generatedPipelinesSource, StringComparison.Ordinal);
        Assert.Contains("return pipeline0.Handle(request, () => handler.Handle(request, ct), ct);", generatedPipelinesSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_AppMediator_Uses_Task_When_All_For_Parallel_Notification_Strategy()
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
        Assert.Contains("var handler0 = global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<global::Demo.TourCreatedHandlerOne>(services).Handle(notification, ct);", generatedDispatchSource, StringComparison.Ordinal);
        Assert.Contains("var handler1 = global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<global::Demo.TourCreatedHandlerTwo>(services).Handle(notification, ct);", generatedDispatchSource, StringComparison.Ordinal);
        Assert.Contains("await global::System.Threading.Tasks.Task.WhenAll(handler0.AsTask(), handler1.AsTask()).ConfigureAwait(false);", generatedDispatchSource, StringComparison.Ordinal);
    }
}
