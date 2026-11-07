using PandaBear.BuildingBlocks;
using PandaBear.Results;

namespace PandaBear.Mediator;

/// <summary>
/// Converts a <see cref="ConcurrencyConflictException"/> thrown while a request runs into a failed result with
/// <see cref="ResultStatus.Conflict"/>, so handlers that return <see cref="Result"/> or <see cref="Result{T}"/> need no try/catch of their own.
/// </summary>
/// <remarks>
/// <para>
/// Only <see cref="ConcurrencyConflictException"/> is converted. Every other exception, including <see cref="OperationCanceledException"/>,
/// propagates unchanged, and a returned result passes through untouched. The result carries <see cref="ConflictMessage"/>; the exception
/// text and its inner exception are never copied into it.
/// </para>
/// <para>
/// The package assembly is not a mediator module, so the generator does not discover this class. Derive a sealed class in your own module
/// assembly and place it outside the transaction behavior (see the package README), or register a closed pipeline with
/// <see cref="ConcurrencyConflictServiceCollectionExtensions.AddConcurrencyConflictHandling{TRequest, TResponse}(Microsoft.Extensions.DependencyInjection.IServiceCollection)"/>.
/// </para>
/// </remarks>
/// <typeparam name="TRequest">The mediator request type.</typeparam>
/// <typeparam name="TResponse">The <see cref="Result"/> or <see cref="Result{T}"/> the request produces.</typeparam>
public class ConcurrencyConflictBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : struct, IResultLike<TResponse>
{
    /// <summary>The fixed, data-free detail of the conflict result.</summary>
    public const string ConflictMessage = "The data was changed or removed by someone else. Reload it and try again.";

    /// <inheritdoc />
    public async ValueTask<TResponse> Handle(TRequest request, RequestHandlerContinuation<TResponse> next, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(next);

        try
        {
            return await next().ConfigureAwait(false);
        }
        catch (ConcurrencyConflictException)
        {
            return TResponse.Conflict(ConflictMessage);
        }
    }
}
