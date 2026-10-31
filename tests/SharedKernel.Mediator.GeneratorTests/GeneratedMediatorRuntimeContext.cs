using System.Linq.Expressions;
using System.Reflection;
using Microsoft.CodeAnalysis;

namespace PandaBear.Mediator.GeneratorTests;

/// <summary>Compiles test source together with the generated AppMediator, dispatch and pipeline sources into an in-memory assembly and builds mediator instances from it for runtime assertions.</summary>
internal sealed class GeneratedMediatorRuntimeContext : IDisposable
{
    private readonly Assembly assembly;
    private readonly Type mediatorType;
    private readonly MeterCollector meterFactory = new();

    private GeneratedMediatorRuntimeContext(Assembly assembly, Type mediatorType)
    {
        this.assembly = assembly;
        this.mediatorType = mediatorType;
    }

    /// <summary>Runs the generator over the source (with optional extra metadata references), compiles the source plus the generated mediator, dispatch and pipeline files, and wraps the loaded assembly and its PandaBear.Mediator.AppMediator type.</summary>
    public static GeneratedMediatorRuntimeContext Create(string source, IEnumerable<MetadataReference>? additionalReferences = null)
    {
        const string runtimeUsings = """
            using System;
            using System.Collections.Generic;
            using System.Threading;
            using System.Threading.Tasks;

            """;
        var references = additionalReferences?.ToArray() ?? [];
        var runResult = GeneratorTestHarness.RunGeneratorDriver(source, references);
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
            additionalReferences: references,
            assemblyName: "SharedKernel.Mediator.Tests.GeneratedDispatchRuntime");
        var assembly = GeneratorTestHarness.LoadAssembly(runtimeCompilation);
        var mediatorType = assembly.GetType("PandaBear.Mediator.AppMediator", throwOnError: true)!;

        return new GeneratedMediatorRuntimeContext(assembly, mediatorType);
    }

    /// <summary>Returns the named type from the compiled assembly, throwing when it does not exist.</summary>
    public Type GetRequiredType(string typeName)
    {
        return assembly.GetType(typeName, throwOnError: true)!;
    }

    /// <summary>Creates an instance of the named compiled type with the given constructor arguments.</summary>
    public object CreateInstance(string typeName, params object[] arguments)
    {
        return Activator.CreateInstance(GetRequiredType(typeName), arguments)!;
    }

    /// <summary>Creates an instance of the named compiled type and casts it to T.</summary>
    public T CreateInstance<T>(string typeName, params object[] arguments)
    {
        return (T)CreateInstance(typeName, arguments);
    }

    /// <summary>Creates an instance of the named open generic type closed over the given type arguments.</summary>
    public object CreateGenericInstance(string typeName, Type[] typeArguments, params object[] arguments)
    {
        return Activator.CreateInstance(GetRequiredType(typeName).MakeGenericType(typeArguments), arguments)!;
    }

    /// <summary>Instantiates the generated mediator through its single constructor, matching each parameter to the supplied services (wrapping a service in a Func when needed) and adding a default AppMediatorInstrumentation when none is given.</summary>
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

    /// <summary>Reads the static Entries list of the named trace type (default Demo.TraceLog) and returns a copy of it.</summary>
    public string[] ReadTraceEntries(string typeName = "Demo.TraceLog")
    {
        var traceType = GetRequiredType(typeName);
        var entries = (IReadOnlyList<string>)traceType.GetProperty("Entries", BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!;
        return [.. entries];
    }

    /// <summary>Empties the static Entries list of the named trace type (default Demo.TraceLog).</summary>
    public void ClearTraceEntries(string typeName = "Demo.TraceLog")
    {
        var traceType = GetRequiredType(typeName);
        var entries = (System.Collections.IList)traceType.GetProperty("Entries", BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!;
        entries.Clear();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        meterFactory.Dispose();
        if (assembly is IDisposable disposableAssembly)
        {
            disposableAssembly.Dispose();
        }
    }
}
