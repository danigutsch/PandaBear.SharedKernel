using System.Diagnostics;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.Extensions.Hosting;

namespace PandaBear.Testing.AspNetCore;

/// <summary>Owns the diagnostics and host lifetime during one worker entry-point invocation.</summary>
internal sealed class WorkerHostCapture : IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object?>>, IDisposable
{
    private const string HostingListener = "Microsoft.Extensions.Hosting";
    private const string HostBuiltEvent = "HostBuilt";
    private static readonly AsyncLocal<WorkerHostCapture?> Current = new();
    private readonly Lock _gate = new();
    private readonly List<IDisposable> _subscriptions = [];
    private readonly HostAbortedException _abort = new();
    private IHost? _host;
    private bool _finished;

    /// <summary>Invokes the entry point and transfers ownership of the captured host on success.</summary>
    /// <exception cref="InvalidOperationException">The entry point returned without building a host.</exception>
    internal IHost Invoke(MethodInfo entryPoint, object?[]? arguments)
    {
        var previous = Current.Value;
        Current.Value = this;
        try
        {
            using var discovery = DiagnosticListener.AllListeners.Subscribe(this);
            try
            {
                entryPoint.Invoke(null, arguments);
            }
            catch (TargetInvocationException exception) when (ReferenceEquals(exception.InnerException, _abort))
            {
            }
            catch (TargetInvocationException exception) when (exception.InnerException is { } failure)
            {
                ExceptionDispatchInfo.Capture(failure).Throw();
            }
            finally
            {
                lock (_gate)
                {
                    _finished = true;
                }
            }
        }
        finally
        {
            Current.Value = previous;
        }

        StopObserving();
        lock (_gate)
        {
            var host = _host ?? throw new InvalidOperationException("The entry point returned without building a host.");
            _host = null;
            return host;
        }
    }

    /// <inheritdoc />
    public void OnNext(DiagnosticListener listener)
    {
        if (Current.Value != this || listener.Name != HostingListener)
        {
            return;
        }

        lock (_gate)
        {
            if (!_finished)
            {
                _subscriptions.Add(listener.Subscribe(this, static name => name == HostBuiltEvent));
            }
        }
    }

    /// <inheritdoc />
    public void OnNext(KeyValuePair<string, object?> diagnostic)
    {
        if (Current.Value != this || diagnostic.Key != HostBuiltEvent || diagnostic.Value is not IHost host)
        {
            return;
        }

        IHost? previous;
        lock (_gate)
        {
            if (_finished)
            {
                return;
            }

            previous = _host;
            _host ??= host;
        }

        if (previous is not null)
        {
            if (!ReferenceEquals(previous, host))
            {
                host.Dispose();
            }

            throw new InvalidOperationException("The entry point built another host after its first host was captured.");
        }

        throw _abort;
    }

    /// <inheritdoc />
    public void OnCompleted()
    {
    }

    /// <inheritdoc />
    public void OnError(Exception error)
    {
    }

    /// <inheritdoc />
    public void Dispose()
    {
        try
        {
            StopObserving();
        }
        finally
        {
            IHost? host;
            lock (_gate)
            {
                host = _host;
                _host = null;
            }

            host?.Dispose();
        }
    }

    private void StopObserving()
    {
        IDisposable[] subscriptions;
        lock (_gate)
        {
            _finished = true;
            subscriptions = [.. _subscriptions];
            _subscriptions.Clear();
        }

        foreach (var subscription in subscriptions)
        {
            subscription.Dispose();
        }
    }
}
