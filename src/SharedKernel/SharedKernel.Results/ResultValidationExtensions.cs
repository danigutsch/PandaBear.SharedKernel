namespace PandaBear.Results;

/// <summary>Reads validation errors from results and drops result values.</summary>
public static class ResultValidationExtensions
{
    /// <summary>Gets the validation errors of a result keyed by field name.</summary>
    /// <param name="result">The result.</param>
    /// <returns>The messages by field, or an empty dictionary when the result carries none.</returns>
    public static IReadOnlyDictionary<string, string[]> ValidationErrors(this Result result)
    {
        return result.ErrorDetails?.ValidationErrors?.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray())
            ?? new Dictionary<string, string[]>();
    }

    /// <summary>Drops the value of a result, keeping success or the failure.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="result">The result.</param>
    /// <returns>A success result, or the same failure without a value type.</returns>
    public static Result WithoutValue<T>(this Result<T> result)
        where T : notnull
    {
        return result.IsSuccess ? Result.Ok() : result.ConvertError();
    }
}
