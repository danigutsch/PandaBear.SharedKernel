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

            public sealed record MissingTour(int Id) : IQuery<string>;
            """;
        var compilation = GeneratorTestHarness.CreateCompilation(source);

        // Act
        var generatedSource = GeneratorTestHarness.RunGenerator(
            compilation,
            "SharedKernel.Mediator.Generated.AppMediator.g.cs");

        // Assert
        GeneratorSnapshotVerifier.Verify(generatedSource);
        Assert.Contains("public sealed partial class AppMediator : IMediator", generatedSource, StringComparison.Ordinal);
        Assert.Contains("internal global::System.IServiceProvider Services { get; }", generatedSource, StringComparison.Ordinal);
        Assert.Contains("public global::System.Threading.Tasks.ValueTask<string> Send(global::Demo.LookupTour request,", generatedSource, StringComparison.Ordinal);
        Assert.Contains("public global::System.Threading.Tasks.ValueTask<int> Send(global::Demo.CreateTour request,", generatedSource, StringComparison.Ordinal);
        Assert.Contains("public global::System.Threading.Tasks.ValueTask<global::SharedKernel.Mediator.Unit> Send(global::Demo.DeleteTour request,", generatedSource, StringComparison.Ordinal);
        Assert.Contains("public global::System.Threading.Tasks.ValueTask<string> Send(global::Demo.GetTourById request,", generatedSource, StringComparison.Ordinal);
        Assert.Contains("return GeneratedDispatch.Send<TResponse>(this, request, ct);", generatedSource, StringComparison.Ordinal);
        Assert.Contains("public global::System.Threading.Tasks.ValueTask<object?> SendObject(", generatedSource, StringComparison.Ordinal);
        Assert.Contains("global::Demo.LookupTour typed => Cast<string, TResponse>(mediator.Send(typed, ct)),", generatedSource, StringComparison.Ordinal);
        Assert.Contains("global::Demo.CreateTour typed => Cast<int, TResponse>(mediator.Send(typed, ct)),", generatedSource, StringComparison.Ordinal);
        Assert.Contains("global::Demo.DeleteTour typed => Cast<global::SharedKernel.Mediator.Unit, TResponse>(mediator.Send(typed, ct)),", generatedSource, StringComparison.Ordinal);
        Assert.Contains("global::Demo.GetTourById typed => Cast<string, TResponse>(mediator.Send(typed, ct)),", generatedSource, StringComparison.Ordinal);
        Assert.Contains("global::Demo.LookupTour typed => Box<string>(mediator.Send(typed, ct)),", generatedSource, StringComparison.Ordinal);
        Assert.Contains("return GeneratedDispatch.ThrowNoHandler<string>(request);", generatedSource, StringComparison.Ordinal);
        Assert.Contains("public static global::System.Threading.Tasks.ValueTask<TResponse> ThrowNoHandler<TResponse>(", generatedSource, StringComparison.Ordinal);
        Assert.Contains("public static global::System.Threading.Tasks.ValueTask<object?> ThrowUnknownRequestObject(", generatedSource, StringComparison.Ordinal);
        Assert.Contains("public static TTarget ThrowInvalidResponseCast<TSource, TTarget>()", generatedSource, StringComparison.Ordinal);
        Assert.Contains("Generated notification dispatch is not available yet.", generatedSource, StringComparison.Ordinal);
    }
}
