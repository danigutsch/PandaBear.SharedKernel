using System.Reflection;

namespace SharedKernel.Mediator.GeneratorTests;

internal sealed class GeneratedMediatorRuntimeContext : IDisposable
{
    private readonly Assembly assembly;
    private readonly Type mediatorType;

    private GeneratedMediatorRuntimeContext(Assembly assembly, Type mediatorType)
    {
        this.assembly = assembly;
        this.mediatorType = mediatorType;
    }

    public static GeneratedMediatorRuntimeContext Create(string source)
    {
        const string runtimeUsings = """
            using System;
            using System.Collections.Generic;
            using System.Threading;
            using System.Threading.Tasks;

            """;
        const string serviceProviderExtensionsSource = """
            namespace Microsoft.Extensions.DependencyInjection;

            public static class ServiceProviderServiceExtensions
            {
                public static T GetRequiredService<T>(global::System.IServiceProvider provider)
                {
                    global::System.ArgumentNullException.ThrowIfNull(provider);

                    var service = provider.GetService(typeof(T));
                    if (service is T typed)
                    {
                        return typed;
                    }

                    throw new global::System.InvalidOperationException($"No service for type '{typeof(T).FullName}'.");
                }
            }
            """;
        var runResult = GeneratorTestHarness.RunGeneratorDriver(source);
        var generatedMediatorSource = GeneratorTestHarness.GetGeneratedSource(
            runResult,
            "SharedKernel.Mediator.Generated.AppMediator.g.cs");
        var generatedDispatchSource = GeneratorTestHarness.GetGeneratedSource(
            runResult,
            "SharedKernel.Mediator.Generated.GeneratedDispatch.g.cs");
        var generatedPipelinesSource = GeneratorTestHarness.GetGeneratedSource(
            runResult,
            "SharedKernel.Mediator.Generated.GeneratedPipelines.g.cs");
        var runtimeCompilation = GeneratorTestHarness.CreateCompilation(
            [runtimeUsings + source, generatedMediatorSource, generatedDispatchSource, generatedPipelinesSource, serviceProviderExtensionsSource],
            assemblyName: "SharedKernel.Mediator.Tests.GeneratedDispatchRuntime");
        var assembly = GeneratorTestHarness.LoadAssembly(runtimeCompilation);
        var mediatorType = assembly.GetType("SharedKernel.Mediator.AppMediator", throwOnError: true)!;

        return new GeneratedMediatorRuntimeContext(assembly, mediatorType);
    }

    public Type GetRequiredType(string typeName)
    {
        return assembly.GetType(typeName, throwOnError: true)!;
    }

    public object CreateInstance(string typeName, params object[] arguments)
    {
        return Activator.CreateInstance(GetRequiredType(typeName), arguments)!;
    }

    public T CreateInstance<T>(string typeName, params object[] arguments)
    {
        return (T)CreateInstance(typeName, arguments);
    }

    public object CreateGenericInstance(string typeName, Type[] typeArguments, params object[] arguments)
    {
        return Activator.CreateInstance(GetRequiredType(typeName).MakeGenericType(typeArguments), arguments)!;
    }

    public IMediator CreateMediator(params object[] services)
    {
        return (IMediator)Activator.CreateInstance(mediatorType, new TestServiceProvider(services))!;
    }

    public string[] ReadTraceEntries(string typeName = "Demo.TraceLog")
    {
        var traceType = GetRequiredType(typeName);
        var entries = (IReadOnlyList<string>)traceType.GetProperty("Entries", BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!;
        return [.. entries];
    }

    public void ClearTraceEntries(string typeName = "Demo.TraceLog")
    {
        var traceType = GetRequiredType(typeName);
        var entries = (System.Collections.IList)traceType.GetProperty("Entries", BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!;
        entries.Clear();
    }

    public void Dispose()
    {
        if (assembly is IDisposable disposableAssembly)
        {
            disposableAssembly.Dispose();
        }
    }

    private sealed class TestServiceProvider(params object[] services) : IServiceProvider
    {
        private readonly Dictionary<Type, object> services = services.ToDictionary(static service => service.GetType());

        public object? GetService(Type serviceType)
        {
            return services.TryGetValue(serviceType, out var service) ? service : null;
        }
    }
}
