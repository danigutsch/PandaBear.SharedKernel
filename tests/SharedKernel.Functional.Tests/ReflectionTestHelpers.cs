using System.Reflection;

namespace SharedKernel.Functional.Tests;

internal static class ReflectionTestHelpers
{
    public static ArgumentNullException InvokeSingleParameterStaticGenericAndUnwrapArgumentNull(
        Type declaringType,
        string methodName,
        Type genericArgument,
        object? argument)
    {
        var method = declaringType
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.GetParameters().Length == 1 && method.Name == methodName)
            .MakeGenericMethod(genericArgument);

        return UnwrapArgumentNullException(() => method.Invoke(null, [argument]));
    }

    public static ArgumentNullException InvokeStaticGenericAndUnwrapArgumentNull(
        Type declaringType,
        string methodName,
        Type[] genericArguments,
        Func<ParameterInfo[], bool> parameterMatcher,
        object?[] arguments)
    {
        var method = declaringType
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.Name == methodName && method.GetGenericArguments().Length == genericArguments.Length)
            .Select(method => method.MakeGenericMethod(genericArguments))
            .Single(method => parameterMatcher(method.GetParameters()));

        Assert.NotNull(method);
        return UnwrapArgumentNullException(() => method.Invoke(null, arguments));
    }

    public static ArgumentNullException InvokeInstanceAndUnwrapArgumentNull(
        object instance,
        string methodName,
        Type[] genericArguments,
        Func<ParameterInfo[], bool> parameterMatcher,
        object?[] arguments)
    {
        var method = instance.GetType()
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(method => method.Name == methodName && method.GetGenericArguments().Length == genericArguments.Length)
            .Select(method => genericArguments.Length == 0 ? method : method.MakeGenericMethod(genericArguments))
            .Single(method => parameterMatcher(method.GetParameters()));

        Assert.NotNull(method);
        return UnwrapArgumentNullException(() => method.Invoke(instance, arguments));
    }

    public static Exception InvokeConstructorAndCapture(
        Type declaringType,
        Type[] parameterTypes,
        object?[] arguments)
    {
        var constructor = declaringType.GetConstructor(
            BindingFlags.Public | BindingFlags.Instance,
            binder: null,
            types: parameterTypes,
            modifiers: null);

        Assert.NotNull(constructor);
        var exception = Assert.Throws<TargetInvocationException>(() => constructor.Invoke(arguments));
        Assert.NotNull(exception.InnerException);
        return exception.InnerException;
    }

    private static ArgumentNullException UnwrapArgumentNullException(Func<object?> invoke)
    {
        try
        {
            var result = invoke();
            return AwaitAndUnwrapArgumentNullException(result);
        }
        catch (TargetInvocationException exception)
        {
            Assert.NotNull(exception.InnerException);
            return Assert.IsType<ArgumentNullException>(exception.InnerException);
        }
    }

    private static ArgumentNullException AwaitAndUnwrapArgumentNullException(object? result)
    {
        if (result is Task task)
        {
            var exception = Record.Exception(() => task.GetAwaiter().GetResult());
            Assert.NotNull(exception);
            return Assert.IsType<ArgumentNullException>(exception);
        }

        if (result is not null)
        {
            var resultType = result.GetType();
            if (resultType.IsGenericType && resultType.GetGenericTypeDefinition() == typeof(ValueTask<>))
            {
                var asTaskMethod = resultType.GetMethod(nameof(ValueTask<int>.AsTask), BindingFlags.Public | BindingFlags.Instance);
                Assert.NotNull(asTaskMethod);
                var asyncTask = (Task)asTaskMethod.Invoke(result, null)!;
                var exception = Record.Exception(() => asyncTask.GetAwaiter().GetResult());
                Assert.NotNull(exception);
                return Assert.IsType<ArgumentNullException>(exception);
            }
        }

        throw new Xunit.Sdk.XunitException("Expected an ArgumentNullException, but no exception was thrown.");
    }

    public static bool HasSingleParameterOf(Type parameterType, ParameterInfo[] parameters) =>
        parameters.Length == 1 && parameters[0].ParameterType == parameterType;

    public static bool HasSingleFuncParameterReturning(Type resultTypeDefinition, ParameterInfo[] parameters)
    {
        if (parameters.Length != 1 || !parameters[0].ParameterType.IsGenericType)
        {
            return false;
        }

        var parameterType = parameters[0].ParameterType;
        if (parameterType.GetGenericTypeDefinition() != typeof(Func<,>))
        {
            return false;
        }

        var resultType = parameterType.GetGenericArguments()[1];
        return resultType.IsGenericType && resultType.GetGenericTypeDefinition() == resultTypeDefinition;
    }

    public static bool HasTwoParameters(Type firstParameterType, Type secondParameterType, ParameterInfo[] parameters) =>
        parameters.Length == 2
        && parameters[0].ParameterType == firstParameterType
        && parameters[1].ParameterType == secondParameterType;

    public static bool HasTwoParametersWithReturnKinds(Type firstResultTypeDefinition, Type secondResultTypeDefinition, ParameterInfo[] parameters)
    {
        if (parameters.Length != 2)
        {
            return false;
        }

        return MatchesFuncReturn(parameters[0].ParameterType, firstResultTypeDefinition)
            && MatchesFuncReturn(parameters[1].ParameterType, secondResultTypeDefinition);
    }

    private static bool MatchesFuncReturn(Type type, Type resultTypeDefinition)
    {
        if (!type.IsGenericType)
        {
            return false;
        }

        var genericDefinition = type.GetGenericTypeDefinition();
        if (genericDefinition != typeof(Func<>) && genericDefinition != typeof(Func<,>))
        {
            return false;
        }

        var resultType = type.GetGenericArguments()[^1];
        return resultType.IsGenericType && resultType.GetGenericTypeDefinition() == resultTypeDefinition;
    }
}
