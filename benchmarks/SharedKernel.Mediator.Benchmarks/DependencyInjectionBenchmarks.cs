using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;

namespace SharedKernel.Mediator.Benchmarks;

/// <summary>
/// Measures generated-style versus hand-written mediator dependency-injection costs.
/// </summary>
[Config(typeof(BenchmarkOutputConfig))]
[MemoryDiagnoser]
public class DependencyInjectionBenchmarks
{
    private const string TransientLifetime = "Transient";
    private const string ScopedLifetime = "Scoped";
    private const string SingletonStatelessLifetime = "SingletonStateless";

    private ServiceProvider rootProvider = null!;
    private IServiceScope scope = null!;
    private IServiceProvider scopedProvider = null!;
    private Type configuredHandlerType = null!;
    private LookupTour request = null!;

    /// <summary>
    /// Builds the reusable provider and request used by resolution benchmarks.
    /// </summary>
    [Params(TransientLifetime, ScopedLifetime, SingletonStatelessLifetime)]
    public string HandlerLifetime { get; set; } = TransientLifetime;

    /// <summary>
    /// Gets or sets the configured number of handler constructor dependencies.
    /// </summary>
    [Params(0, 1, 5)]
    public int HandlerDependencyCount { get; set; }

    /// <summary>
    /// Builds the reusable provider and request used by resolution benchmarks.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        configuredHandlerType = GetConfiguredHandlerType(HandlerDependencyCount);
        rootProvider = CreateGeneratedServiceProvider(HandlerLifetime, configuredHandlerType);
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
        using var provider = CreateGeneratedServiceProvider(HandlerLifetime, configuredHandlerType);
        return provider.GetHashCode() ^ request.Code.Length;
    }

    /// <summary>
    /// Measures building a provider from equivalent hand-written registrations.
    /// </summary>
    /// <returns>A guard value so the build is not optimized away.</returns>
    [Benchmark(Description = "Hand-written DI registration service-provider build time")]
    public int HandWrittenServiceProviderBuildTime()
    {
        using var provider = CreateHandWrittenServiceProvider(HandlerLifetime, configuredHandlerType);
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
        using var provider = CreateGeneratedServiceProvider(HandlerLifetime, configuredHandlerType);
        using var dispatchScope = provider.CreateScope();
        var mediator = dispatchScope.ServiceProvider.GetRequiredService<IMediator>();
        return await mediator.Send(request, CancellationToken.None).ConfigureAwait(false);
    }

    /// <summary>
    /// Measures resolving the configured concrete handler registration.
    /// </summary>
    /// <returns>The resolved handler instance.</returns>
    [Benchmark(Description = "Handler lifetime candidate resolution")]
    public object ConfiguredHandlerLifetimeResolution()
    {
        return scopedProvider.GetRequiredService(configuredHandlerType);
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

    private static ServiceProvider CreateGeneratedServiceProvider(string handlerLifetime, Type handlerType)
    {
        var services = new ServiceCollection();
        AddGeneratedRegistrations(services, handlerLifetime, handlerType);
        return services.BuildServiceProvider();
    }

    private static ServiceProvider CreateHandWrittenServiceProvider(string handlerLifetime, Type handlerType)
    {
        var services = new ServiceCollection();
        services.AddScoped<BenchmarkAppMediator>();
        services.AddScoped<ISender>(static sp => sp.GetRequiredService<BenchmarkAppMediator>());
        services.AddScoped<IPublisher>(static sp => sp.GetRequiredService<BenchmarkAppMediator>());
        services.AddScoped<IMediator>(static sp => sp.GetRequiredService<BenchmarkAppMediator>());
        AddHandlerDependencies(services);
        AddHandlerRegistrations(services, handlerLifetime, handlerType);
        return services.BuildServiceProvider();
    }

    private static void AddGeneratedRegistrations(IServiceCollection services, string handlerLifetime, Type handlerType)
    {
        services.AddScoped<BenchmarkAppMediator>();
        services.AddScoped<ISender>(static sp => sp.GetRequiredService<BenchmarkAppMediator>());
        services.AddScoped<IPublisher>(static sp => sp.GetRequiredService<BenchmarkAppMediator>());
        services.AddScoped<IMediator>(static sp => sp.GetRequiredService<BenchmarkAppMediator>());
        AddHandlerDependencies(services);
        AddHandlerRegistrations(services, handlerLifetime, handlerType);
    }

    private static void AddHandlerDependencies(IServiceCollection services)
    {
        services.AddSingleton(new HandlerDependencyOne(1));
        services.AddSingleton(new HandlerDependencyTwo(2));
        services.AddSingleton(new HandlerDependencyThree(3));
        services.AddSingleton(new HandlerDependencyFour(4));
        services.AddSingleton(new HandlerDependencyFive(5));
    }

    private static void AddHandlerRegistrations(IServiceCollection services, string handlerLifetime, Type handlerType)
    {
        var lifetime = GetServiceLifetime(handlerLifetime);
        services.Add(new ServiceDescriptor(handlerType, handlerType, lifetime));
        services.Add(new ServiceDescriptor(typeof(IQueryHandler<LookupTour, string>), handlerType, lifetime));
    }

    private static ServiceLifetime GetServiceLifetime(string handlerLifetime)
    {
        return handlerLifetime switch
        {
            TransientLifetime => ServiceLifetime.Transient,
            ScopedLifetime => ServiceLifetime.Scoped,
            SingletonStatelessLifetime => ServiceLifetime.Singleton,
            _ => throw new InvalidOperationException($"Unsupported handler lifetime '{handlerLifetime}'."),
        };
    }

    private static Type GetConfiguredHandlerType(int handlerDependencyCount)
    {
        return handlerDependencyCount switch
        {
            0 => typeof(LookupTourHandler0),
            1 => typeof(LookupTourHandler1),
            5 => typeof(LookupTourHandler5),
            _ => throw new InvalidOperationException(
                $"Unsupported handler dependency count '{handlerDependencyCount}'."),
        };
    }

    private sealed record LookupTour(string Code) : IQuery<string>;

    private sealed class LookupTourHandler0 : IQueryHandler<LookupTour, string>
    {
        /// <inheritdoc />
        public ValueTask<string> Handle(LookupTour request, CancellationToken ct)
        {
            return ValueTask.FromResult(request.Code);
        }
    }

    private sealed class LookupTourHandler1(HandlerDependencyOne dependencyOne) : IQueryHandler<LookupTour, string>
    {
        private HandlerDependencyOne DependencyOne { get; } = dependencyOne;

        /// <inheritdoc />
        public ValueTask<string> Handle(LookupTour request, CancellationToken ct)
        {
            _ = DependencyOne.Touch();
            return ValueTask.FromResult(request.Code);
        }
    }

    private sealed class LookupTourHandler5(
        HandlerDependencyOne dependencyOne,
        HandlerDependencyTwo dependencyTwo,
        HandlerDependencyThree dependencyThree,
        HandlerDependencyFour dependencyFour,
        HandlerDependencyFive dependencyFive) : IQueryHandler<LookupTour, string>
    {
        private HandlerDependencyOne DependencyOne { get; } = dependencyOne;

        private HandlerDependencyTwo DependencyTwo { get; } = dependencyTwo;

        private HandlerDependencyThree DependencyThree { get; } = dependencyThree;

        private HandlerDependencyFour DependencyFour { get; } = dependencyFour;

        private HandlerDependencyFive DependencyFive { get; } = dependencyFive;

        /// <inheritdoc />
        public ValueTask<string> Handle(LookupTour request, CancellationToken ct)
        {
            _ = DependencyOne.Touch()
                + DependencyTwo.Touch()
                + DependencyThree.Touch()
                + DependencyFour.Touch()
                + DependencyFive.Touch();
            return ValueTask.FromResult(request.Code);
        }
    }

    private sealed class HandlerDependencyOne(int value)
    {
        private int Value { get; } = value;

        public int Touch() => Value;
    }

    private sealed class HandlerDependencyTwo(int value)
    {
        private int Value { get; } = value;

        public int Touch() => Value;
    }

    private sealed class HandlerDependencyThree(int value)
    {
        private int Value { get; } = value;

        public int Touch() => Value;
    }

    private sealed class HandlerDependencyFour(int value)
    {
        private int Value { get; } = value;

        public int Touch() => Value;
    }

    private sealed class HandlerDependencyFive(int value)
    {
        private int Value { get; } = value;

        public int Touch() => Value;
    }

    private sealed class BenchmarkAppMediator(IServiceProvider services) : IMediator
    {
        private IServiceProvider Services { get; } = services;

        /// <inheritdoc />
        public ValueTask<string> Send(LookupTour request, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(request);

            var handler = ServiceProviderServiceExtensions.GetRequiredService<IQueryHandler<LookupTour, string>>(Services);
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
        public IAsyncEnumerable<TResponse> Send<TResponse>(IStreamRequest<TResponse> request, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(request);
            throw new NotSupportedException("Benchmark stream dispatch is not implemented.");
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
