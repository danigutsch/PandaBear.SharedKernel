using System.Buffers;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace SharedKernel.HttpClients;

/// <summary>
/// Creates contract command outcomes from HTTP requests and responses.
/// </summary>
public static class ContractCommandOutcome
{
    /// <summary>
    /// The activity source name used for bounded HTTP outcome telemetry.
    /// </summary>
    public const string ActivitySourceName = "SharedKernel.HttpClients.ContractOutcomes";

    private const string EmptyBodyMessage = "HTTP response body was empty.";
    private const string MalformedBodyMessage = "HTTP response body was malformed.";
    internal const int MaximumJsonResponseBodyBytes = 1024 * 1024;
    private const int ResponseReadBufferBytes = 16 * 1024;
    private const string TimedOutMessage = "HTTP request timed out.";
    private const string TransportFailureMessage = "HTTP transport failed.";
    private const string UnsupportedContentTypeMessage = "HTTP response content type was not JSON.";
    private const string ValidationEmptyBodyMessage = "Validation problem response body was empty.";
    private const string ValidationMalformedBodyMessage = "Validation problem response body was malformed.";
    private const string ValidationMissingErrorsMessage = "Validation problem response body did not contain errors.";
    private const string MethodTag = "http.request.method";
    private const string OutcomeKindTag = "http.outcome.kind";
    private const string StatusCodeTag = "http.response.status_code";
    private static readonly ActivitySource OutcomeActivitySource = new(ActivitySourceName);

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
    /// Creates a successful materialized command outcome.
    /// </summary>
    /// <typeparam name="TValue">The materialized response contract type.</typeparam>
    /// <param name="statusCode">The successful HTTP status code.</param>
    /// <param name="value">The materialized response value.</param>
    /// <param name="location">The created resource location, when present.</param>
    /// <returns>The successful command outcome.</returns>
    public static ContractCommandOutcomeDto<TValue> Succeeded<TValue>(HttpStatusCode statusCode, TValue value, Uri? location = null)
    {
        ArgumentNullException.ThrowIfNull(value);

        return new ContractCommandOutcomeDto<TValue>
        {
            Kind = ContractCommandOutcomeKind.Succeeded,
            StatusCode = statusCode,
            Value = value,
            Location = location
        };
    }

    /// <summary>
    /// Sends an HTTP request and maps its response to a command outcome.
    /// </summary>
    /// <remarks>
    /// This method takes ownership of <paramref name="request" /> and the received response and disposes both.
    /// Caller cancellation is rethrown; timeout and transport failures are returned as redacted outcomes.
    /// </remarks>
    /// <param name="httpClient">The HTTP client.</param>
    /// <param name="request">The request whose ownership is transferred to this method.</param>
    /// <param name="successStatusCode">The status code that represents success for this operation.</param>
    /// <param name="validationProblemJson">The source-generated validation problem JSON metadata.</param>
    /// <param name="ct">The caller cancellation token.</param>
    /// <returns>The command outcome.</returns>
    public static async Task<ContractCommandOutcomeDto> Send(
        HttpClient httpClient,
        HttpRequestMessage request,
        HttpStatusCode successStatusCode,
        JsonTypeInfo<ContractValidationProblemDto> validationProblemJson,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(validationProblemJson);

        using (request)
        using (var activity = StartActivity(request.Method))
        using (var timeoutSource = CreateResponseTimeoutSource(httpClient, ct))
        {
            var requestToken = timeoutSource?.Token ?? ct;
            ContractCommandOutcomeDto outcome;
            try
            {
                using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, requestToken).ConfigureAwait(false);
                outcome = await FromResponseCore(
                    response,
                    successStatusCode,
                    validationProblemJson,
                    GetResponseBodyLimit(httpClient),
                    requestToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException exception) when (ct.IsCancellationRequested)
            {
                throw new OperationCanceledException(exception.Message, exception, ct);
            }
            catch (Exception exception) when (IsTimeout(exception))
            {
                outcome = Failure(ContractCommandOutcomeKind.TimedOut, TimedOutMessage);
            }
            catch (Exception exception) when (IsTransportFailure(exception))
            {
                outcome = Failure(ContractCommandOutcomeKind.TransportFailure, TransportFailureMessage);
            }

            RecordOutcome(activity, outcome.Kind, outcome.StatusCode);
            return outcome;
        }
    }

    /// <summary>
    /// Sends an HTTP request and maps its materialized JSON response to a command outcome.
    /// </summary>
    /// <remarks>
    /// This method takes ownership of <paramref name="request" /> and the received response and disposes both.
    /// Caller cancellation is rethrown; timeout and transport failures are returned as redacted outcomes.
    /// </remarks>
    /// <typeparam name="TValue">The materialized response contract type.</typeparam>
    /// <param name="httpClient">The HTTP client.</param>
    /// <param name="request">The request whose ownership is transferred to this method.</param>
    /// <param name="successStatusCode">The status code that represents success for this operation.</param>
    /// <param name="valueJson">The source-generated response JSON metadata.</param>
    /// <param name="validationProblemJson">The source-generated validation problem JSON metadata.</param>
    /// <param name="ct">The caller cancellation token.</param>
    /// <returns>The materialized command outcome.</returns>
    public static async Task<ContractCommandOutcomeDto<TValue>> Send<TValue>(
        HttpClient httpClient,
        HttpRequestMessage request,
        HttpStatusCode successStatusCode,
        JsonTypeInfo<TValue> valueJson,
        JsonTypeInfo<ContractValidationProblemDto> validationProblemJson,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(valueJson);
        ArgumentNullException.ThrowIfNull(validationProblemJson);

        using (request)
        using (var activity = StartActivity(request.Method))
        using (var timeoutSource = CreateResponseTimeoutSource(httpClient, ct))
        {
            var requestToken = timeoutSource?.Token ?? ct;
            ContractCommandOutcomeDto<TValue> outcome;
            try
            {
                using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, requestToken).ConfigureAwait(false);
                outcome = await FromResponseCore(
                    response,
                    successStatusCode,
                    valueJson,
                    validationProblemJson,
                    GetResponseBodyLimit(httpClient),
                    requestToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException exception) when (ct.IsCancellationRequested)
            {
                throw new OperationCanceledException(exception.Message, exception, ct);
            }
            catch (Exception exception) when (IsTimeout(exception))
            {
                outcome = Failure<TValue>(ContractCommandOutcomeKind.TimedOut, TimedOutMessage);
            }
            catch (Exception exception) when (IsTransportFailure(exception))
            {
                outcome = Failure<TValue>(ContractCommandOutcomeKind.TransportFailure, TransportFailureMessage);
            }

            RecordOutcome(activity, outcome.Kind, outcome.StatusCode);
            return outcome;
        }
    }

    /// <summary>
    /// Creates a command outcome from an HTTP response.
    /// </summary>
    /// <remarks>This method takes ownership of <paramref name="response" /> and disposes it.</remarks>
    /// <param name="response">The HTTP response whose ownership is transferred to this method.</param>
    /// <param name="validationProblemJson">The source-generated validation problem JSON metadata.</param>
    /// <param name="ct">Cancellation token for reading the response body.</param>
    /// <returns>The command outcome.</returns>
    public static async Task<ContractCommandOutcomeDto> FromResponse(
        HttpResponseMessage response,
        JsonTypeInfo<ContractValidationProblemDto> validationProblemJson,
        CancellationToken ct)
    {
        return await FromResponse(response, HttpStatusCode.Created, validationProblemJson, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a command outcome from an HTTP response for an operation-specific success status.
    /// </summary>
    /// <remarks>This method takes ownership of <paramref name="response" /> and disposes it.</remarks>
    /// <param name="response">The HTTP response whose ownership is transferred to this method.</param>
    /// <param name="successStatusCode">The status code that represents success for this operation.</param>
    /// <param name="validationProblemJson">The source-generated validation problem JSON metadata.</param>
    /// <param name="ct">Cancellation token for reading the response body.</param>
    /// <returns>The command outcome.</returns>
    public static async Task<ContractCommandOutcomeDto> FromResponse(
        HttpResponseMessage response,
        HttpStatusCode successStatusCode,
        JsonTypeInfo<ContractValidationProblemDto> validationProblemJson,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(validationProblemJson);

        using (response)
        {
            return await FromResponseCore(
                response,
                successStatusCode,
                validationProblemJson,
                MaximumJsonResponseBodyBytes,
                ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Creates a materialized command outcome from an HTTP response.
    /// </summary>
    /// <remarks>This method takes ownership of <paramref name="response" /> and disposes it.</remarks>
    /// <typeparam name="TValue">The materialized response contract type.</typeparam>
    /// <param name="response">The HTTP response whose ownership is transferred to this method.</param>
    /// <param name="successStatusCode">The status code that represents success for this operation.</param>
    /// <param name="valueJson">The source-generated response JSON metadata.</param>
    /// <param name="validationProblemJson">The source-generated validation problem JSON metadata.</param>
    /// <param name="ct">Cancellation token for reading the response body.</param>
    /// <returns>The materialized command outcome.</returns>
    public static async Task<ContractCommandOutcomeDto<TValue>> FromResponse<TValue>(
        HttpResponseMessage response,
        HttpStatusCode successStatusCode,
        JsonTypeInfo<TValue> valueJson,
        JsonTypeInfo<ContractValidationProblemDto> validationProblemJson,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(valueJson);
        ArgumentNullException.ThrowIfNull(validationProblemJson);

        using (response)
        {
            return await FromResponseCore(
                response,
                successStatusCode,
                valueJson,
                validationProblemJson,
                MaximumJsonResponseBodyBytes,
                ct).ConfigureAwait(false);
        }
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

    /// <summary>
    /// Creates a materialized command outcome for a status code.
    /// </summary>
    /// <typeparam name="TValue">The materialized response contract type.</typeparam>
    /// <param name="kind">The outcome kind.</param>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <param name="message">A non-PII diagnostic message.</param>
    /// <returns>The materialized command outcome.</returns>
    public static ContractCommandOutcomeDto<TValue> Status<TValue>(ContractCommandOutcomeKind kind, HttpStatusCode statusCode, string? message = null)
    {
        return new ContractCommandOutcomeDto<TValue>
        {
            Kind = kind,
            StatusCode = statusCode,
            Message = message
        };
    }

    private static async Task<ContractCommandOutcomeDto> FromResponseCore(
        HttpResponseMessage response,
        HttpStatusCode successStatusCode,
        JsonTypeInfo<ContractValidationProblemDto> validationProblemJson,
        int responseBodyLimit,
        CancellationToken ct)
    {
        if (response.StatusCode == successStatusCode)
        {
            return Succeeded(response.StatusCode, response.Headers.Location);
        }

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            return await ReadValidationProblem(response, validationProblemJson, responseBodyLimit, ct).ConfigureAwait(false);
        }

        return Status(MapStatusCode(response.StatusCode), response.StatusCode);
    }

    private static async Task<ContractCommandOutcomeDto<TValue>> FromResponseCore<TValue>(
        HttpResponseMessage response,
        HttpStatusCode successStatusCode,
        JsonTypeInfo<TValue> valueJson,
        JsonTypeInfo<ContractValidationProblemDto> validationProblemJson,
        int responseBodyLimit,
        CancellationToken ct)
    {
        if (response.StatusCode == successStatusCode)
        {
            return await ReadValue(response, valueJson, responseBodyLimit, ct).ConfigureAwait(false);
        }

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var validationOutcome = await ReadValidationProblem(
                response,
                validationProblemJson,
                responseBodyLimit,
                ct).ConfigureAwait(false);
            return FromOutcome<TValue>(validationOutcome);
        }

        return Status<TValue>(MapStatusCode(response.StatusCode), response.StatusCode);
    }

    private static async Task<ContractCommandOutcomeDto<TValue>> ReadValue<TValue>(
        HttpResponseMessage response,
        JsonTypeInfo<TValue> valueJson,
        int responseBodyLimit,
        CancellationToken ct)
    {
        if (response.Content.Headers.ContentLength == 0)
        {
            return Status<TValue>(ContractCommandOutcomeKind.EmptyBody, response.StatusCode, EmptyBodyMessage);
        }

        if (!HasJsonContentType(response.Content))
        {
            return Status<TValue>(ContractCommandOutcomeKind.UnsupportedContentType, response.StatusCode, UnsupportedContentTypeMessage);
        }

        var body = await ReadResponseBody(response.Content, responseBodyLimit, ct).ConfigureAwait(false);
        if (body.TransportFailed)
        {
            return Failure<TValue>(ContractCommandOutcomeKind.TransportFailure, TransportFailureMessage);
        }

        if (body.Malformed)
        {
            return Status<TValue>(ContractCommandOutcomeKind.MalformedBody, response.StatusCode, MalformedBodyMessage);
        }

        if (string.IsNullOrWhiteSpace(body.Content))
        {
            return Status<TValue>(ContractCommandOutcomeKind.EmptyBody, response.StatusCode, EmptyBodyMessage);
        }

        try
        {
            var value = JsonSerializer.Deserialize(body.Content, valueJson);
            return value is null
                ? Status<TValue>(ContractCommandOutcomeKind.MalformedBody, response.StatusCode, MalformedBodyMessage)
                : Succeeded(response.StatusCode, value, response.Headers.Location);
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException)
        {
            return Status<TValue>(ContractCommandOutcomeKind.MalformedBody, response.StatusCode, MalformedBodyMessage);
        }
    }

    private static async Task<ContractCommandOutcomeDto> ReadValidationProblem(
        HttpResponseMessage response,
        JsonTypeInfo<ContractValidationProblemDto> validationProblemJson,
        int responseBodyLimit,
        CancellationToken ct)
    {
        if (response.Content.Headers.ContentLength == 0)
        {
            return Status(ContractCommandOutcomeKind.EmptyBody, response.StatusCode, ValidationEmptyBodyMessage);
        }

        if (!HasJsonContentType(response.Content))
        {
            return Status(ContractCommandOutcomeKind.UnsupportedContentType, response.StatusCode, UnsupportedContentTypeMessage);
        }

        var body = await ReadResponseBody(response.Content, responseBodyLimit, ct).ConfigureAwait(false);
        if (body.TransportFailed)
        {
            return Failure(ContractCommandOutcomeKind.TransportFailure, TransportFailureMessage);
        }

        if (body.Malformed)
        {
            return Status(ContractCommandOutcomeKind.MalformedBody, response.StatusCode, ValidationMalformedBodyMessage);
        }

        if (string.IsNullOrWhiteSpace(body.Content))
        {
            return Status(ContractCommandOutcomeKind.EmptyBody, response.StatusCode, ValidationEmptyBodyMessage);
        }

        try
        {
            var problem = JsonSerializer.Deserialize(body.Content, validationProblemJson);
            if (problem?.Errors is null or { Count: 0 })
            {
                return Status(ContractCommandOutcomeKind.MalformedBody, response.StatusCode, ValidationMissingErrorsMessage);
            }

            if (!problem.HasWellFormedErrors())
            {
                return Status(ContractCommandOutcomeKind.MalformedBody, response.StatusCode, ValidationMalformedBodyMessage);
            }

            return new ContractCommandOutcomeDto
            {
                Kind = ContractCommandOutcomeKind.ValidationProblem,
                StatusCode = response.StatusCode,
                ValidationErrors = new Dictionary<string, string[]>(problem.Errors, StringComparer.Ordinal)
            };
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException)
        {
            return Status(ContractCommandOutcomeKind.MalformedBody, response.StatusCode, ValidationMalformedBodyMessage);
        }
    }

    internal static async Task<(string? Content, bool Malformed, bool TransportFailed)> ReadResponseBody(
        HttpContent content,
        int responseBodyLimit,
        CancellationToken ct)
    {
        if (content.Headers.ContentLength > responseBodyLimit)
        {
            return (null, Malformed: true, TransportFailed: false);
        }

        try
        {
            using var source = await content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            var contentLength = content.Headers.ContentLength;
            var initialCapacity = contentLength.HasValue && contentLength.Value > 0 && contentLength.Value <= responseBodyLimit
                ? (int)contentLength.Value
                : 0;
            using var buffer = new MemoryStream(initialCapacity);
            var rentedBuffer = ArrayPool<byte>.Shared.Rent(ResponseReadBufferBytes);
            try
            {
                while (true)
                {
                    var remainingBytes = responseBodyLimit - (int)buffer.Length;
                    var readLength = Math.Min(rentedBuffer.Length, remainingBytes + 1);
                    var bytesRead = await source.ReadAsync(
                        rentedBuffer.AsMemory(0, readLength),
                        ct).ConfigureAwait(false);
                    if (bytesRead == 0)
                    {
                        break;
                    }

                    if (bytesRead > remainingBytes)
                    {
                        return (null, Malformed: true, TransportFailed: false);
                    }

                    await buffer.WriteAsync(rentedBuffer.AsMemory(0, bytesRead), ct).ConfigureAwait(false);
                }

                buffer.Position = 0;
                using var reader = new StreamReader(
                    buffer,
                    GetResponseEncoding(content),
                    detectEncodingFromByteOrderMarks: true,
                    bufferSize: 1024,
                    leaveOpen: true);
                var body = await reader.ReadToEndAsync(ct).ConfigureAwait(false);
                return (body, Malformed: false, TransportFailed: false);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rentedBuffer);
            }
        }
        catch (Exception exception) when (exception is IOException or HttpRequestException)
        {
            return (null, Malformed: false, TransportFailed: true);
        }
        catch (ArgumentException)
        {
            return (null, Malformed: true, TransportFailed: false);
        }
    }

    private static ContractCommandOutcomeDto Failure(ContractCommandOutcomeKind kind, string message)
    {
        return new ContractCommandOutcomeDto
        {
            Kind = kind,
            Message = message
        };
    }

    private static ContractCommandOutcomeDto<TValue> Failure<TValue>(ContractCommandOutcomeKind kind, string message)
    {
        return new ContractCommandOutcomeDto<TValue>
        {
            Kind = kind,
            Message = message
        };
    }

    private static ContractCommandOutcomeDto<TValue> FromOutcome<TValue>(ContractCommandOutcomeDto outcome)
    {
        return new ContractCommandOutcomeDto<TValue>
        {
            Kind = outcome.Kind,
            StatusCode = outcome.StatusCode,
            Location = outcome.Location,
            ValidationErrors = outcome.ValidationErrors,
            Message = outcome.Message
        };
    }

    private static Activity? StartActivity(HttpMethod method)
    {
        var activity = OutcomeActivitySource.StartActivity("http.client.outcome", ActivityKind.Internal);
        activity?.SetTag(MethodTag, method.Method);
        return activity;
    }

    private static void RecordOutcome(Activity? activity, ContractCommandOutcomeKind kind, HttpStatusCode? statusCode)
    {
        if (statusCode is not null)
        {
            activity?.SetTag(StatusCodeTag, (int)statusCode.Value);
        }

        activity?.SetTag(OutcomeKindTag, kind.ToString());
    }

    internal static bool HasJsonContentType(HttpContent content)
    {
        var mediaType = content.Headers.ContentType?.MediaType;
        return string.Equals(mediaType, "application/json", StringComparison.OrdinalIgnoreCase)
            || (mediaType?.EndsWith("+json", StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private static int GetResponseBodyLimit(HttpClient httpClient)
    {
        return (int)Math.Min(httpClient.MaxResponseContentBufferSize, MaximumJsonResponseBodyBytes);
    }

    private static CancellationTokenSource? CreateResponseTimeoutSource(HttpClient httpClient, CancellationToken ct)
    {
        if (httpClient.Timeout == Timeout.InfiniteTimeSpan)
        {
            return null;
        }

        var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutSource.CancelAfter(httpClient.Timeout);
        return timeoutSource;
    }

    private static Encoding GetResponseEncoding(HttpContent content)
    {
        var charset = content.Headers.ContentType?.CharSet;
        return string.IsNullOrWhiteSpace(charset)
            ? Encoding.UTF8
            : Encoding.GetEncoding(charset.Trim('"'));
    }

    private static bool IsTimeout(Exception exception)
    {
        return exception is OperationCanceledException or TimeoutException or TimeoutRejectedException;
    }

    private static bool IsTransportFailure(Exception exception)
    {
        return exception is HttpRequestException or BrokenCircuitException;
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
