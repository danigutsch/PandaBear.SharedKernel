using System.Reflection;
using Microsoft.Extensions.Hosting;

namespace SharedKernel.Testing.AspNetCore;

/// <summary>Captures the Generic Host built by a managed worker entry point before startup.</summary>
public static class WorkerTestHost
{
    /// <summary>Invokes the entry point and returns its built, unstarted host.</summary>
    /// <param name="assembly">The assembly containing the managed executable entry point.</param>
    /// <param name="args">The arguments for an entry point accepting a string array.</param>
    /// <returns>The built host. The caller owns its disposal.</returns>
    /// <remarks>
    /// The helper invokes trusted test executables in-process. It stops the entry point at the hosting
    /// diagnostic event and forwards ordinary entry-point exceptions to the caller.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="assembly"/> or <paramref name="args"/> is null.</exception>
    /// <exception cref="InvalidOperationException">There is no managed entry point or it returns without building a host.</exception>
    /// <exception cref="AggregateException">The entry point and cleanup of its captured host both failed.</exception>
    public static IHost Build(Assembly assembly, params string[] args)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentNullException.ThrowIfNull(args);
        var entryPoint = assembly.EntryPoint
            ?? throw new InvalidOperationException("The assembly has no managed entry point.");
        object?[]? invocationArguments = entryPoint.GetParameters().Length == 0 ? null : [args];
        using var capture = new WorkerHostCapture();
        try
        {
            return capture.Invoke(entryPoint, invocationArguments);
        }
        catch (Exception startupFailure)
        {
            try
            {
                capture.Dispose();
            }
            catch (Exception cleanupFailure)
            {
                throw new AggregateException(startupFailure, cleanupFailure);
            }

            throw;
        }
    }
}
