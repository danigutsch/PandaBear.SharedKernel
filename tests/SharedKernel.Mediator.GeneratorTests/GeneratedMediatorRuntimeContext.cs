using System.Diagnostics.Metrics;
using System.Linq.Expressions;
using System.Reflection;

namespace SharedKernel.Mediator.GeneratorTests;

internal sealed class GeneratedMediatorRuntimeContext : IDisposable
{
    private readonly Assembly assembly;
    private readonly Type mediatorType;
    private readonly TestMeterFactory meterFactory = new();

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
            [runtimeUsings + source, generatedMediatorSource, generatedDispatchSource, generatedPipelinesSource],
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
        var resolvedServices = services.ToList();
        if (!resolvedServices.OfType<AppMediatorInstrumentation>().Any())
        {
            resolvedServices.Add(new AppMediatorInstrumentation(meterFactory));
        }

        var constructor = mediatorType.GetConstructors().ShouldHaveSingleItem();
        var arguments = constructor.GetParameters()
            .Select(parameter => ResolveService(parameter.ParameterType, resolvedServices))
            .ToArray();

        return (IMediator)constructor.Invoke(arguments);
    }

    private static object ResolveService(Type parameterType, IReadOnlyList<object> services)
    {
        var directService = services.FirstOrDefault(parameterType.IsInstanceOfType);
        if (directService is not null)
        {
            return directService;
        }

        if (parameterType.IsGenericType && parameterType.GetGenericTypeDefinition() == typeof(Func<>))
        {
            var serviceType = parameterType.GenericTypeArguments[0];
            var service = services.FirstOrDefault(serviceType.IsInstanceOfType)
                ?? throw new InvalidOperationException($"No generated mediator dependency was supplied for '{parameterType.FullName}'.");
            var body = Expression.Convert(Expression.Constant(service), serviceType);
            return Expression.Lambda(parameterType, body).Compile();
        }

        throw new InvalidOperationException($"No generated mediator dependency was supplied for '{parameterType.FullName}'.");
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
        meterFactory.Dispose();
        if (assembly is IDisposable disposableAssembly)
        {
            disposableAssembly.Dispose();
        }
    }

    private sealed class TestMeterFactory : IMeterFactory
    {
        private readonly List<Meter> meters = [];

        Meter IMeterFactory.Create(MeterOptions options)
        {
            var meter = new Meter(options);
            meters.Add(meter);
            return meter;
        }

        public void Dispose()
        {
            foreach (var meter in meters)
            {
                meter.Dispose();
            }
        }
    }
}
