using System.Net;

namespace PandaBear.HttpClients;

/// <summary>
/// Represents an HTTP 400 validation response, including a payload that cannot provide usable field errors.
/// </summary>
public sealed class ContractValidationException : HttpRequestException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContractValidationException"/> class.
    /// </summary>
    public ContractValidationException()
        : base("Validation failed", null, HttpStatusCode.BadRequest)
    {
        ValidationErrors = new Dictionary<string, string[]>(StringComparer.Ordinal);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ContractValidationException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public ContractValidationException(string message)
        : base(message, null, HttpStatusCode.BadRequest)
    {
        ValidationErrors = new Dictionary<string, string[]>(StringComparer.Ordinal);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ContractValidationException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public ContractValidationException(string message, Exception innerException)
        : base(message, innerException, HttpStatusCode.BadRequest)
    {
        ValidationErrors = new Dictionary<string, string[]>(StringComparer.Ordinal);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ContractValidationException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="validationErrors">The validation errors from the API.</param>
    /// <exception cref="ArgumentNullException">When <paramref name="validationErrors"/> is <see langword="null"/>.</exception>
    public ContractValidationException(string message, IReadOnlyDictionary<string, string[]> validationErrors)
        : base(message, null, HttpStatusCode.BadRequest)
    {
        ArgumentNullException.ThrowIfNull(validationErrors);

        ValidationErrors = new Dictionary<string, string[]>(validationErrors, StringComparer.Ordinal);
    }

    /// <summary>
    /// Gets validation errors by field or model key; empty when the response did not provide usable errors.
    /// </summary>
    public IReadOnlyDictionary<string, string[]> ValidationErrors
    {
        get;
    }

    /// <summary>
    /// Gets all validation error messages as a single formatted string.
    /// </summary>
    /// <returns>The formatted validation error messages.</returns>
    public string GetAllErrorMessages()
    {
        var messages = ValidationErrors
            .SelectMany(error => error.Value.Select(message => $"{error.Key}: {message}"))
            .ToList();

        return string.Join(Environment.NewLine, messages);
    }
}
