using System.Reflection;

namespace SharedKernel.Functional.Tests;

internal static class InvocationTestHelpers
{
    public static TException InvokeStaticGenericAndCapture<TException>(
        Type declaringType,
        string methodName,
        Type[] genericArguments,
        Type[] parameterTypes,
        object?[] arguments)
        where TException : Exception
    {
        var method = declaringType
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.Name == methodName && method.GetGenericArguments().Length == genericArguments.Length)
            .Select(method => method.MakeGenericMethod(genericArguments))
            .Single(method => HasExactParameterTypes(method, parameterTypes));

        return InvokeAndCapture<TException>(() => method.Invoke(null, arguments));
    }

    public static TException InvokeInstanceGenericAndCapture<TException>(
        object instance,
        string methodName,
        Type[] genericArguments,
        Type[] parameterTypes,
        object?[] arguments)
        where TException : Exception
    {
        var method = instance.GetType()
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(method => method.Name == methodName && method.GetGenericArguments().Length == genericArguments.Length)
            .Select(method => method.MakeGenericMethod(genericArguments))
            .Single(method => HasExactParameterTypes(method, parameterTypes));

        return InvokeAndCapture<TException>(() => method.Invoke(instance, arguments));
    }

    public static TException InvokeConstructorAndCapture<TException>(
        Type declaringType,
        Type[] parameterTypes,
        object?[] arguments)
        where TException : Exception
    {
        var constructor = declaringType.GetConstructor(
            BindingFlags.Public | BindingFlags.Instance,
            binder: null,
            types: parameterTypes,
            modifiers: null);

        Assert.NotNull(constructor);
        return InvokeAndCaptureAssignable<TException>(() => constructor.Invoke(arguments));
    }

    private static TException InvokeAndCapture<TException>(Func<object?> invoke)
        where TException : Exception
    {
        try
        {
            var result = invoke();

            if (result is Task task)
            {
                var exception = Record.Exception(() => task.GetAwaiter().GetResult());
                Assert.NotNull(exception);
                return Assert.IsType<TException>(exception);
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
                    return Assert.IsType<TException>(exception);
                }
            }
        }
        catch (TargetInvocationException exception)
        {
            Assert.NotNull(exception.InnerException);
            return Assert.IsType<TException>(exception.InnerException);
        }

        throw new Xunit.Sdk.XunitException($"Expected exception of type {typeof(TException).Name}, but no exception was thrown.");
    }

    private static TException InvokeAndCaptureAssignable<TException>(Func<object?> invoke)
        where TException : Exception
    {
        try
        {
            invoke();
        }
        catch (TargetInvocationException exception)
        {
            Assert.NotNull(exception.InnerException);
            return Assert.IsAssignableFrom<TException>(exception.InnerException);
        }

        throw new Xunit.Sdk.XunitException($"Expected exception assignable to {typeof(TException).Name}, but no exception was thrown.");
    }

    private static bool HasExactParameterTypes(MethodInfo method, Type[] parameterTypes)
    {
        var parameters = method.GetParameters();
        if (parameters.Length != parameterTypes.Length)
        {
            return false;
        }

        for (var index = 0; index < parameters.Length; index++)
        {
            if (parameters[index].ParameterType != parameterTypes[index])
            {
                return false;
            }
        }

        return true;
    }
}
