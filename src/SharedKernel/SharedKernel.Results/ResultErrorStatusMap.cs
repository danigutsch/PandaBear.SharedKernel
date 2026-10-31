namespace PandaBear.Results;

/// <summary>Maps a <see cref="ResultError"/> code to the failure status of a result.</summary>
internal static class ResultErrorStatusMap
{
    /// <summary>Gets the failure status for the error, validating that invalid errors carry field details.</summary>
    /// <param name="error">The error to map; must not be null.</param>
    /// <param name="paramName">The parameter name reported when the error is invalid.</param>
    /// <returns>The failure status.</returns>
    public static ResultStatus GetFailureStatus(ResultError error, string paramName)
    {
        if (string.Equals(error.Code, ResultErrorCodes.Invalid, StringComparison.Ordinal)
            && (error.ValidationErrors is null || error.ValidationErrors.Count == 0))
        {
            throw new ArgumentException(ResultInvariantMessages.ValidationErrorsMustContainFieldDetails, paramName);
        }

        return error.Code switch
        {
            ResultErrorCodes.Invalid => ResultStatus.Invalid,
            ResultErrorCodes.NotFound => ResultStatus.NotFound,
            ResultErrorCodes.Unauthorized => ResultStatus.Unauthorized,
            ResultErrorCodes.Forbidden => ResultStatus.Forbidden,
            ResultErrorCodes.Conflict => ResultStatus.Conflict,
            ResultErrorCodes.CriticalError => ResultStatus.CriticalError,
            ResultErrorCodes.Unavailable => ResultStatus.Unavailable,
            _ => ResultStatus.Error,
        };
    }
}
