using System.Reflection;
using System.Runtime.ExceptionServices;

namespace PandaBear.Testing;

/// <summary>
/// Wraps a service so a test can hold a call, make it fail, follow it up, or replace its result, without writing a decorator class per service.
/// Hooks are chosen by method name and apply to methods returning <see cref="Task"/>, <see cref="Task{TResult}"/>, <see cref="ValueTask"/>,
/// <see cref="ValueTask{TResult}"/> or a value (or nothing) synchronously.
/// </summary>
/// <remarks>
/// The service must be a public interface, because the runtime generates the proxy type. Hooks are chosen by method name only, so
/// overloads share their hooks, and so do same-named methods of different interfaces; use it for a method with a unique name. Hooks of a synchronous method are waited for
/// on the calling thread, so a hook that stays pending blocks that call; use it only with tasks that complete without the caller's help.
/// </remarks>
public class CallProxy : DispatchProxy
{
    private object _inner = null!;
    private CallHooks _hooks = new();

    /// <summary>Creates a proxy of <typeparamref name="TService"/> over a real implementation.</summary>
    /// <typeparam name="TService">The service interface; it must be public.</typeparam>
    /// <param name="inner">The real service.</param>
    /// <param name="configure">Adds the hooks, or <see langword="null"/> for a plain pass-through.</param>
    /// <returns>The proxy.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="inner"/> is null.</exception>
    /// <exception cref="ArgumentException">A hook or replacement is registered for a name that matches no method of the service.</exception>
    /// <exception cref="InvalidOperationException">
    /// <see cref="CallHooks.Returns"/> is registered for a name whose methods all return no value or all have out or ref parameters.
    /// </exception>
    public static TService Create<TService>(TService inner, Action<CallHooks>? configure = null)
        where TService : class
    {
        ArgumentNullException.ThrowIfNull(inner);

        var proxy = Create<TService, CallProxy>();
        var callProxy = (CallProxy)(object)proxy;
        callProxy._inner = inner;
        configure?.Invoke(callProxy._hooks);
        callProxy._hooks.Validate(typeof(TService));
        return proxy;
    }

    /// <inheritdoc />
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(targetMethod);

        args ??= [];
        var call = new CallInfo(targetMethod.Name, new System.Collections.ObjectModel.ReadOnlyCollection<object?>(args));
        var returnType = targetMethod.ReturnType;
        if (returnType == typeof(Task))
        {
            return RunVoid(targetMethod, call, args);
        }

        if (returnType == typeof(ValueTask))
        {
#pragma warning disable CA2012 // DispatchProxy returns the value task boxed as an object; the caller awaits it.
            return RunValueVoid(targetMethod, call, args);
#pragma warning restore CA2012
        }

        if (returnType.IsGenericType)
        {
            var definition = returnType.GetGenericTypeDefinition();
            if (definition == typeof(Task<>))
            {
                return InvokeGeneric(nameof(RunWithResult), returnType, targetMethod, call, args);
            }

            if (definition == typeof(ValueTask<>))
            {
                return InvokeGeneric(nameof(RunValueWithResult), returnType, targetMethod, call, args);
            }
        }

        return RunSynchronously(targetMethod, call, args);
    }

    private object? InvokeGeneric(string name, Type returnType, MethodInfo method, CallInfo call, object?[] args)
    {
        var run = typeof(CallProxy).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
        return run.MakeGenericMethod(returnType.GenericTypeArguments[0]).Invoke(this, [method, call, args]);
    }

    private async Task RunVoid(MethodInfo method, CallInfo call, object?[] args)
    {
        await CallHooks.Run(_hooks.BeforeCalls, call).ConfigureAwait(false);
        await ((Task)Forward(method, args)!).ConfigureAwait(false);
        await CallHooks.Run(_hooks.AfterCalls, call).ConfigureAwait(false);
    }

    private async Task<T> RunWithResult<T>(MethodInfo method, CallInfo call, object?[] args)
    {
        await CallHooks.Run(_hooks.BeforeCalls, call).ConfigureAwait(false);
        call.Result = _hooks.Replacements.TryGetValue(call.Method, out var replacement)
            ? Replacement(method, typeof(T), replacement)
            : await ((Task<T>)Forward(method, args)!).ConfigureAwait(false);
        await CallHooks.Run(_hooks.AfterCalls, call).ConfigureAwait(false);
        return (T)call.Result!;
    }

    private async ValueTask RunValueVoid(MethodInfo method, CallInfo call, object?[] args)
    {
        await CallHooks.Run(_hooks.BeforeCalls, call).ConfigureAwait(false);
        await ((ValueTask)Forward(method, args)!).ConfigureAwait(false);
        await CallHooks.Run(_hooks.AfterCalls, call).ConfigureAwait(false);
    }

    private async ValueTask<T> RunValueWithResult<T>(MethodInfo method, CallInfo call, object?[] args)
    {
        await CallHooks.Run(_hooks.BeforeCalls, call).ConfigureAwait(false);
        call.Result = _hooks.Replacements.TryGetValue(call.Method, out var replacement)
            ? Replacement(method, typeof(T), replacement)
            : await ((ValueTask<T>)Forward(method, args)!).ConfigureAwait(false);
        await CallHooks.Run(_hooks.AfterCalls, call).ConfigureAwait(false);
        return (T)call.Result!;
    }

    private object? RunSynchronously(MethodInfo method, CallInfo call, object?[] args)
    {
        CallHooks.Run(_hooks.BeforeCalls, call).GetAwaiter().GetResult();
        call.Result = method.ReturnType != typeof(void) && _hooks.Replacements.TryGetValue(call.Method, out var replacement)
            ? Replacement(method, method.ReturnType, replacement)
            : Forward(method, args);
        CallHooks.Run(_hooks.AfterCalls, call).GetAwaiter().GetResult();
        return call.Result;
    }

    private static object? Replacement(MethodInfo method, Type expected, object? value)
    {
        var fits = value is null
            ? !expected.IsValueType || Nullable.GetUnderlyingType(expected) is not null
            : expected.IsInstanceOfType(value);
        return fits
            ? value
            : throw new InvalidOperationException(
                $"CallProxy.Returns for '{method.Name}' cannot supply {(value is null ? "null" : value.GetType().FullName)} where {expected.FullName} is expected.");
    }

    private object? Forward(MethodInfo method, object?[] args)
    {
        try
        {
            return method.Invoke(_inner, args);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }
}
