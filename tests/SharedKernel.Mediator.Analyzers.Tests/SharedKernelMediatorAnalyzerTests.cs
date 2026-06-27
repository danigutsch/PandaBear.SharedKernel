using System.Collections.Immutable;
using SharedKernel.Testing.Roslyn;

namespace SharedKernel.Mediator.Analyzers.Tests;

public sealed class SharedKernelMediatorAnalyzerTests
{
    [Fact]
    public async Task Explicit_interface_handler_reports_invalid_handler_signature()
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
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        Assert.Contains(diagnostics, static diagnostic => diagnostic.Id == MediatorDiagnosticIds.InvalidHandlerSignature);
    }

    [Fact]
    public async Task Handler_without_cancellationToken_reports_missing_cancellationToken()
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
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        Assert.Contains(diagnostics, static diagnostic => diagnostic.Id == MediatorDiagnosticIds.MissingCancellationToken);
    }

    [Fact]
    public async Task Handler_with_wrong_return_type_reports_return_type_mismatch()
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
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        Assert.Contains(diagnostics, static diagnostic => diagnostic.Id == MediatorDiagnosticIds.HandlerReturnTypeMismatch);
    }

    [Fact]
    public async Task Pipeline_with_invalid_generic_arity_reports_diagnostic()
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
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        Assert.Contains(diagnostics, static diagnostic => diagnostic.Id == MediatorDiagnosticIds.InvalidPipelineGenericArity);
    }

    [Fact]
    public async Task Mediator_call_without_available_cancellationToken_forwarding_reports_diagnostic()
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
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        Assert.Contains(diagnostics, static diagnostic => diagnostic.Id == MediatorDiagnosticIds.MissingCancellationForwarding);
    }

    [Fact]
    public async Task Async_stream_handler_without_enumeratorCancellation_reports_diagnostic()
    {
        // Arrange
        const string source = """
            using SharedKernel.Mediator;

            namespace Demo;

            public sealed record StreamTours(int Count) : IStreamRequest<string>;

            public sealed class StreamToursHandler : IStreamRequestHandler<StreamTours, string>
            {
                public async IAsyncEnumerable<string> Handle(StreamTours request, CancellationToken ct)
                {
                    await Task.Yield();
                    yield return request.Count.ToString();
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        Assert.Contains(diagnostics, static diagnostic => diagnostic.Id == MediatorDiagnosticIds.MissingEnumeratorCancellation);
    }

    [Fact]
    public async Task Async_stream_pipeline_without_enumeratorCancellation_reports_diagnostic()
    {
        // Arrange
        const string source = """
            using SharedKernel.Mediator;

            namespace Demo;

            public sealed record StreamTours(int Count) : IStreamRequest<string>;

            public sealed class StreamValidationBehavior : IStreamPipelineBehavior<StreamTours, string>
            {
                public async IAsyncEnumerable<string> Handle(StreamTours request, StreamHandlerContinuation<string> next, CancellationToken ct)
                {
                    await Task.Yield();
                    await foreach (var item in next())
                    {
                        yield return item;
                    }
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        Assert.Contains(diagnostics, static diagnostic => diagnostic.Id == MediatorDiagnosticIds.MissingEnumeratorCancellation);
    }

    [Fact]
    public async Task Async_stream_handler_with_enumeratorCancellation_does_not_report_diagnostic()
    {
        // Arrange
        const string source = """
            using SharedKernel.Mediator;

            namespace Demo;

            public sealed record StreamTours(int Count) : IStreamRequest<string>;

            public sealed class StreamToursHandler : IStreamRequestHandler<StreamTours, string>
            {
                public async IAsyncEnumerable<string> Handle(
                    StreamTours request,
                    [global::System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
                {
                    await Task.Yield();
                    yield return request.Count.ToString();
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        Assert.DoesNotContain(diagnostics, static diagnostic => diagnostic.Id == MediatorDiagnosticIds.MissingEnumeratorCancellation);
    }

    [Fact]
    public async Task Non_iterator_stream_handler_reports_sKMED008_diagnostic()
    {
        // Arrange
        const string source = """
            using SharedKernel.Mediator;

            namespace Demo;

            public sealed record StreamTours(int Count) : IStreamRequest<string>;

            public sealed class StreamToursHandler : IStreamRequestHandler<StreamTours, string>
            {
                public IAsyncEnumerable<string> Handle(StreamTours request, CancellationToken ct)
                    => GetItems();

                private async IAsyncEnumerable<string> GetItems()
                {
                    yield return "item";
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        Assert.DoesNotContain(diagnostics, static diagnostic => diagnostic.Id == MediatorDiagnosticIds.MissingEnumeratorCancellation);
        Assert.Contains(diagnostics, static diagnostic => diagnostic.Id == MediatorDiagnosticIds.NonIteratorStreamHandlerHasCancellationToken);
    }

    [Fact]
    public async Task Non_iterator_stream_pipeline_does_not_report_enumeratorCancellation_diagnostic()
    {
        // Arrange
        const string source = """
            using SharedKernel.Mediator;

            namespace Demo;

            public sealed record StreamTours(int Count) : IStreamRequest<string>;

            public sealed class StreamValidationBehavior : IStreamPipelineBehavior<StreamTours, string>
            {
                public IAsyncEnumerable<string> Handle(StreamTours request, StreamHandlerContinuation<string> next, CancellationToken ct)
                {
                    return next();
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        Assert.DoesNotContain(diagnostics, static diagnostic => diagnostic.Id == MediatorDiagnosticIds.MissingEnumeratorCancellation);
        Assert.Contains(diagnostics, static diagnostic => diagnostic.Id == MediatorDiagnosticIds.NonIteratorStreamHandlerHasCancellationToken);
    }

    [Fact]
    public async Task Cancellation_analysis_can_be_disabled_from_editorconfig()
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
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(
            source,
            ImmutableDictionary<string, string>.Empty.Add("sharedkernel_mediator_enable_cancellation_analysis", "false"));

        // Assert
        Assert.DoesNotContain(diagnostics, static diagnostic => diagnostic.Id == MediatorDiagnosticIds.MissingCancellationForwarding);
    }

    [Fact]
    public async Task Strict_mode_reports_handler_to_handler_send_calls()
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
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        Assert.Contains(diagnostics, static diagnostic => diagnostic.Id == MediatorDiagnosticIds.HandlerShouldNotCallSender);
    }

    [Fact]
    public async Task Strict_mode_can_allow_handler_to_handler_send_calls()
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
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(
            source,
            ImmutableDictionary<string, string>.Empty.Add("sharedkernel_mediator_allow_handler_to_handler_calls", "true"));

        // Assert
        Assert.DoesNotContain(diagnostics, static diagnostic => diagnostic.Id == MediatorDiagnosticIds.HandlerShouldNotCallSender);
    }

    [Fact]
    public void Editorconfig_options_use_documented_defaults()
    {
        // Arrange
        var provider = new TestAnalyzerConfigOptionsProvider(ImmutableDictionary<string, string>.Empty);

        // Act
        var options = MediatorAnalyzerConfigOptions.Parse(provider);

        // Assert
        Assert.True(options.CqrsStrict);
        Assert.False(options.AllowHandlerToHandlerCalls);
        Assert.True(options.EnableCancellationAnalysis);
    }

    [Fact]
    public async Task Explicit_interface_stream_handler_reports_invalid_handler_signature()
    {
        // Arrange
        const string source = """
            using SharedKernel.Mediator;
            using System.Collections.Generic;
            using System.Runtime.CompilerServices;

            namespace Demo;

            public sealed record StreamTours(int Count) : IStreamRequest<string>;

            public sealed class StreamToursHandler : IStreamRequestHandler<StreamTours, string>
            {
                async IAsyncEnumerable<string> IStreamRequestHandler<StreamTours, string>.Handle(
                    StreamTours request,
                    [EnumeratorCancellation] CancellationToken ct)
                {
                    await Task.Yield();
                    yield return request.Count.ToString();
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        Assert.Contains(diagnostics, static diagnostic => diagnostic.Id == MediatorDiagnosticIds.InvalidHandlerSignature);
    }

    [Fact]
    public async Task Stream_handler_without_cancellationToken_reports_missing_cancellationToken()
    {
        // Arrange
        const string source = """
            using SharedKernel.Mediator;
            using System.Collections.Generic;
            using System.Runtime.CompilerServices;

            namespace Demo;

            public sealed record StreamTours(int Count) : IStreamRequest<string>;

            public sealed class StreamToursHandler : IStreamRequestHandler<StreamTours, string>
            {
                public async IAsyncEnumerable<string> Handle(StreamTours request)
                {
                    await Task.Yield();
                    yield return request.Count.ToString();
                }

                async IAsyncEnumerable<string> IStreamRequestHandler<StreamTours, string>.Handle(
                    StreamTours request,
                    [EnumeratorCancellation] CancellationToken ct)
                {
                    await Task.Yield();
                    yield return request.Count.ToString();
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        Assert.Contains(diagnostics, static diagnostic => diagnostic.Id == MediatorDiagnosticIds.MissingCancellationToken);
    }

    [Fact]
    public async Task Stream_handler_with_wrong_return_type_reports_return_type_mismatch()
    {
        // Arrange
        const string source = """
            using SharedKernel.Mediator;
            using System.Collections.Generic;
            using System.Runtime.CompilerServices;

            namespace Demo;

            public sealed record StreamTours(int Count) : IStreamRequest<string>;

            public sealed class StreamToursHandler : IStreamRequestHandler<StreamTours, string>
            {
                public IEnumerable<string> Handle(StreamTours request, CancellationToken ct)
                {
                    yield return request.Count.ToString();
                }

                async IAsyncEnumerable<string> IStreamRequestHandler<StreamTours, string>.Handle(
                    StreamTours request,
                    [EnumeratorCancellation] CancellationToken ct)
                {
                    await Task.Yield();
                    yield return request.Count.ToString();
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        Assert.Contains(diagnostics, static diagnostic => diagnostic.Id == MediatorDiagnosticIds.HandlerReturnTypeMismatch);
    }

    [Fact]
    public async Task Stream_handler_calling_sender_reports_handler_should_not_call_sender()
    {
        // Arrange
        const string source = """
            using SharedKernel.Mediator;
            using System.Collections.Generic;
            using System.Runtime.CompilerServices;

            namespace Demo;

            public sealed record StreamTours(int Count) : IStreamRequest<string>;
            public sealed record LookupTour(string Code) : IQuery<string>;

            public sealed class StreamToursHandler(ISender sender) : IStreamRequestHandler<StreamTours, string>
            {
                public async IAsyncEnumerable<string> Handle(
                    StreamTours request,
                    [EnumeratorCancellation] CancellationToken ct)
                {
                    var result = await sender.Send(new LookupTour("test"), ct);
                    yield return result;
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        Assert.Contains(diagnostics, static diagnostic => diagnostic.Id == MediatorDiagnosticIds.HandlerShouldNotCallSender);
    }

    [Fact]
    public async Task Non_iterator_stream_handler_with_cancellationToken_reports_nonIterator_diagnostic()
    {
        // Arrange
        const string source = """
            using SharedKernel.Mediator;
            using System.Collections.Generic;
            using System.Threading;

            namespace Demo;

            public sealed record StreamTours(int Count) : IStreamRequest<string>;

            public sealed class StreamToursHandler : IStreamRequestHandler<StreamTours, string>
            {
                public IAsyncEnumerable<string> Handle(StreamTours request, CancellationToken ct)
                    => GetItems(); // non-iterator: ct has no effect
            
                private static async IAsyncEnumerable<string> GetItems()
                {
                    yield return "item";
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        Assert.Contains(diagnostics, static d => d.Id == MediatorDiagnosticIds.NonIteratorStreamHandlerHasCancellationToken);
        Assert.DoesNotContain(diagnostics, static d => d.Id == MediatorDiagnosticIds.MissingEnumeratorCancellation);
    }

    [Fact]
    public async Task Non_iterator_stream_pipeline_with_cancellationToken_reports_nonIterator_diagnostic()
    {
        // Arrange
        const string source = """
            using SharedKernel.Mediator;
            using System.Collections.Generic;
            using System.Threading;

            namespace Demo;

            public sealed record StreamTours(int Count) : IStreamRequest<string>;

            public sealed class StreamValidationBehavior : IStreamPipelineBehavior<StreamTours, string>
            {
                public IAsyncEnumerable<string> Handle(
                    StreamTours request,
                    StreamHandlerContinuation<string> next,
                    CancellationToken ct)
                    => next(); // non-iterator: ct has no effect
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        Assert.Contains(diagnostics, static d => d.Id == MediatorDiagnosticIds.NonIteratorStreamHandlerHasCancellationToken);
        Assert.DoesNotContain(diagnostics, static d => d.Id == MediatorDiagnosticIds.MissingEnumeratorCancellation);
    }

    [Fact]
    public async Task Iterator_stream_handler_does_not_report_nonIterator_diagnostic()
    {
        // Arrange
        const string source = """
            using SharedKernel.Mediator;
            using System.Collections.Generic;
            using System.Threading;

            namespace Demo;

            public sealed record StreamTours(int Count) : IStreamRequest<string>;

            public sealed class StreamToursHandler : IStreamRequestHandler<StreamTours, string>
            {
                public async IAsyncEnumerable<string> Handle(StreamTours request, CancellationToken ct)
                {
                    yield return "item";
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        Assert.Contains(diagnostics, static d => d.Id == MediatorDiagnosticIds.MissingEnumeratorCancellation);
        Assert.DoesNotContain(diagnostics, static d => d.Id == MediatorDiagnosticIds.NonIteratorStreamHandlerHasCancellationToken);
    }

    [Fact]
    public async Task Non_iterator_stream_handler_without_cancellationToken_does_not_report_nonIterator_diagnostic()
    {
        // Arrange — [EnumeratorCancellation] present suppresses both SKMED007 and SKMED008
        const string source = """
            using SharedKernel.Mediator;
            using System.Collections.Generic;
            using System.Runtime.CompilerServices;
            using System.Threading;

            namespace Demo;

            public sealed record StreamTours(int Count) : IStreamRequest<string>;

            public sealed class StreamToursHandler : IStreamRequestHandler<StreamTours, string>
            {
                public IAsyncEnumerable<string> Handle(
                    StreamTours request,
                    [EnumeratorCancellation] CancellationToken ct)
                    => GetItems(ct);

                private static async IAsyncEnumerable<string> GetItems(
                    [EnumeratorCancellation] CancellationToken ct)
                {
                    yield return "item";
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        Assert.DoesNotContain(diagnostics, static d => d.Id == MediatorDiagnosticIds.NonIteratorStreamHandlerHasCancellationToken);
        Assert.DoesNotContain(diagnostics, static d => d.Id == MediatorDiagnosticIds.MissingEnumeratorCancellation);
    }

    [Fact]
    public async Task Non_iterator_stream_handler_that_forwards_cancellationToken_does_not_report_nonIterator_diagnostic()
    {
        // Arrange
        const string source = """
            using SharedKernel.Mediator;
            using System.Collections.Generic;
            using System.Runtime.CompilerServices;
            using System.Threading;

            namespace Demo;

            public sealed record StreamTours(int Count) : IStreamRequest<string>;

            public sealed class StreamToursHandler : IStreamRequestHandler<StreamTours, string>
            {
                public IAsyncEnumerable<string> Handle(StreamTours request, CancellationToken ct)
                {
                    ArgumentNullException.ThrowIfNull(request);
                    return GetItems(request.Count, ct);
                }

                private static async IAsyncEnumerable<string> GetItems(
                    int count,
                    [EnumeratorCancellation] CancellationToken ct)
                {
                    for (var index = 0; index < count; index++)
                    {
                        ct.ThrowIfCancellationRequested();
                        yield return index.ToString();
                    }
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        Assert.DoesNotContain(diagnostics, static d => d.Id == MediatorDiagnosticIds.NonIteratorStreamHandlerHasCancellationToken);
        Assert.DoesNotContain(diagnostics, static d => d.Id == MediatorDiagnosticIds.MissingEnumeratorCancellation);
    }
}
