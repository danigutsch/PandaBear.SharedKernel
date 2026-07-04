namespace SharedKernel.Mediator.EntityFrameworkCore.Tests;

internal sealed class TestCommandTransactionBehavior<TRequest, TResponse>(TestDbContext dbContext)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly EfCoreCommandTransactionBehavior<TestDbContext, TRequest, TResponse> behavior = new(dbContext);

    public ValueTask<TResponse> Handle(
        TRequest request,
        RequestHandlerContinuation<TResponse> next,
        CancellationToken ct) => behavior.Handle(request, next, ct);
}
