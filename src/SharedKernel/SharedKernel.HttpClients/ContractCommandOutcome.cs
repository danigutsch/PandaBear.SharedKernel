using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace SharedKernel.HttpClients;

/// <summary>
/// Creates contract command outcomes from HTTP responses.
/// </summary>
public static class ContractCommandOutcome
{
    /// <summary>
    /// Creates a successful command outcome.
    /// </summary>
    /// <param name="statusCode">The successful HTTP status code.</param>
    /// <param name="location">The created resource location, when present.</param>
    /// <returns>The successful command outcome.</returns>
    public static ContractCommandOutcomeDto Succeeded(HttpStatusCode statusCode, Uri? location)
    {
        return new ContractCommandOutcomeDto
        {
            Kind = ContractCommandOutcomeKind.Succeeded,
            StatusCode = statusCode,
            Location = location
        };
    }

    /// <summary>
    /// Creates a command outcome from an HTTP response.
    /// </summary>
    /// <param name="response">The HTTP response.</param>
    /// <param name="validationProblemJson">The source-generated validation problem JSON metadata.</param>
    /// <param name="ct">Cancellation token for reading the response body.</param>
    /// <returns>The command outcome.</returns>
    public static async Task<ContractCommandOutcomeDto> FromResponse(
        HttpResponseMessage response,
        JsonTypeInfo<ContractValidationProblemDto> validationProblemJson,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(validationProblemJson);

        return response.StatusCode switch
        {
            HttpStatusCode.Created => Succeeded(response.StatusCode, response.Headers.Location),
            HttpStatusCode.BadRequest => await ReadValidationProblem(response, validationProblemJson, ct).ConfigureAwait(false),
            _ => Status(MapStatusCode(response.StatusCode), response.StatusCode)
        };
    }

    /// <summary>
    /// Creates a command outcome for a status code.
    /// </summary>
    /// <param name="kind">The outcome kind.</param>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <param name="message">A non-PII diagnostic message.</param>
    /// <returns>The command outcome.</returns>
    public static ContractCommandOutcomeDto Status(ContractCommandOutcomeKind kind, HttpStatusCode statusCode, string? message = null)
    {
        return new ContractCommandOutcomeDto
        {
            Kind = kind,
            StatusCode = statusCode,
            Message = message
        };
    }

    private static async Task<ContractCommandOutcomeDto> ReadValidationProblem(
        HttpResponseMessage response,
        JsonTypeInfo<ContractValidationProblemDto> validationProblemJson,
        CancellationToken ct)
    {
        var content = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(content))
        {
            return Status(ContractCommandOutcomeKind.EmptyBody, response.StatusCode, "Validation problem response body was empty.");
        }

        try
        {
            var errors = JsonSerializer.Deserialize(content, validationProblemJson)?.Errors;
            return errors is null || errors.Count == 0
                ? Status(ContractCommandOutcomeKind.MalformedBody, response.StatusCode, "Validation problem response body did not contain errors.")
                : new ContractCommandOutcomeDto
                {
                    Kind = ContractCommandOutcomeKind.ValidationProblem,
                    StatusCode = response.StatusCode,
                    ValidationErrors = new Dictionary<string, string[]>(errors, StringComparer.Ordinal)
                };
        }
        catch (JsonException)
        {
            return Status(ContractCommandOutcomeKind.MalformedBody, response.StatusCode, "Validation problem response body was malformed.");
        }
    }

    private static ContractCommandOutcomeKind MapStatusCode(HttpStatusCode statusCode)
    {
        return statusCode switch
        {
            HttpStatusCode.NotFound => ContractCommandOutcomeKind.NotFound,
            HttpStatusCode.Unauthorized => ContractCommandOutcomeKind.Unauthorized,
            HttpStatusCode.Forbidden => ContractCommandOutcomeKind.Forbidden,
            HttpStatusCode.Conflict => ContractCommandOutcomeKind.Conflict,
            _ => ContractCommandOutcomeKind.UnexpectedStatus
        };
    }
}
