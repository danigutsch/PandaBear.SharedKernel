using System.Collections.Immutable;

namespace SharedKernel.Mediator.Analyzers.Tests;

public sealed class SharedKernelMediatorAnalyzerTests
{
    private const string InvalidHandlerSignatureId = "SKMED003";
    private const string MissingCancellationTokenId = "SKMED004";
    private const string HandlerReturnTypeMismatchId = "SKMED005";
    private const string MissingCancellationForwardingId = "SKMED006";
    private const string InvalidPipelineGenericArityId = "SKMED020";
    private const string HandlerShouldNotCallSenderId = "SKMED500";

    [Fact]
    public async Task Explicit_Interface_Handler_Reports_Invalid_Handler_Signature()
    {
        // Arrange
        const string source = """
            using SharedKernel.Mediator;

            namespace Demo;

            public sealed record LookupTour(string Code) : IQuery<string>;

            public sealed class LookupTourHandler : IQueryHandler<LookupTour, string>
            {
                ValueTask<string> IQueryHandler<LookupTour, string>.Handle(LookupTour request, CancellationToken ct)
                {
                    return ValueTask.FromResult(request.Code);
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnosticsAsync(source);

        // Assert
        Assert.Contains(diagnostics, static diagnostic => diagnostic.Id == InvalidHandlerSignatureId);
    }

    [Fact]
    public async Task Handler_Without_CancellationToken_Reports_Missing_CancellationToken()
    {
        // Arrange
        const string source = """
            using SharedKernel.Mediator;

            namespace Demo;

            public sealed record LookupTour(string Code) : IQuery<string>;

            public sealed class LookupTourHandler : IQueryHandler<LookupTour, string>
            {
                public ValueTask<string> Handle(LookupTour request)
                {
                    return ValueTask.FromResult(request.Code);
                }

                ValueTask<string> IQueryHandler<LookupTour, string>.Handle(LookupTour request, CancellationToken ct)
                {
                    return ValueTask.FromResult(request.Code);
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnosticsAsync(source);

        // Assert
        Assert.Contains(diagnostics, static diagnostic => diagnostic.Id == MissingCancellationTokenId);
    }

    [Fact]
    public async Task Handler_With_Wrong_Return_Type_Reports_Return_Type_Mismatch()
    {
        // Arrange
        const string source = """
            using SharedKernel.Mediator;

            namespace Demo;

            public sealed record LookupTour(string Code) : IQuery<string>;

            public sealed class LookupTourHandler : IQueryHandler<LookupTour, string>
            {
                public Task<string> Handle(LookupTour request, CancellationToken ct)
                {
                    return Task.FromResult(request.Code);
                }

                ValueTask<string> IQueryHandler<LookupTour, string>.Handle(LookupTour request, CancellationToken ct)
                {
                    return ValueTask.FromResult(request.Code);
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnosticsAsync(source);

        // Assert
        Assert.Contains(diagnostics, static diagnostic => diagnostic.Id == HandlerReturnTypeMismatchId);
    }

    [Fact]
    public async Task Pipeline_With_Invalid_Generic_Arity_Reports_Diagnostic()
    {
        // Arrange
        const string source = """
            using SharedKernel.Mediator;

            namespace Demo;

            public sealed record CreateTour(string Name) : ICommand<int>;

            public sealed class CreateTourHandler : ICommandHandler<CreateTour, int>
            {
                public ValueTask<int> Handle(CreateTour request, CancellationToken ct) => ValueTask.FromResult(42);
            }

            public sealed class ValidationBehavior<TRequest> : IPipelineBehavior<TRequest, int>
                where TRequest : IRequest<int>
            {
                public ValueTask<int> Handle(TRequest request, RequestHandlerContinuation<int> next, CancellationToken ct) => next();
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnosticsAsync(source);

        // Assert
        Assert.Contains(diagnostics, static diagnostic => diagnostic.Id == InvalidPipelineGenericArityId);
    }

    [Fact]
    public async Task Mediator_Call_Without_Available_CancellationToken_Forwarding_Reports_Diagnostic()
    {
        // Arrange
        const string source = """
            using SharedKernel.Mediator;

            namespace Demo;

            public sealed record LookupTour(string Code) : IQuery<string>;
            public sealed record SearchTour(string Code) : IQuery<string>;

            public sealed class LookupTourHandler(ISender sender) : IQueryHandler<LookupTour, string>
            {
                public async ValueTask<string> Handle(LookupTour request, CancellationToken ct)
                {
                    return await sender.Send(new SearchTour(request.Code), CancellationToken.None);
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnosticsAsync(source);

        // Assert
        Assert.Contains(diagnostics, static diagnostic => diagnostic.Id == MissingCancellationForwardingId);
    }

    [Fact]
    public async Task Cancellation_Analysis_Can_Be_Disabled_From_Editorconfig()
    {
        // Arrange
        const string source = """
            using SharedKernel.Mediator;

            namespace Demo;

            public sealed record LookupTour(string Code) : IQuery<string>;
            public sealed record SearchTour(string Code) : IQuery<string>;

            public sealed class LookupTourHandler(ISender sender) : IQueryHandler<LookupTour, string>
            {
                public async ValueTask<string> Handle(LookupTour request, CancellationToken ct)
                {
                    return await sender.Send(new SearchTour(request.Code), CancellationToken.None);
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnosticsAsync(
            source,
            ImmutableDictionary<string, string>.Empty.Add("sharedkernel_mediator_enable_cancellation_analysis", "false"));

        // Assert
        Assert.DoesNotContain(diagnostics, static diagnostic => diagnostic.Id == MissingCancellationForwardingId);
    }

    [Fact]
    public async Task Strict_Mode_Reports_Handler_To_Handler_Send_Calls()
    {
        // Arrange
        const string source = """
            using SharedKernel.Mediator;

            namespace Demo;

            public sealed record LookupTour(string Code) : IQuery<string>;
            public sealed record SearchTour(string Code) : IQuery<string>;

            public sealed class LookupTourHandler(ISender sender) : IQueryHandler<LookupTour, string>
            {
                public async ValueTask<string> Handle(LookupTour request, CancellationToken ct)
                {
                    return await sender.Send(new SearchTour(request.Code), ct);
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnosticsAsync(source);

        // Assert
        Assert.Contains(diagnostics, static diagnostic => diagnostic.Id == HandlerShouldNotCallSenderId);
    }

    [Fact]
    public async Task Strict_Mode_Can_Allow_Handler_To_Handler_Send_Calls()
    {
        // Arrange
        const string source = """
            using SharedKernel.Mediator;

            namespace Demo;

            public sealed record LookupTour(string Code) : IQuery<string>;
            public sealed record SearchTour(string Code) : IQuery<string>;

            public sealed class LookupTourHandler(ISender sender) : IQueryHandler<LookupTour, string>
            {
                public async ValueTask<string> Handle(LookupTour request, CancellationToken ct)
                {
                    return await sender.Send(new SearchTour(request.Code), ct);
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnosticsAsync(
            source,
            ImmutableDictionary<string, string>.Empty.Add("sharedkernel_mediator_allow_handler_to_handler_calls", "true"));

        // Assert
        Assert.DoesNotContain(diagnostics, static diagnostic => diagnostic.Id == HandlerShouldNotCallSenderId);
    }

    [Fact]
    public void Editorconfig_Options_Use_Documented_Defaults()
    {
        // Arrange
        var provider = new TestAnalyzerConfigOptionsProvider(ImmutableDictionary<string, string>.Empty);

        // Act
        var options = SharedKernel.Mediator.Analyzers.MediatorAnalyzerConfigOptions.Parse(provider);

        // Assert
        Assert.True(options.CqrsStrict);
        Assert.False(options.AllowHandlerToHandlerCalls);
        Assert.True(options.EnableCancellationAnalysis);
    }

    private sealed class TestAnalyzerConfigOptionsProvider(ImmutableDictionary<string, string>? globalOptions)
        : Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptionsProvider
    {
        private readonly Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptions global = new TestAnalyzerConfigOptions(globalOptions);

        public override Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptions GlobalOptions => global;

        public override Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptions GetOptions(Microsoft.CodeAnalysis.SyntaxTree tree)
        {
            return TestAnalyzerConfigOptions.Empty;
        }

        public override Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptions GetOptions(Microsoft.CodeAnalysis.AdditionalText textFile)
        {
            return TestAnalyzerConfigOptions.Empty;
        }
    }

    private sealed class TestAnalyzerConfigOptions(ImmutableDictionary<string, string>? values)
        : Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptions
    {
        public static readonly TestAnalyzerConfigOptions Empty = new(null);

        private readonly ImmutableDictionary<string, string> options = values ?? ImmutableDictionary<string, string>.Empty;

        public override bool TryGetValue(string key, out string value)
        {
            return options.TryGetValue(key, out value!);
        }
    }
}
