using Microsoft.Extensions.DependencyInjection;

namespace PandaBear.Mediator;

/// <summary>
/// Sends each request in its own service scope, and lets the caller carry per-circuit state into that scope. Components that live
/// as long as a connection, such as Blazor Server circuits, would otherwise share one scoped <c>DbContext</c> and see stale data;
/// one scope per operation keeps every request independent.
/// </summary>
/// <remarks>
/// <para>
/// <b>What each new scope lacks.</b> A new operation scope does not contain the circuit's scoped state. Per-connection scoped
/// services, such as the <c>AuthenticationStateProvider</c>, a current-user or tenant holder, or an audit context, resolve inside
/// the handler as new, uninitialised instances. Register <see cref="ScopedSender"/> with <c>AddScopedSender()</c> (scoped lifetime),
/// so the instance a component receives holds the provider of the scope it was resolved in (for a Blazor component, the circuit
/// scope), and carry the state over with the scope initializer. The initializer receives the caller's provider first and the new
/// operation's provider second, and runs before the handler's sender is resolved:
/// </para>
/// <code>
/// services.AddScopedSender((caller, operation) =>
/// {
///     operation.GetRequiredService&lt;CurrentUserHolder&gt;().Set(caller.GetRequiredService&lt;CurrentUserHolder&gt;().Current);
///     return ValueTask.CompletedTask;
/// });
/// </code>
/// <para>
/// When practical, prefer passing the actor or identity explicitly in the command: it needs no framework support. When state must
/// be reached from places that cannot take a <see cref="ScopedSender"/>, Microsoft documents a <c>CircuitServicesAccessor</c> (an
/// <c>AsyncLocal</c> filled by a <c>CircuitHandler</c>) under "Access server-side Blazor services from a different DI scope" at
/// https://learn.microsoft.com/aspnet/core/blazor/fundamentals/dependency-injection.
/// </para>
/// </remarks>
/// <param name="scopeFactory">The scope factory used to create one scope per send.</param>
/// <param name="callerServices">The provider of the scope this sender lives in, such as a Blazor circuit scope. It is passed to the initializer.</param>
/// <param name="scopeInitializer">
/// An optional callback that runs once per send, before the sender is resolved from the new scope. It receives the caller's provider
/// and the new operation scope's provider, so it can copy state from the first to the second. When it throws, the exception
/// propagates and the scope is still disposed.
/// </param>
public sealed class ScopedSender(
    IServiceScopeFactory scopeFactory,
    IServiceProvider callerServices,
    Func<IServiceProvider, IServiceProvider, ValueTask>? scopeInitializer = null)
{
    /// <summary>Sends a request in a fresh scope without cancellation.</summary>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="request">The request.</param>
    /// <returns>The response.</returns>
    public ValueTask<TResponse> Send<TResponse>(IRequest<TResponse> request) => Send(request, CancellationToken.None);

    /// <summary>Sends a request in a fresh scope, disposed when the request completes.</summary>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="request">The request.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The response.</returns>
    /// <remarks>
    /// The scope is new: state held by scoped services of the caller, such as a Blazor circuit's current user, is not visible to the
    /// handler unless the scope initializer copies it. See <see cref="ScopedSender"/>.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is <see langword="null"/>.</exception>
    public async ValueTask<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var scope = scopeFactory.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            if (scopeInitializer is not null)
            {
                await scopeInitializer(callerServices, scope.ServiceProvider).ConfigureAwait(false);
            }

            return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request, ct).ConfigureAwait(false);
        }
    }
}
