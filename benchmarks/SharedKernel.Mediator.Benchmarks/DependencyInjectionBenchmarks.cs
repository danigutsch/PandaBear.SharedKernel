using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;

namespace SharedKernel.Mediator.Benchmarks;

/// <summary>
/// Measures generated-style versus hand-written mediator dependency-injection costs.
/// </summary>
[MemoryDiagnoser]
public class DependencyInjectionBenchmarks
{
    private ServiceProvider rootProvider = null!;
    private IServiceScope scope = null!;
    private IServiceProvider scopedProvider = null!;
    private LookupTour request = null!;

    /// <summary>
    /// Builds the reusable provider and request used by resolution benchmarks.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        rootProvider = CreateGeneratedServiceProvider();
        scope = rootProvider.CreateScope();
        scopedProvider = scope.ServiceProvider;
        request = new LookupTour("vt-42");
    }

    /// <summary>
    /// Disposes the shared provider after benchmark execution.
    /// </summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        scope.Dispose();
        rootProvider.Dispose();
    }

    /// <summary>
    /// Measures building a provider from generated-style registrations.
    /// </summary>
    /// <returns>A guard value so the build is not optimized away.</returns>
    [Benchmark(Baseline = true, Description = "Generated DI registration service-provider build time")]
    public int GeneratedServiceProviderBuildTime()
    {
        using var provider = CreateGeneratedServiceProvider();
        return provider.GetHashCode() ^ request.Code.Length;
    }

    /// <summary>
    /// Measures building a provider from equivalent hand-written registrations.
    /// </summary>
    /// <returns>A guard value so the build is not optimized away.</returns>
    [Benchmark(Description = "Hand-written DI registration service-provider build time")]
    public int HandWrittenServiceProviderBuildTime()
    {
        using var provider = CreateHandWrittenServiceProvider();
        return provider.GetHashCode() ^ request.Code.Length;
    }

    /// <summary>
    /// Measures resolving the handler through its mediator contract.
    /// </summary>
    /// <returns>The resolved handler instance.</returns>
    [Benchmark(Description = "Handler resolution cost")]
    public object HandlerResolutionCost()
    {
        return scopedProvider.GetRequiredService<IQueryHandler<LookupTour, string>>();
    }

    /// <summary>
    /// Measures the first generated-style dispatch after provider creation.
    /// </summary>
    /// <returns>The dispatched response.</returns>
    [Benchmark(Description = "First dispatch after service-provider creation")]
    public async ValueTask<string> FirstDispatchAfterServiceProviderCreation()
    {
        using var provider = CreateGeneratedServiceProvider();
        using var dispatchScope = provider.CreateScope();
        var mediator = dispatchScope.ServiceProvider.GetRequiredService<IMediator>();
        return await mediator.Send(request, CancellationToken.None).ConfigureAwait(false);
    }

    /// <summary>
    /// Measures resolving the transient concrete handler registration.
    /// </summary>
    /// <returns>The resolved handler instance.</returns>
    [Benchmark(Description = "Transient handler resolution")]
    public object TransientHandlerResolution()
    {
        return scopedProvider.GetRequiredService<LookupTourHandler>();
    }

    /// <summary>
    /// Measures resolving the scoped mediator alias.
    /// </summary>
    /// <returns>The resolved mediator instance.</returns>
    [Benchmark(Description = "Scoped mediator resolution")]
    public object ScopedMediatorResolution()
    {
        return scopedProvider.GetRequiredService<IMediator>();
    }

    private static ServiceProvider CreateGeneratedServiceProvider()
    {
        var services = new ServiceCollection();
        AddGeneratedRegistrations(services);
        return services.BuildServiceProvider();
    }

    private static ServiceProvider CreateHandWrittenServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddScoped<BenchmarkAppMediator>();
        services.AddScoped<ISender>(static sp => sp.GetRequiredService<BenchmarkAppMediator>());
        services.AddScoped<IPublisher>(static sp => sp.GetRequiredService<BenchmarkAppMediator>());
        services.AddScoped<IMediator>(static sp => sp.GetRequiredService<BenchmarkAppMediator>());
        services.AddTransient<LookupTourHandler>();
        services.AddTransient<IQueryHandler<LookupTour, string>, LookupTourHandler>();
        return services.BuildServiceProvider();
    }

    private static void AddGeneratedRegistrations(IServiceCollection services)
    {
        services.AddScoped<BenchmarkAppMediator>();
        services.AddScoped<ISender>(static sp => sp.GetRequiredService<BenchmarkAppMediator>());
        services.AddScoped<IPublisher>(static sp => sp.GetRequiredService<BenchmarkAppMediator>());
        services.AddScoped<IMediator>(static sp => sp.GetRequiredService<BenchmarkAppMediator>());
        services.AddTransient<LookupTourHandler>();
        services.AddTransient<IQueryHandler<LookupTour, string>, LookupTourHandler>();
    }

    private sealed record LookupTour(string Code) : IQuery<string>;

    private sealed class LookupTourHandler : IQueryHandler<LookupTour, string>
    {
        /// <inheritdoc />
        public ValueTask<string> Handle(LookupTour request, CancellationToken ct)
        {
            return ValueTask.FromResult(request.Code);
        }
    }

    private sealed class BenchmarkAppMediator(IServiceProvider services) : IMediator
    {
        private IServiceProvider Services { get; } = services;

        /// <inheritdoc />
        public ValueTask<string> Send(LookupTour request, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(request);

            var handler = ServiceProviderServiceExtensions.GetRequiredService<LookupTourHandler>(Services);
            return handler.Handle(request, ct);
        }

        /// <inheritdoc />
        public ValueTask<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(request);

            return request switch
            {
                LookupTour typed => Cast<string, TResponse>(Send(typed, ct)),
                _ => ThrowNoHandler(request),
            };
        }

        /// <inheritdoc />
        public ValueTask Publish<TNotification>(TNotification notification, CancellationToken ct)
            where TNotification : INotification
        {
            ArgumentNullException.ThrowIfNull(notification);
            throw new NotSupportedException("Benchmark notification dispatch is not implemented.");
        }

        private static async ValueTask<TTarget> Cast<TSource, TTarget>(ValueTask<TSource> source)
        {
            var result = await source.ConfigureAwait(false);
            if (result is TTarget typed)
            {
                return typed;
            }

            throw new InvalidOperationException(
                $"Benchmark mediator returned '{typeof(TSource).FullName}' when '{typeof(TTarget).FullName}' was expected.");
        }

        private static ValueTask<TResponse> ThrowNoHandler<TResponse>(IRequest<TResponse> request)
        {
            throw new NotSupportedException(
                $"Benchmark mediator dispatch is not available for request type '{request.GetType().FullName}'.");
        }
    }
}
