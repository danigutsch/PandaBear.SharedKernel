using SharedKernel.Results;

namespace SharedKernel.BuildingBlocks;

/// <summary>
/// Extension methods for converting optional identified models to results.
/// </summary>
public static class OptionResultExtensions
{
    /// <summary>
    /// Converts an option to a successful result or a standard not-found result for an identified model.
    /// </summary>
    /// <typeparam name="TEntity">The identified model type.</typeparam>
    /// <typeparam name="TId">The identifier type.</typeparam>
    /// <param name="source">The option to convert.</param>
    /// <param name="id">The identifier requested from the lookup.</param>
    /// <param name="container">An optional context that contains the identified model.</param>
    /// <returns>A successful result containing the option value, or a not-found result when the option is empty.</returns>
    public static Result<TEntity> ToNotFoundResult<TEntity, TId>(
        this Option<TEntity> source,
        TId id,
        string? container = null)
        where TEntity : IIdentified<TId>
        where TId : notnull =>
        source.Match(
            Result.Ok,
            () => Result.NotFound<TEntity>(
                $"{typeof(TEntity).Name} with ID {id} not found{(string.IsNullOrWhiteSpace(container) ? string.Empty : $" in {container}")}."));
}
