using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Xunit.Sdk;
using static PandaBear.Testing.Assertions.UserMessageAssertions;

namespace PandaBear.Testing.Http;

/// <summary>Provides assertions for successful HTTP responses: statuses, JSON bodies, created locations, files, and cache headers.</summary>
/// <remarks>
/// A failed assertion throws an <see cref="XunitException"/> that names the expected and actual values. A failure about the status or body quotes at most
/// 200 characters of a text body, with control characters flattened to spaces, and only the size of a non-text body. Header values of credential headers such
/// as <c>Set-Cookie</c> are never quoted. Responses remain owned by the caller, and bodies stay readable after an assertion.
/// </remarks>
public static class HttpResponseSuccessAssertionExtensions
{
    private const string JsonMediaType = "application/json";

    private static readonly HashSet<string> CredentialHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Authorization", "Proxy-Authorization", "Cookie", "Set-Cookie", "WWW-Authenticate", "Proxy-Authenticate"
    };

    /// <summary>Verifies the HTTP status code.</summary>
    /// <param name="actual">The response, which remains owned by the caller.</param>
    /// <param name="expectedStatus">The expected HTTP status.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The response, for further assertions.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="actual"/> is null.</exception>
    /// <exception cref="XunitException">Thrown when the status differs; the message quotes a truncated excerpt of the body.</exception>
    /// <exception cref="OperationCanceledException">The supplied cancellation token is canceled.</exception>
    public static async Task<HttpResponseMessage> ShouldHaveStatus(this HttpResponseMessage actual, HttpStatusCode expectedStatus,
        CancellationToken cancellationToken = default)
    {
        await EnsureStatus(actual, expectedStatus, cancellationToken).ConfigureAwait(false);
        return actual;
    }

    /// <summary>Verifies the HTTP status code.</summary>
    /// <param name="actual">The response, which remains owned by the caller.</param>
    /// <param name="expectedStatus">The expected HTTP status.</param>
    /// <param name="userMessage">Why the test expects the assertion to hold; the failure message starts with it.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The response, for further assertions.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="actual"/> is null. Also thrown when <paramref name="userMessage"/> is null.</exception>
    /// <exception cref="OperationCanceledException">The supplied cancellation token is canceled.</exception>
    public static Task<HttpResponseMessage> ShouldHaveStatus(this HttpResponseMessage actual, HttpStatusCode expectedStatus,
        string userMessage, CancellationToken cancellationToken = default) =>
        WithUserMessage(userMessage, () => actual.ShouldHaveStatus(expectedStatus, cancellationToken));

    /// <summary>Verifies a 200 OK status.</summary>
    /// <param name="actual">The response, which remains owned by the caller.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The response, for further assertions.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="actual"/> is null.</exception>
    /// <exception cref="XunitException">Thrown when the status is not 200; the message quotes a truncated excerpt of the body.</exception>
    /// <exception cref="OperationCanceledException">The supplied cancellation token is canceled.</exception>
    public static Task<HttpResponseMessage> ShouldBeOk(this HttpResponseMessage actual, CancellationToken cancellationToken = default) =>
        actual.ShouldHaveStatus(HttpStatusCode.OK, cancellationToken);

    /// <summary>Verifies a 200 OK status.</summary>
    /// <param name="actual">The response, which remains owned by the caller.</param>
    /// <param name="userMessage">Why the test expects the assertion to hold; the failure message starts with it.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The response, for further assertions.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="actual"/> is null. Also thrown when <paramref name="userMessage"/> is null.</exception>
    /// <exception cref="OperationCanceledException">The supplied cancellation token is canceled.</exception>
    public static Task<HttpResponseMessage> ShouldBeOk(this HttpResponseMessage actual, string userMessage, CancellationToken cancellationToken = default) =>
        WithUserMessage(userMessage, () => actual.ShouldBeOk(cancellationToken));

    /// <summary>Verifies a 200 OK <c>application/json</c> response and deserializes its body with <see cref="JsonSerializerOptions.Web"/>.</summary>
    /// <typeparam name="T">The body type.</typeparam>
    /// <param name="actual">The response, which remains owned by the caller.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The deserialized body.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="actual"/> is null.</exception>
    /// <exception cref="XunitException">Thrown when the status or media type differs, or the body is empty, is not valid <typeparamref name="T"/> JSON, or is JSON null.</exception>
    /// <exception cref="OperationCanceledException">The supplied cancellation token is canceled.</exception>
    public static Task<T> ShouldBeOk<T>(this HttpResponseMessage actual, CancellationToken cancellationToken = default) =>
        actual.ShouldBeOk<T>(JsonSerializerOptions.Web, cancellationToken);

    /// <summary>Verifies a 200 OK <c>application/json</c> response and deserializes its body with <see cref="JsonSerializerOptions.Web"/>.</summary>
    /// <typeparam name="T">The body type.</typeparam>
    /// <param name="actual">The response, which remains owned by the caller.</param>
    /// <param name="userMessage">Why the test expects the assertion to hold; the failure message starts with it.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The deserialized body.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="actual"/> is null. Also thrown when <paramref name="userMessage"/> is null.</exception>
    /// <exception cref="OperationCanceledException">The supplied cancellation token is canceled.</exception>
    public static Task<T> ShouldBeOk<T>(this HttpResponseMessage actual, string userMessage, CancellationToken cancellationToken = default) =>
        WithUserMessage(userMessage, () => actual.ShouldBeOk<T>(cancellationToken));

    /// <summary>Verifies a 200 OK <c>application/json</c> response and deserializes its body with the supplied options.</summary>
    /// <typeparam name="T">The body type.</typeparam>
    /// <param name="actual">The response, which remains owned by the caller.</param>
    /// <param name="options">The serializer options, which must be able to resolve <typeparamref name="T"/>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The deserialized body.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="actual"/> or <paramref name="options"/> is null.</exception>
    /// <exception cref="XunitException">Thrown when the status or media type differs, or the body is empty, is not valid <typeparamref name="T"/> JSON, or is JSON null.</exception>
    /// <exception cref="OperationCanceledException">The supplied cancellation token is canceled.</exception>
    public static async Task<T> ShouldBeOk<T>(this HttpResponseMessage actual, JsonSerializerOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actual);
        ArgumentNullException.ThrowIfNull(options);
        return await ReadJson(actual, HttpStatusCode.OK, false, bytes => JsonSerializer.Deserialize<T>(bytes, options), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Verifies a 200 OK <c>application/json</c> response and deserializes its body with the supplied options.</summary>
    /// <typeparam name="T">The body type.</typeparam>
    /// <param name="actual">The response, which remains owned by the caller.</param>
    /// <param name="options">The serializer options, which must be able to resolve <typeparamref name="T"/>.</param>
    /// <param name="userMessage">Why the test expects the assertion to hold; the failure message starts with it.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The deserialized body.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="actual"/> or <paramref name="options"/> is null. Also thrown when <paramref name="userMessage"/> is null.</exception>
    /// <exception cref="OperationCanceledException">The supplied cancellation token is canceled.</exception>
    public static Task<T> ShouldBeOk<T>(this HttpResponseMessage actual, JsonSerializerOptions options, string userMessage,
        CancellationToken cancellationToken = default) =>
        WithUserMessage(userMessage, () => actual.ShouldBeOk<T>(options, cancellationToken));

    /// <summary>Verifies a 200 OK <c>application/json</c> response and deserializes its body with source-generated metadata, without reflection.</summary>
    /// <typeparam name="T">The body type.</typeparam>
    /// <param name="actual">The response, which remains owned by the caller.</param>
    /// <param name="typeInfo">The metadata of <typeparamref name="T"/>, such as a property of a <see cref="System.Text.Json.Serialization.JsonSerializerContext"/>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The deserialized body.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="actual"/> or <paramref name="typeInfo"/> is null.</exception>
    /// <exception cref="XunitException">Thrown when the status or media type differs, or the body is empty, is not valid <typeparamref name="T"/> JSON, or is JSON null.</exception>
    /// <exception cref="OperationCanceledException">The supplied cancellation token is canceled.</exception>
    public static async Task<T> ShouldBeOk<T>(this HttpResponseMessage actual, JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actual);
        ArgumentNullException.ThrowIfNull(typeInfo);
        return await ReadJson(actual, HttpStatusCode.OK, false, bytes => JsonSerializer.Deserialize(bytes, typeInfo), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Verifies a 200 OK <c>application/json</c> response and deserializes its body with source-generated metadata, without reflection.</summary>
    /// <typeparam name="T">The body type.</typeparam>
    /// <param name="actual">The response, which remains owned by the caller.</param>
    /// <param name="typeInfo">The metadata of <typeparamref name="T"/>, such as a property of a <see cref="System.Text.Json.Serialization.JsonSerializerContext"/>.</param>
    /// <param name="userMessage">Why the test expects the assertion to hold; the failure message starts with it.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The deserialized body.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="actual"/> or <paramref name="typeInfo"/> is null. Also thrown when <paramref name="userMessage"/> is null.</exception>
    /// <exception cref="OperationCanceledException">The supplied cancellation token is canceled.</exception>
    public static Task<T> ShouldBeOk<T>(this HttpResponseMessage actual, JsonTypeInfo<T> typeInfo, string userMessage,
        CancellationToken cancellationToken = default) =>
        WithUserMessage(userMessage, () => actual.ShouldBeOk(typeInfo, cancellationToken));

    /// <summary>Verifies a 201 Created status with a <c>Location</c> header.</summary>
    /// <param name="actual">The response, which remains owned by the caller.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The response, for <see cref="ShouldHaveLocation(HttpResponseMessage, string)"/> and further assertions.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="actual"/> is null.</exception>
    /// <exception cref="XunitException">Thrown when the status is not 201 or the <c>Location</c> header is missing.</exception>
    /// <exception cref="OperationCanceledException">The supplied cancellation token is canceled.</exception>
    public static async Task<HttpResponseMessage> ShouldBeCreated(this HttpResponseMessage actual, CancellationToken cancellationToken = default)
    {
        await EnsureStatus(actual, HttpStatusCode.Created, cancellationToken).ConfigureAwait(false);
        RequireLocation(actual);
        return actual;
    }

    /// <summary>Verifies a 201 Created status with a <c>Location</c> header.</summary>
    /// <param name="actual">The response, which remains owned by the caller.</param>
    /// <param name="userMessage">Why the test expects the assertion to hold; the failure message starts with it.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The response, for <see cref="ShouldHaveLocation(HttpResponseMessage, string)"/> and further assertions.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="actual"/> is null. Also thrown when <paramref name="userMessage"/> is null.</exception>
    /// <exception cref="OperationCanceledException">The supplied cancellation token is canceled.</exception>
    public static Task<HttpResponseMessage> ShouldBeCreated(this HttpResponseMessage actual, string userMessage, CancellationToken cancellationToken = default) =>
        WithUserMessage(userMessage, () => actual.ShouldBeCreated(cancellationToken));

    /// <summary>Verifies a 201 Created <c>application/json</c> response with a <c>Location</c> header and deserializes its body with <see cref="JsonSerializerOptions.Web"/>.</summary>
    /// <typeparam name="T">The body type.</typeparam>
    /// <param name="actual">The response, which remains owned by the caller.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The deserialized body; check the location afterwards with <see cref="ShouldHaveLocation(HttpResponseMessage, string)"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="actual"/> is null.</exception>
    /// <exception cref="XunitException">Thrown when the status differs, the <c>Location</c> header is missing, the media type differs, or the body is empty, is not valid <typeparamref name="T"/> JSON, or is JSON null.</exception>
    /// <exception cref="OperationCanceledException">The supplied cancellation token is canceled.</exception>
    public static Task<T> ShouldBeCreated<T>(this HttpResponseMessage actual, CancellationToken cancellationToken = default) =>
        actual.ShouldBeCreated<T>(JsonSerializerOptions.Web, cancellationToken);

    /// <summary>Verifies a 201 Created <c>application/json</c> response with a <c>Location</c> header and deserializes its body with <see cref="JsonSerializerOptions.Web"/>.</summary>
    /// <typeparam name="T">The body type.</typeparam>
    /// <param name="actual">The response, which remains owned by the caller.</param>
    /// <param name="userMessage">Why the test expects the assertion to hold; the failure message starts with it.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The deserialized body; check the location afterwards with <see cref="ShouldHaveLocation(HttpResponseMessage, string)"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="actual"/> is null. Also thrown when <paramref name="userMessage"/> is null.</exception>
    /// <exception cref="OperationCanceledException">The supplied cancellation token is canceled.</exception>
    public static Task<T> ShouldBeCreated<T>(this HttpResponseMessage actual, string userMessage, CancellationToken cancellationToken = default) =>
        WithUserMessage(userMessage, () => actual.ShouldBeCreated<T>(cancellationToken));

    /// <summary>Verifies a 201 Created <c>application/json</c> response with a <c>Location</c> header and deserializes its body with the supplied options.</summary>
    /// <typeparam name="T">The body type.</typeparam>
    /// <param name="actual">The response, which remains owned by the caller.</param>
    /// <param name="options">The serializer options, which must be able to resolve <typeparamref name="T"/>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The deserialized body; check the location afterwards with <see cref="ShouldHaveLocation(HttpResponseMessage, string)"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="actual"/> or <paramref name="options"/> is null.</exception>
    /// <exception cref="XunitException">Thrown when the status differs, the <c>Location</c> header is missing, the media type differs, or the body is empty, is not valid <typeparamref name="T"/> JSON, or is JSON null.</exception>
    /// <exception cref="OperationCanceledException">The supplied cancellation token is canceled.</exception>
    public static async Task<T> ShouldBeCreated<T>(this HttpResponseMessage actual, JsonSerializerOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actual);
        ArgumentNullException.ThrowIfNull(options);
        return await ReadJson(actual, HttpStatusCode.Created, true, bytes => JsonSerializer.Deserialize<T>(bytes, options), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Verifies a 201 Created <c>application/json</c> response with a <c>Location</c> header and deserializes its body with the supplied options.</summary>
    /// <typeparam name="T">The body type.</typeparam>
    /// <param name="actual">The response, which remains owned by the caller.</param>
    /// <param name="options">The serializer options, which must be able to resolve <typeparamref name="T"/>.</param>
    /// <param name="userMessage">Why the test expects the assertion to hold; the failure message starts with it.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The deserialized body; check the location afterwards with <see cref="ShouldHaveLocation(HttpResponseMessage, string)"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="actual"/> or <paramref name="options"/> is null. Also thrown when <paramref name="userMessage"/> is null.</exception>
    /// <exception cref="OperationCanceledException">The supplied cancellation token is canceled.</exception>
    public static Task<T> ShouldBeCreated<T>(this HttpResponseMessage actual, JsonSerializerOptions options, string userMessage,
        CancellationToken cancellationToken = default) =>
        WithUserMessage(userMessage, () => actual.ShouldBeCreated<T>(options, cancellationToken));

    /// <summary>Verifies a 201 Created <c>application/json</c> response with a <c>Location</c> header and deserializes its body with source-generated metadata, without reflection.</summary>
    /// <typeparam name="T">The body type.</typeparam>
    /// <param name="actual">The response, which remains owned by the caller.</param>
    /// <param name="typeInfo">The metadata of <typeparamref name="T"/>, such as a property of a <see cref="System.Text.Json.Serialization.JsonSerializerContext"/>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The deserialized body; check the location afterwards with <see cref="ShouldHaveLocation(HttpResponseMessage, string)"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="actual"/> or <paramref name="typeInfo"/> is null.</exception>
    /// <exception cref="XunitException">Thrown when the status differs, the <c>Location</c> header is missing, the media type differs, or the body is empty, is not valid <typeparamref name="T"/> JSON, or is JSON null.</exception>
    /// <exception cref="OperationCanceledException">The supplied cancellation token is canceled.</exception>
    public static async Task<T> ShouldBeCreated<T>(this HttpResponseMessage actual, JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actual);
        ArgumentNullException.ThrowIfNull(typeInfo);
        return await ReadJson(actual, HttpStatusCode.Created, true, bytes => JsonSerializer.Deserialize(bytes, typeInfo), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Verifies a 201 Created <c>application/json</c> response with a <c>Location</c> header and deserializes its body with source-generated metadata, without reflection.</summary>
    /// <typeparam name="T">The body type.</typeparam>
    /// <param name="actual">The response, which remains owned by the caller.</param>
    /// <param name="typeInfo">The metadata of <typeparamref name="T"/>, such as a property of a <see cref="System.Text.Json.Serialization.JsonSerializerContext"/>.</param>
    /// <param name="userMessage">Why the test expects the assertion to hold; the failure message starts with it.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The deserialized body; check the location afterwards with <see cref="ShouldHaveLocation(HttpResponseMessage, string)"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="actual"/> or <paramref name="typeInfo"/> is null. Also thrown when <paramref name="userMessage"/> is null.</exception>
    /// <exception cref="OperationCanceledException">The supplied cancellation token is canceled.</exception>
    public static Task<T> ShouldBeCreated<T>(this HttpResponseMessage actual, JsonTypeInfo<T> typeInfo, string userMessage,
        CancellationToken cancellationToken = default) =>
        WithUserMessage(userMessage, () => actual.ShouldBeCreated(typeInfo, cancellationToken));

    /// <summary>Verifies that the <c>Location</c> header exists and that its path starts with the prefix.</summary>
    /// <param name="actual">The response.</param>
    /// <param name="expectedPathPrefix">The case-sensitive path prefix, such as <c>/api/tours/</c>; the query and fragment are not compared.</param>
    /// <returns>The <c>Location</c> URI, absolute or relative as the server sent it, so a test can read the created identifier.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="actual"/> or <paramref name="expectedPathPrefix"/> is null.</exception>
    /// <exception cref="XunitException">Thrown when the header is missing or its path does not start with the prefix; the message omits the query and fragment.</exception>
    public static Uri ShouldHaveLocation(this HttpResponseMessage actual, string expectedPathPrefix)
    {
        ArgumentNullException.ThrowIfNull(actual);
        ArgumentNullException.ThrowIfNull(expectedPathPrefix);
        var location = RequireLocation(actual);
        var text = location.IsAbsoluteUri ? location.AbsolutePath : location.OriginalString;
        var end = text.IndexOfAny(['?', '#']);
        var path = end < 0 ? text : text[..end];
        if (!path.StartsWith(expectedPathPrefix, StringComparison.Ordinal))
        {
            throw new XunitException($"Expected the Location path to start with '{expectedPathPrefix}' but was '{path}'.");
        }

        return location;
    }

    /// <summary>Verifies that the <c>Location</c> header exists and that its path starts with the prefix.</summary>
    /// <param name="actual">The response.</param>
    /// <param name="expectedPathPrefix">The case-sensitive path prefix, such as <c>/api/tours/</c>; the query and fragment are not compared.</param>
    /// <param name="userMessage">Why the test expects the assertion to hold; the failure message starts with it.</param>
    /// <returns>The <c>Location</c> URI, absolute or relative as the server sent it, so a test can read the created identifier.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="actual"/> or <paramref name="expectedPathPrefix"/> is null. Also thrown when <paramref name="userMessage"/> is null.</exception>
    public static Uri ShouldHaveLocation(this HttpResponseMessage actual, string expectedPathPrefix, string userMessage) =>
        WithUserMessage(userMessage, () => actual.ShouldHaveLocation(expectedPathPrefix));

    /// <summary>Verifies a 204 No Content status with an empty body.</summary>
    /// <param name="actual">The response, which remains owned by the caller.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The response, for further assertions.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="actual"/> is null.</exception>
    /// <exception cref="XunitException">Thrown when the status is not 204 or the body is not empty.</exception>
    /// <exception cref="OperationCanceledException">The supplied cancellation token is canceled.</exception>
    public static async Task<HttpResponseMessage> ShouldBeNoContent(this HttpResponseMessage actual, CancellationToken cancellationToken = default)
    {
        await EnsureStatus(actual, HttpStatusCode.NoContent, cancellationToken).ConfigureAwait(false);
        var body = await actual.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        if (body.Length > 0)
        {
            var excerpt = await HttpResponseDescription.Body(actual, cancellationToken).ConfigureAwait(false);
            throw new XunitException($"Expected an empty body but it was: {excerpt}");
        }

        return actual;
    }

    /// <summary>Verifies a 204 No Content status with an empty body.</summary>
    /// <param name="actual">The response, which remains owned by the caller.</param>
    /// <param name="userMessage">Why the test expects the assertion to hold; the failure message starts with it.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The response, for further assertions.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="actual"/> is null. Also thrown when <paramref name="userMessage"/> is null.</exception>
    /// <exception cref="OperationCanceledException">The supplied cancellation token is canceled.</exception>
    public static Task<HttpResponseMessage> ShouldBeNoContent(this HttpResponseMessage actual, string userMessage, CancellationToken cancellationToken = default) =>
        WithUserMessage(userMessage, () => actual.ShouldBeNoContent(cancellationToken));

    /// <summary>Verifies a 202 Accepted status.</summary>
    /// <param name="actual">The response, which remains owned by the caller.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The response, for further assertions.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="actual"/> is null.</exception>
    /// <exception cref="XunitException">Thrown when the status is not 202; the message quotes a truncated excerpt of the body.</exception>
    /// <exception cref="OperationCanceledException">The supplied cancellation token is canceled.</exception>
    public static Task<HttpResponseMessage> ShouldBeAccepted(this HttpResponseMessage actual, CancellationToken cancellationToken = default) =>
        actual.ShouldHaveStatus(HttpStatusCode.Accepted, cancellationToken);

    /// <summary>Verifies a 202 Accepted status.</summary>
    /// <param name="actual">The response, which remains owned by the caller.</param>
    /// <param name="userMessage">Why the test expects the assertion to hold; the failure message starts with it.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The response, for further assertions.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="actual"/> is null. Also thrown when <paramref name="userMessage"/> is null.</exception>
    /// <exception cref="OperationCanceledException">The supplied cancellation token is canceled.</exception>
    public static Task<HttpResponseMessage> ShouldBeAccepted(this HttpResponseMessage actual, string userMessage, CancellationToken cancellationToken = default) =>
        WithUserMessage(userMessage, () => actual.ShouldBeAccepted(cancellationToken));

    /// <summary>Verifies a 200 OK file download: the media type and an <c>attachment</c> <c>Content-Disposition</c>.</summary>
    /// <param name="actual">The response, which remains owned by the caller.</param>
    /// <param name="contentType">The expected media type; parameters such as <c>charset</c> and the case are ignored.</param>
    /// <param name="fileName">The exact expected file name, or null to leave it unchecked. The RFC 5987 <c>filename*</c> parameter wins over <c>filename</c>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The file bytes.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="actual"/> or <paramref name="contentType"/> is null.</exception>
    /// <exception cref="XunitException">Thrown when the status, media type, disposition, or file name differs.</exception>
    /// <exception cref="OperationCanceledException">The supplied cancellation token is canceled.</exception>
    public static async Task<byte[]> ShouldBeFile(this HttpResponseMessage actual, string contentType, string? fileName = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contentType);
        await EnsureStatus(actual, HttpStatusCode.OK, cancellationToken).ConfigureAwait(false);
        await EnsureMediaType(actual, MediaTypeHeaderValue.Parse(contentType).MediaType!, cancellationToken).ConfigureAwait(false);
        var disposition = actual.Content.Headers.ContentDisposition
            ?? throw new XunitException("Expected Content-Disposition 'attachment' but the response has none.");
        if (!string.Equals(disposition.DispositionType, "attachment", StringComparison.OrdinalIgnoreCase))
        {
            throw new XunitException($"Expected Content-Disposition 'attachment' but was '{disposition.DispositionType}'.");
        }

        if (fileName is not null)
        {
            var actualName = disposition.FileNameStar ?? disposition.FileName?.Trim('"')
                ?? throw new XunitException($"Expected file name '{fileName}' but the Content-Disposition has none.");
            if (!string.Equals(actualName, fileName, StringComparison.Ordinal))
            {
                throw new XunitException($"Expected file name '{fileName}' but was '{actualName}'.");
            }
        }

        return await actual.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Verifies a 200 OK file download: the media type and an <c>attachment</c> <c>Content-Disposition</c>.</summary>
    /// <param name="actual">The response, which remains owned by the caller.</param>
    /// <param name="contentType">The expected media type; parameters such as <c>charset</c> and the case are ignored.</param>
    /// <param name="fileName">The exact expected file name, or null to leave it unchecked. The RFC 5987 <c>filename*</c> parameter wins over <c>filename</c>.</param>
    /// <param name="userMessage">Why the test expects the assertion to hold; the failure message starts with it.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The file bytes.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="actual"/> or <paramref name="contentType"/> is null. Also thrown when <paramref name="userMessage"/> is null.</exception>
    /// <exception cref="OperationCanceledException">The supplied cancellation token is canceled.</exception>
    public static Task<byte[]> ShouldBeFile(this HttpResponseMessage actual, string contentType, string? fileName, string userMessage,
        CancellationToken cancellationToken = default) =>
        WithUserMessage(userMessage, () => actual.ShouldBeFile(contentType, fileName, cancellationToken));

    /// <summary>Verifies that a header, found by case-insensitive name on the response or its content, has the exact value.</summary>
    /// <param name="actual">The response.</param>
    /// <param name="name">The header name; the case is ignored.</param>
    /// <param name="expectedValue">The exact, case-sensitive value; a header with several values passes when any one value, or all joined with a comma and space, equals it.</param>
    /// <returns>The response, for further assertions.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="XunitException">Thrown when the header is missing or has other values; the values of credential headers are not quoted.</exception>
    public static HttpResponseMessage ShouldHaveHeader(this HttpResponseMessage actual, string name, string expectedValue)
    {
        ArgumentNullException.ThrowIfNull(actual);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(expectedValue);
        if (!actual.Headers.TryGetValues(name, out var values) && !actual.Content.Headers.TryGetValues(name, out values))
        {
            throw new XunitException($"Expected header '{name}' to be '{expectedValue}' but the response has none.");
        }

        var all = values.ToArray();
        var joined = string.Join(", ", all);
        if (!all.Contains(expectedValue, StringComparer.Ordinal) && !string.Equals(joined, expectedValue, StringComparison.Ordinal))
        {
            var shown = CredentialHeaders.Contains(name) ? "<redacted>" : joined;
            throw new XunitException($"Expected header '{name}' to be '{expectedValue}' but was '{shown}'.");
        }

        return actual;
    }

    /// <summary>Verifies that a header, found by case-insensitive name on the response or its content, has the exact value.</summary>
    /// <param name="actual">The response.</param>
    /// <param name="name">The header name; the case is ignored.</param>
    /// <param name="expectedValue">The exact, case-sensitive value; a header with several values passes when any one value, or all joined with a comma and space, equals it.</param>
    /// <param name="userMessage">Why the test expects the assertion to hold; the failure message starts with it.</param>
    /// <returns>The response, for further assertions.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    public static HttpResponseMessage ShouldHaveHeader(this HttpResponseMessage actual, string name, string expectedValue, string userMessage) =>
        WithUserMessage(userMessage, () => actual.ShouldHaveHeader(name, expectedValue));

    /// <summary>Verifies the headers that <c>HttpCacheHeaders.SetNoStore</c> of <c>PandaBear.SharedKernel.HttpCaching.AspNetCore</c> writes.</summary>
    /// <remarks>Requires <c>Cache-Control: no-store</c>, <c>Pragma: no-cache</c>, and an <c>Expires</c> date that is not in the future.</remarks>
    /// <param name="actual">The response.</param>
    /// <returns>The response, for further assertions.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="actual"/> is null.</exception>
    /// <exception cref="XunitException">Thrown when one of the three headers is missing or has another value.</exception>
    public static HttpResponseMessage ShouldBeNoStore(this HttpResponseMessage actual)
    {
        ArgumentNullException.ThrowIfNull(actual);
        var cacheControl = actual.Headers.CacheControl;
        if (cacheControl?.NoStore != true)
        {
            throw new XunitException(Missing("Cache-Control", "contain 'no-store'", cacheControl?.ToString()));
        }

        if (!actual.Headers.Pragma.Any(static pragma => string.Equals(pragma.Name, "no-cache", StringComparison.OrdinalIgnoreCase)))
        {
            throw new XunitException(Missing("Pragma", "contain 'no-cache'", actual.Headers.Pragma.Count == 0 ? null : string.Join(", ", actual.Headers.Pragma)));
        }

        var expires = actual.Content.Headers.Expires;
        if (expires is null || expires.Value > TimeProvider.System.GetUtcNow())
        {
            throw new XunitException(Missing("Expires", "be a date in the past", expires?.ToString("R", CultureInfo.InvariantCulture)));
        }

        return actual;
    }

    /// <summary>Verifies the headers that <c>HttpCacheHeaders.SetNoStore</c> of <c>PandaBear.SharedKernel.HttpCaching.AspNetCore</c> writes.</summary>
    /// <remarks>Requires <c>Cache-Control: no-store</c>, <c>Pragma: no-cache</c>, and an <c>Expires</c> date that is not in the future.</remarks>
    /// <param name="actual">The response.</param>
    /// <param name="userMessage">Why the test expects the assertion to hold; the failure message starts with it.</param>
    /// <returns>The response, for further assertions.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="actual"/> is null. Also thrown when <paramref name="userMessage"/> is null.</exception>
    public static HttpResponseMessage ShouldBeNoStore(this HttpResponseMessage actual, string userMessage) =>
        WithUserMessage(userMessage, () => actual.ShouldBeNoStore());

    /// <summary>Verifies that the <c>ETag</c> header is the expected strong entity tag.</summary>
    /// <param name="actual">The response.</param>
    /// <param name="expectedStrongTag">The quoted tag, such as <c>"v1"</c> with its quotation marks and without a <c>W/</c> prefix.</param>
    /// <returns>The response, for further assertions.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="expectedStrongTag"/> is not a quoted strong entity tag.</exception>
    /// <exception cref="XunitException">Thrown when the header is missing, is weak, or has another tag.</exception>
    public static HttpResponseMessage ShouldHaveETag(this HttpResponseMessage actual, string expectedStrongTag)
    {
        ArgumentNullException.ThrowIfNull(actual);
        ArgumentNullException.ThrowIfNull(expectedStrongTag);
        if (expectedStrongTag.Length < 2 || expectedStrongTag[0] != '"' || expectedStrongTag[^1] != '"')
        {
            throw new ArgumentException("The expected tag must be a quoted strong entity tag such as \"v1\", without a W/ prefix.", nameof(expectedStrongTag));
        }

        var etag = actual.Headers.ETag
            ?? throw new XunitException($"Expected strong ETag '{expectedStrongTag}' but the response has none.");
        if (etag.IsWeak || !string.Equals(etag.Tag, expectedStrongTag, StringComparison.Ordinal))
        {
            throw new XunitException($"Expected strong ETag '{expectedStrongTag}' but was '{etag}'.");
        }

        return actual;
    }

    /// <summary>Verifies that the <c>ETag</c> header is the expected strong entity tag.</summary>
    /// <param name="actual">The response.</param>
    /// <param name="expectedStrongTag">The quoted tag, such as <c>"v1"</c> with its quotation marks and without a <c>W/</c> prefix.</param>
    /// <param name="userMessage">Why the test expects the assertion to hold; the failure message starts with it.</param>
    /// <returns>The response, for further assertions.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="expectedStrongTag"/> is not a quoted strong entity tag.</exception>
    public static HttpResponseMessage ShouldHaveETag(this HttpResponseMessage actual, string expectedStrongTag, string userMessage) =>
        WithUserMessage(userMessage, () => actual.ShouldHaveETag(expectedStrongTag));

    private static async Task EnsureStatus(HttpResponseMessage actual, HttpStatusCode expectedStatus, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actual);
        cancellationToken.ThrowIfCancellationRequested();
        if (actual.StatusCode != expectedStatus)
        {
            var body = await HttpResponseDescription.Body(actual, cancellationToken).ConfigureAwait(false);
            throw new XunitException(
                $"Expected HTTP status {HttpResponseDescription.Status(expectedStatus)} but was {HttpResponseDescription.Status(actual.StatusCode)}. Body: {body}");
        }
    }

    private static async Task EnsureMediaType(HttpResponseMessage actual, string expectedMediaType, CancellationToken cancellationToken)
    {
        var contentType = actual.Content.Headers.ContentType;
        if (!string.Equals(contentType?.MediaType, expectedMediaType, StringComparison.OrdinalIgnoreCase))
        {
            var body = await HttpResponseDescription.Body(actual, cancellationToken).ConfigureAwait(false);
            throw new XunitException($"{HttpResponseDescription.ContentTypeDifference(expectedMediaType, contentType)} Body: {body}");
        }
    }

    private static Uri RequireLocation(HttpResponseMessage actual) =>
        actual.Headers.Location ?? throw new XunitException("Expected a Location header but the response has none.");

    private static async Task<T> ReadJson<T>(HttpResponseMessage actual, HttpStatusCode expectedStatus, bool requireLocation, Func<byte[], T?> deserialize,
        CancellationToken cancellationToken)
    {
        await EnsureStatus(actual, expectedStatus, cancellationToken).ConfigureAwait(false);
        if (requireLocation)
        {
            RequireLocation(actual);
        }

        await EnsureMediaType(actual, JsonMediaType, cancellationToken).ConfigureAwait(false);
        var bytes = await actual.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        if (bytes.Length == 0)
        {
            throw new XunitException("Expected a JSON body but it was empty.");
        }

        T? value;
        try
        {
            value = deserialize(bytes);
        }
        catch (JsonException exception)
        {
            var body = await HttpResponseDescription.Body(actual, cancellationToken).ConfigureAwait(false);
            throw new XunitException($"Expected the body to deserialize to '{typeof(T).Name}' but it did not: {exception.Message} Body: {body}", exception);
        }

        return value ?? throw new XunitException($"Expected the body to deserialize to '{typeof(T).Name}' but it was the JSON value null.");
    }

    private static string Missing(string header, string expectation, string? actualValue) =>
        actualValue is null
            ? $"Expected header '{header}' to {expectation} but the response has none."
            : $"Expected header '{header}' to {expectation} but was '{actualValue}'.";
}
