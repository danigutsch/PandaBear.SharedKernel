namespace PandaBear.Mediator.EntityFrameworkCore.Tests;

/// <summary>A pipeline behavior that delegates to EfCoreCommandTransactionBehavior over a supplied context, so tests can construct it directly.</summary>
/// <typeparam name="TRequest">The request type, which must produce TResponse.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
/// <param name="dbContext">The context the wrapped behavior runs its transaction on.</param>
internal sealed class TestCommandTransactionBehavior<TRequest, TResponse>(TestDbContext dbContext)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly EfCoreCommandTransactionBehavior<TRequest, TResponse> behavior = new(dbContext);

    /// <summary>Forwards the request and continuation to the wrapped EF Core transaction behavior.</summary>
    public ValueTask<TResponse> Handle(
        TRequest request,
        RequestHandlerContinuation<TResponse> next,
        CancellationToken ct) => behavior.Handle(request, next, ct);
}
