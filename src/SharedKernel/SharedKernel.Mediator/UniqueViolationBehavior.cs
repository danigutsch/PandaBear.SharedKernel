using PandaBear.BuildingBlocks;
using PandaBear.Results;

namespace PandaBear.Mediator;

/// <summary>
/// Converts a <see cref="UniqueConstraintViolationException"/> thrown while a request runs into a failed result, so handlers that return
/// <see cref="Result"/> or <see cref="Result{T}"/> need no try/catch of their own when two concurrent requests race past an existence check.
/// </summary>
/// <remarks>
/// <para>
/// A violated rule that is mapped in the <see cref="UniqueRuleMap"/> becomes <see cref="ResultStatus.Invalid"/> with one field error (the mapped
/// field and message). Any other rule, or an application without a map, becomes <see cref="ResultStatus.Conflict"/> with
/// <see cref="ConflictMessage"/>. The rule name, exception text and inner exception are never copied into the result.
/// </para>
/// <para>
/// Every other exception, including <see cref="OperationCanceledException"/>, propagates unchanged, and a returned result passes through
/// untouched. The package assembly is not a mediator module, so the generator does not discover this class: derive a sealed class in your own
/// module assembly (see the package README), or register a closed pipeline with
/// <see cref="UniqueViolationServiceCollectionExtensions.AddUniqueViolationHandling{TRequest, TResponse}(Microsoft.Extensions.DependencyInjection.IServiceCollection)"/>.
/// </para>
/// </remarks>
/// <typeparam name="TRequest">The mediator request type.</typeparam>
/// <typeparam name="TResponse">The <see cref="Result"/> or <see cref="Result{T}"/> the request produces.</typeparam>
/// <param name="map">The rule map, or <see langword="null"/> to answer every violation with a conflict.</param>
public class UniqueViolationBehavior<TRequest, TResponse>(UniqueRuleMap? map = null) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : struct, IResultLike<TResponse>
{
    /// <summary>The fixed, data-free detail of the conflict result for a rule that is not mapped.</summary>
    public const string ConflictMessage = "A value that must be unique is already in use.";

    /// <summary>The fixed summary of the validation result for a mapped rule.</summary>
    public const string ValidationDetail = "One or more validation errors occurred.";

    /// <inheritdoc />
    public async ValueTask<TResponse> Handle(TRequest request, RequestHandlerContinuation<TResponse> next, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(next);

        try
        {
            return await next().ConfigureAwait(false);
        }
        catch (UniqueConstraintViolationException violation)
        {
            if (map is not null && map.TryGet(violation.Rule, out var error))
            {
                return TResponse.Invalid(ValidationDetail, error.Field, error.Message);
            }

            return TResponse.Conflict(ConflictMessage);
        }
    }
}
