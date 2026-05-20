namespace SharedKernel.Functional;

/// <summary>
/// Collects and combines multiple validation errors into a single result.
/// </summary>
public sealed class ValidationErrors
{
    private const string MultipleValidationErrorsDetailMessage = "Multiple validation errors occurred.";

    private readonly List<Result> errors = [];

    /// <summary>
    /// Gets a value indicating whether any validation errors have been collected.
    /// </summary>
    public bool HasErrors => errors.Count > 0;

    /// <summary>
    /// Returns the combined validation error result.
    /// </summary>
    /// <returns>A combined invalid result.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no errors have been collected.</exception>
    public Result ToResult()
    {
        if (errors.Count == 0)
        {
            throw new InvalidOperationException("Cannot create result from empty error collection. Check HasErrors before calling ToResult.");
        }

        if (errors.Count == 1)
        {
            return errors[0];
        }

        return Result.Invalid(MultipleValidationErrorsDetailMessage, MergeValidationErrors());
    }

    /// <summary>
    /// Returns the combined validation error result as a generic result.
    /// </summary>
    /// <typeparam name="T">The success value type.</typeparam>
    /// <returns>A combined invalid result.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no errors have been collected.</exception>
    public Result<T> ToResult<T>()
        where T : notnull
    {
        if (errors.Count == 0)
        {
            throw new InvalidOperationException("Cannot create result from empty error collection. Check HasErrors before calling ToResult.");
        }

        if (errors.Count == 1)
        {
            var singleError = errors[0].ErrorDetails ?? throw new InvalidOperationException("Validation errors must include error details.");
            var validationErrors = singleError.ValidationErrors ?? throw new InvalidOperationException("Validation errors must include field details.");
            return Result.Invalid<T>(singleError.Detail, ToValidationDictionary(validationErrors));
        }

        return Result.Invalid<T>(MultipleValidationErrorsDetailMessage, MergeValidationErrors());
    }

    /// <summary>
    /// Adds a non-generic invalid result to the collection.
    /// </summary>
    /// <param name="error">The invalid result to add.</param>
    /// <exception cref="InvalidOperationException">Thrown when the result is not invalid.</exception>
    public void Add(Result error)
    {
        if (error.Status != ResultStatus.Invalid)
        {
            throw new InvalidOperationException("Only validation errors can be added.");
        }

        errors.Add(error);
    }

    /// <summary>
    /// Adds a generic invalid result to the collection.
    /// </summary>
    /// <typeparam name="T">The success value type.</typeparam>
    /// <param name="error">The invalid result to add.</param>
    /// <exception cref="InvalidOperationException">Thrown when the result is not invalid.</exception>
    public void Add<T>(Result<T> error)
        where T : notnull
    {
        if (error.Status != ResultStatus.Invalid)
        {
            throw new InvalidOperationException("Only validation errors can be added.");
        }

        errors.Add(error.ToResult());
    }

    private Dictionary<string, string[]> MergeValidationErrors()
    {
        var mergedValidationErrors = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var error in errors)
        {
            var validationErrors = error.ErrorDetails?.ValidationErrors;
            if (validationErrors is null)
            {
                continue;
            }

            foreach (var (field, messages) in validationErrors)
            {
                if (!mergedValidationErrors.TryGetValue(field, out var existingMessages))
                {
                    existingMessages = [];
                    mergedValidationErrors[field] = existingMessages;
                }

                existingMessages.AddRange(messages);
            }
        }

        var result = new Dictionary<string, string[]>(mergedValidationErrors.Count, StringComparer.Ordinal);
        foreach (var (field, messages) in mergedValidationErrors)
        {
            result[field] = [.. messages];
        }

        return result;
    }

    private static Dictionary<string, string[]> ToValidationDictionary(IReadOnlyDictionary<string, IReadOnlyList<string>> validationErrors)
    {
        var result = new Dictionary<string, string[]>(validationErrors.Count, StringComparer.Ordinal);
        foreach (var (field, messages) in validationErrors)
        {
            result[field] = [.. messages];
        }

        return result;
    }
}
