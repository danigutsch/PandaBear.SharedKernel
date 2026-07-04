using Microsoft.EntityFrameworkCore;
using SharedKernel.EntityFrameworkCore;

namespace SharedKernel.Mediator.EntityFrameworkCore;

/// <summary>
/// Wraps mediator commands in an EF Core execution-strategy-compatible transaction.
/// </summary>
/// <typeparam name="TRequest">The mediator request type.</typeparam>
/// <typeparam name="TResponse">The mediator response type.</typeparam>
public sealed class EfCoreCommandTransactionBehavior<TRequest, TResponse>(DbContext dbContext)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    /// <inheritdoc />
    public ValueTask<TResponse> Handle(
        TRequest request,
        RequestHandlerContinuation<TResponse> next,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(next);

        return request is ICommand or ICommand<TResponse>
            ? EfCoreCommandTransactionScope.Execute(dbContext, () => next(), ct)
            : next();
    }
}
