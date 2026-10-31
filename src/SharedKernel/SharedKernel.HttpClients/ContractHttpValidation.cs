using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace PandaBear.HttpClients;

/// <summary>
/// Reads validation problem responses for contract-owned API clients.
/// </summary>
public static class ContractHttpValidation
{
    /// <summary>
    /// Accepts successful responses, extracts validation errors from HTTP 400, and throws for unsuccessful responses.
    /// </summary>
    /// <exception cref="ArgumentNullException">When <paramref name="response"/> or <paramref name="jsonTypeInfo"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// The caller retains ownership of the response. HTTP 400 JSON reads are limited to 1 MiB; this method does not send
    /// a request or create a timeout. Invalid validation payloads produce an exception without field errors.
    /// </remarks>
    /// <param name="response">The caller-owned HTTP response.</param>
    /// <param name="jsonTypeInfo">The validation problem JSON metadata.</param>
    /// <param name="ct">Cancellation token for reading the validation response body.</param>
    /// <returns>A task that completes for any successful HTTP status.</returns>
    /// <exception cref="ContractValidationException">The HTTP 400 response contains validation errors or an unusable validation payload.</exception>
    /// <exception cref="HttpRequestException">A non-400 response is unsuccessful, or validation response-body I/O fails.</exception>
    /// <exception cref="OperationCanceledException">Reading the validation response body is canceled.</exception>
    public static async Task EnsureSuccessOrThrowValidationException(
        HttpResponseMessage response,
        JsonTypeInfo<ContractValidationProblemDto> jsonTypeInfo,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(jsonTypeInfo);

        if (response.IsSuccessStatusCode)
        {
            return;
        }

        if (response.StatusCode != HttpStatusCode.BadRequest)
        {
            response.EnsureSuccessStatusCode();
            return;
        }

        if (!HttpOutcomeMapper.HasJsonContentType(response.Content))
        {
            throw new ContractValidationException("Validation problem response body was not JSON.");
        }

        var body = await HttpOutcomeMapper.ReadResponseBody(
            response.Content,
            HttpOutcomeMapper.MaximumJsonResponseBodyBytes,
            ct).ConfigureAwait(false);
        if (body.TransportFailed)
        {
            throw new HttpRequestException("HTTP transport failed.");
        }

        if (body.Malformed || string.IsNullOrWhiteSpace(body.Content))
        {
            throw new ContractValidationException("Validation problem response body was malformed.");
        }

        ContractValidationProblemDto? problem;
        try
        {
            problem = JsonSerializer.Deserialize(body.Content, jsonTypeInfo);
        }
        catch (JsonException exception)
        {
            throw new ContractValidationException("Validation problem response body was malformed.", exception);
        }

        if (problem?.Errors is null || problem.Errors.Count == 0)
        {
            throw new ContractValidationException("Validation problem response body did not contain errors.");
        }

        if (!problem.HasWellFormedErrors())
        {
            throw new ContractValidationException("Validation problem response body was malformed.");
        }

        var message = string.Join(Environment.NewLine, problem.Errors.SelectMany(error => error.Value));
        throw new ContractValidationException(message, problem.Errors);
    }
}
