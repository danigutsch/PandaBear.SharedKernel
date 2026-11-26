using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net;
using System.Text;
using SharedKernel.Testing;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace SharedKernel.HttpClients.Tests;

/// <summary>
/// Unit tests for HttpOutcomeMapper.Send mapping of HTTP responses, timeouts, cancellation, and transport failures to bounded HttpOutcome values.
/// </summary>
[Trait(TestTraitNames.ScopeName, "unit")]
public sealed class HttpOutcomeMapperTests
{
    private static readonly HttpOutcomeMapperTestsJsonContext Json = HttpOutcomeMapperTestsJsonContext.Default;

    [Fact]
    public async Task Send_returns_success_with_materialized_body_and_propagates_the_exact_token()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        using var handler = new HttpOutcomeMapperTestHandler((_, _) => Task.FromResult(
            HttpOutcomeMapperTestResponses.Json(HttpStatusCode.OK, "{\"name\":\"Lisbon\"}")));
        using var client = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("https://catalog.example.test") };
        var request = new HttpRequestMessage(HttpMethod.Get, "/tours");

        // Act
        var outcome = await HttpOutcomeMapper.Send(
            client,
            request,
            HttpStatusCode.OK,
            Json.HttpOutcomeMapperTestPayload,
            Json.ContractValidationProblemDto,
            cts.Token);

        // Assert
        outcome.Kind.ShouldBe(HttpOutcomeKind.Succeeded);
        outcome.StatusCode.ShouldBe(HttpStatusCode.OK);
        var value = outcome.Value.ShouldNotBeNull();
        value.Name.ShouldBe("Lisbon");
        handler.ObservedCancellationToken.CanBeCanceled.ShouldBeTrue();
        handler.ObservedMethod.ShouldBe(HttpMethod.Get);
        handler.ObservedRequestUri.ShouldBe(new Uri("https://catalog.example.test/tours"));
    }

    [Theory]
    [InlineData("\"v1\"")]
    [InlineData("W/\"v1\"")]
    public async Task Send_retains_the_response_entity_tag_unchanged_on_success(string entityTag)
    {
        // Arrange
        using var handler = new HttpOutcomeMapperTestHandler((_, _) => Task.FromResult(
            HttpOutcomeMapperTestResponses.JsonWithETag(HttpStatusCode.OK, "{\"name\":\"Lisbon\"}", entityTag)));
        using var client = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("https://catalog.example.test") };

        // Act
        var outcome = await HttpOutcomeMapper.Send(
            client,
            new HttpRequestMessage(HttpMethod.Get, "/tours"),
            HttpStatusCode.OK,
            Json.HttpOutcomeMapperTestPayload,
            Json.ContractValidationProblemDto,
            TestContext.Current.CancellationToken);

        // Assert
        outcome.Kind.ShouldBe(HttpOutcomeKind.Succeeded);
        outcome.ETag.ShouldBe(entityTag);
    }

    [Fact]
    public async Task Send_leaves_the_entity_tag_unset_when_the_response_has_none()
    {
        // Arrange
        using var handler = new HttpOutcomeMapperTestHandler((_, _) => Task.FromResult(
            HttpOutcomeMapperTestResponses.Json(HttpStatusCode.OK, "{\"name\":\"Lisbon\"}")));
        using var client = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("https://catalog.example.test") };

        // Act
        var outcome = await HttpOutcomeMapper.Send(
            client,
            new HttpRequestMessage(HttpMethod.Get, "/tours"),
            HttpStatusCode.OK,
            Json.HttpOutcomeMapperTestPayload,
            Json.ContractValidationProblemDto,
            TestContext.Current.CancellationToken);

        // Assert
        outcome.Kind.ShouldBe(HttpOutcomeKind.Succeeded);
        outcome.ETag.ShouldBeNull();
    }

    [Fact]
    public async Task Send_returns_not_found_without_reading_a_body()
    {
        // Arrange
        using var responseBody = new TrackingReadStream("sensitive missing resource"u8.ToArray());
        using var handler = new HttpOutcomeMapperTestHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StreamContent(responseBody)
            }));
        using var client = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("https://catalog.example.test") };

        // Act
        var outcome = await HttpOutcomeMapper.Send(
            client,
            new HttpRequestMessage(HttpMethod.Get, "/tours/missing"),
            HttpStatusCode.OK,
            Json.HttpOutcomeMapperTestPayload,
            Json.ContractValidationProblemDto,
            TestContext.Current.CancellationToken);

        // Assert
        outcome.Kind.ShouldBe(HttpOutcomeKind.NotFound);
        outcome.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        outcome.Value.ShouldBeNull();
        responseBody.ReadCount.ShouldBe(0);
        responseBody.IsDisposed.ShouldBeTrue();
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, HttpOutcomeKind.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden, HttpOutcomeKind.Forbidden)]
    [InlineData(HttpStatusCode.Conflict, HttpOutcomeKind.Conflict)]
    public async Task Send_returns_bounded_kind_for_known_non_success_status(
        HttpStatusCode statusCode,
        HttpOutcomeKind expectedKind)
    {
        // Arrange
        using var handler = new HttpOutcomeMapperTestHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(statusCode)));
        using var client = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("https://catalog.example.test") };

        // Act
        var outcome = await HttpOutcomeMapper.Send(
            client,
            new HttpRequestMessage(HttpMethod.Get, "/content"),
            HttpStatusCode.OK,
            Json.HttpOutcomeMapperTestPayload,
            Json.ContractValidationProblemDto,
            TestContext.Current.CancellationToken);

        // Assert
        outcome.Kind.ShouldBe(expectedKind);
        outcome.StatusCode.ShouldBe(statusCode);
        outcome.Value.ShouldBeNull();
    }

    [Theory]
    [InlineData("Name")]
    [InlineData("")]
    public async Task Send_returns_validation_problem_from_problem_JSON(string fieldName)
    {
        // Arrange
        using var handler = new HttpOutcomeMapperTestHandler((_, _) => Task.FromResult(
            HttpOutcomeMapperTestResponses.Json(
                HttpStatusCode.BadRequest,
                $$$"""{"errors":{"{{{fieldName}}}":["Name is required."]}}""",
                "application/problem+json")));
        using var client = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("https://admin.example.test") };

        // Act
        var outcome = await HttpOutcomeMapper.Send(
            client,
            new HttpRequestMessage(HttpMethod.Post, "/customers"),
            HttpStatusCode.Created,
            Json.HttpOutcomeMapperTestPayload,
            Json.ContractValidationProblemDto,
            TestContext.Current.CancellationToken);

        // Assert
        outcome.Kind.ShouldBe(HttpOutcomeKind.ValidationProblem);
        outcome.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var errors = outcome.ValidationErrors.ShouldNotBeNull();
        errors[fieldName].ShouldBeEquivalentTo("Name is required.");
    }

    [Fact]
    public async Task Send_returns_empty_body_for_whitespace_success_content()
    {
        // Arrange
        using var handler = new HttpOutcomeMapperTestHandler((_, _) => Task.FromResult(
            HttpOutcomeMapperTestResponses.Json(HttpStatusCode.OK, "   ")));
        using var client = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("https://catalog.example.test") };

        // Act
        var outcome = await HttpOutcomeMapper.Send(
            client,
            new HttpRequestMessage(HttpMethod.Get, "/content"),
            HttpStatusCode.OK,
            Json.HttpOutcomeMapperTestPayload,
            Json.ContractValidationProblemDto,
            TestContext.Current.CancellationToken);

        // Assert
        outcome.Kind.ShouldBe(HttpOutcomeKind.EmptyBody);
        outcome.Message.ShouldBe("HTTP response body was empty.");
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("null")]
    public async Task Send_returns_malformed_body_for_invalid_or_null_JSON(string content)
    {
        // Arrange
        using var handler = new HttpOutcomeMapperTestHandler((_, _) => Task.FromResult(
            HttpOutcomeMapperTestResponses.Json(HttpStatusCode.OK, content)));
        using var client = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("https://catalog.example.test") };

        // Act
        var outcome = await HttpOutcomeMapper.Send(
            client,
            new HttpRequestMessage(HttpMethod.Get, "/content"),
            HttpStatusCode.OK,
            Json.HttpOutcomeMapperTestPayload,
            Json.ContractValidationProblemDto,
            TestContext.Current.CancellationToken);

        // Assert
        outcome.Kind.ShouldBe(HttpOutcomeKind.MalformedBody);
        outcome.Value.ShouldBeNull();
        outcome.Message.ShouldNotContain(content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Send_returns_unsupported_content_type_without_exposing_the_body()
    {
        // Arrange
        const string sensitiveBody = "{\"name\":\"alice@example.test\"}";
        using var responseBody = new TrackingReadStream(Encoding.UTF8.GetBytes(sensitiveBody));
        using var handler = new HttpOutcomeMapperTestHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(responseBody)
                {
                    Headers = { ContentType = new MediaTypeHeaderValue("text/plain") }
                }
            }));
        using var client = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("https://catalog.example.test") };

        // Act
        var outcome = await HttpOutcomeMapper.Send(
            client,
            new HttpRequestMessage(HttpMethod.Get, "/content"),
            HttpStatusCode.OK,
            Json.HttpOutcomeMapperTestPayload,
            Json.ContractValidationProblemDto,
            TestContext.Current.CancellationToken);

        // Assert
        outcome.Kind.ShouldBe(HttpOutcomeKind.UnsupportedContentType);
        outcome.Message.ShouldBe("HTTP response content type was not JSON.");
        outcome.Message.ShouldNotContain("alice@example.test", StringComparison.Ordinal);
        responseBody.ReadCount.ShouldBe(0);
        responseBody.IsDisposed.ShouldBeTrue();
    }

    [Fact]
    public async Task Send_without_value_does_not_read_an_ignored_success_body()
    {
        // Arrange
        using var responseBody = new TrackingReadStream("ignored response"u8.ToArray());
        using var handler = new HttpOutcomeMapperTestHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StreamContent(responseBody)
            }));
        using var client = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("https://admin.example.test") };

        // Act
        var outcome = await HttpOutcomeMapper.Send(
            client,
            new HttpRequestMessage(HttpMethod.Post, "/customers"),
            HttpStatusCode.Created,
            Json.ContractValidationProblemDto,
            TestContext.Current.CancellationToken);

        // Assert
        outcome.Kind.ShouldBe(HttpOutcomeKind.Succeeded);
        responseBody.ReadCount.ShouldBe(0);
        responseBody.IsDisposed.ShouldBeTrue();
    }

    [Fact]
    [Trait(TestTraitNames.CategoryName, TestTraitValues.SecurityCategory)]
    public async Task Send_returns_malformed_body_after_reading_only_the_bounded_oversized_JSON_prefix()
    {
        // Arrange
        const int responseBodyLimit = 64;
        var oversizedJson = Encoding.UTF8.GetBytes($"{{\"name\":\"{new string('x', responseBodyLimit * 2)}\"}}");
        using var responseBody = new TrackingReadStream(oversizedJson);
        using var handler = new HttpOutcomeMapperTestHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(responseBody)
                {
                    Headers = { ContentType = new MediaTypeHeaderValue("application/json") }
                }
            }));
        using var client = new HttpClient(handler, disposeHandler: false)
        {
            BaseAddress = new Uri("https://catalog.example.test"),
            MaxResponseContentBufferSize = responseBodyLimit
        };

        // Act
        var outcome = await HttpOutcomeMapper.Send(
            client,
            new HttpRequestMessage(HttpMethod.Get, "/content"),
            HttpStatusCode.OK,
            Json.HttpOutcomeMapperTestPayload,
            Json.ContractValidationProblemDto,
            TestContext.Current.CancellationToken);

        // Assert
        outcome.Kind.ShouldBe(HttpOutcomeKind.MalformedBody);
        outcome.StatusCode.ShouldBe(HttpStatusCode.OK);
        outcome.Value.ShouldBeNull();
        outcome.Message.ShouldBe("HTTP response body was malformed.");
        responseBody.BytesRead.ShouldBeLessThanOrEqualTo(responseBodyLimit + 1);
        responseBody.IsDisposed.ShouldBeTrue();
    }

    [Theory]
    [InlineData("{\"errors\":{\"Name\":null}}")]
    [InlineData("{\"errors\":{\"Name\":[]}}")]
    [InlineData("{\"errors\":{\" \":[\"Name is required.\"]}}")]
    [InlineData("{\"errors\":{\"Name\":[\" \"]}}")]
    [InlineData("{\"errors\":{\"Name\":[\"Valid message\",null]}}")]
    public async Task Send_returns_malformed_body_for_invalid_validation_error_entries(string content)
    {
        // Arrange
        using var handler = new HttpOutcomeMapperTestHandler((_, _) => Task.FromResult(
            HttpOutcomeMapperTestResponses.Json(
                HttpStatusCode.BadRequest,
                content,
                "application/problem+json")));
        using var client = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("https://admin.example.test") };

        // Act
        var outcome = await HttpOutcomeMapper.Send(
            client,
            new HttpRequestMessage(HttpMethod.Post, "/customers"),
            HttpStatusCode.Created,
            Json.HttpOutcomeMapperTestPayload,
            Json.ContractValidationProblemDto,
            TestContext.Current.CancellationToken);

        // Assert
        outcome.Kind.ShouldBe(HttpOutcomeKind.MalformedBody);
        outcome.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        outcome.ValidationErrors.ShouldBeNull();
        outcome.Message.ShouldBe("Validation problem response body was malformed.");
    }

    [Fact]
    public async Task Send_returns_timed_out_when_transport_cancels_without_caller_cancellation()
    {
        // Arrange
        using var handler = new HttpOutcomeMapperTestHandler((_, _) => throw new TaskCanceledException("secret timeout details"));
        using var client = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("https://catalog.example.test") };

        // Act
        var outcome = await HttpOutcomeMapper.Send(
            client,
            new HttpRequestMessage(HttpMethod.Get, "/content"),
            HttpStatusCode.OK,
            Json.HttpOutcomeMapperTestPayload,
            Json.ContractValidationProblemDto,
            TestContext.Current.CancellationToken);

        // Assert
        outcome.Kind.ShouldBe(HttpOutcomeKind.TimedOut);
        outcome.StatusCode.ShouldBeNull();
        outcome.Message.ShouldBe("HTTP request timed out.");
    }

    [Fact]
    public async Task Send_returns_timed_out_when_the_resilience_pipeline_times_out()
    {
        // Arrange
        using var handler = new HttpOutcomeMapperTestHandler((_, _) => throw new TimeoutRejectedException("secret timeout details"));
        using var client = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("https://catalog.example.test") };

        // Act
        var outcome = await HttpOutcomeMapper.Send(
            client,
            new HttpRequestMessage(HttpMethod.Get, "/content"),
            HttpStatusCode.OK,
            Json.HttpOutcomeMapperTestPayload,
            Json.ContractValidationProblemDto,
            TestContext.Current.CancellationToken);

        // Assert
        outcome.Kind.ShouldBe(HttpOutcomeKind.TimedOut);
        outcome.StatusCode.ShouldBeNull();
        outcome.Message.ShouldBe("HTTP request timed out.");
    }

    [Fact]
    public async Task Send_returns_timed_out_when_the_response_body_stalls_after_headers()
    {
        // Arrange
        using var responseBody = new BlockingReadStream();
        using var handler = new HttpOutcomeMapperTestHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(responseBody)
                {
                    Headers = { ContentType = new MediaTypeHeaderValue("application/json") }
                }
            }));
        using var client = new HttpClient(handler, disposeHandler: false)
        {
            BaseAddress = new Uri("https://catalog.example.test"),
            Timeout = TimeSpan.FromMilliseconds(100)
        };

        // Act
        var outcome = await HttpOutcomeMapper.Send(
            client,
            new HttpRequestMessage(HttpMethod.Get, "/content"),
            HttpStatusCode.OK,
            Json.HttpOutcomeMapperTestPayload,
            Json.ContractValidationProblemDto,
            TestContext.Current.CancellationToken);

        // Assert
        responseBody.ReadStarted.IsCompletedSuccessfully.ShouldBeTrue();
        outcome.Kind.ShouldBe(HttpOutcomeKind.TimedOut);
        outcome.StatusCode.ShouldBeNull();
        outcome.Message.ShouldBe("HTTP request timed out.");
        responseBody.IsDisposed.ShouldBeTrue();
    }

    [Fact]
    public async Task Send_preserves_caller_cancellation()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        using var handler = new HttpOutcomeMapperTestHandler((_, token) => throw new OperationCanceledException(token));
        using var client = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("https://catalog.example.test") };

        // Act
        Func<Task> action = async () => await HttpOutcomeMapper.Send(
                client,
                new HttpRequestMessage(HttpMethod.Get, "/content"),
                HttpStatusCode.OK,
                Json.HttpOutcomeMapperTestPayload,
                Json.ContractValidationProblemDto,
                cts.Token)
            .ConfigureAwait(false);

        // Assert
        await action.ShouldThrowAssignableTo<OperationCanceledException>();
    }

    [Fact]
    public async Task Send_preserves_caller_cancellation_during_response_body_reading()
    {
        // Arrange
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        using var responseBody = new BlockingReadStream(cts.Cancel);
        using var handler = new HttpOutcomeMapperTestHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(responseBody)
                {
                    Headers = { ContentType = new MediaTypeHeaderValue("application/json") }
                }
            }));
        using var client = new HttpClient(handler, disposeHandler: false)
        {
            BaseAddress = new Uri("https://catalog.example.test"),
            Timeout = TimeSpan.FromMinutes(1)
        };
        var validations = 0;
        // Act
        Func<Task> action = async () => await HttpOutcomeMapper.Send(
                client,
                new HttpRequestMessage(HttpMethod.Get, "/content"),
                HttpStatusCode.OK,
                Json.HttpOutcomeMapperTestPayload,
                Json.ContractValidationProblemDto,
                validateValue: _ =>
                {
                    validations++;
                    return true;
                },
                cts.Token)
            .ConfigureAwait(false);
        var exception = await action
            .ShouldThrowAssignableTo<OperationCanceledException>();

        // Assert
        responseBody.ReadStarted.IsCompletedSuccessfully.ShouldBeTrue();
        exception.CancellationToken.ShouldBe(cts.Token);
        responseBody.IsDisposed.ShouldBeTrue();
        validations.ShouldBe(0);
    }

    [Fact]
    public async Task Send_without_value_preserves_caller_cancellation_with_a_finite_timeout()
    {
        // Arrange
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        using var responseBody = new BlockingReadStream(cts.Cancel);
        using var handler = new HttpOutcomeMapperTestHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StreamContent(responseBody)
                {
                    Headers = { ContentType = new MediaTypeHeaderValue("application/problem+json") }
                }
            }));
        using var client = new HttpClient(handler, disposeHandler: false)
        {
            BaseAddress = new Uri("https://admin.example.test"),
            Timeout = TimeSpan.FromMinutes(1)
        };
        Func<Task> action = async () => await HttpOutcomeMapper.Send(
                client,
                new HttpRequestMessage(HttpMethod.Post, "/customers"),
                HttpStatusCode.Created,
                Json.ContractValidationProblemDto,
                cts.Token)
            .ConfigureAwait(false);

        // Act
        var exception = await action.ShouldThrowAssignableTo<OperationCanceledException>();

        // Assert
        exception.CancellationToken.ShouldBe(cts.Token);
        responseBody.ReadStarted.IsCompletedSuccessfully.ShouldBeTrue();
        responseBody.IsDisposed.ShouldBeTrue();
    }

    [Fact]
    public async Task Send_rejects_a_known_oversized_JSON_body_without_reading_it()
    {
        // Arrange
        const int responseBodyLimit = 64;
        using var responseBody = new TrackingReadStream("{}"u8.ToArray());
        var content = new StreamContent(responseBody);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        content.Headers.ContentLength = responseBodyLimit + 1;
        using var handler = new HttpOutcomeMapperTestHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = content }));
        using var client = new HttpClient(handler, disposeHandler: false)
        {
            BaseAddress = new Uri("https://catalog.example.test"),
            MaxResponseContentBufferSize = responseBodyLimit
        };

        // Act
        var outcome = await HttpOutcomeMapper.Send(
            client,
            new HttpRequestMessage(HttpMethod.Get, "/content"),
            HttpStatusCode.OK,
            Json.HttpOutcomeMapperTestPayload,
            Json.ContractValidationProblemDto,
            TestContext.Current.CancellationToken);

        // Assert
        outcome.Kind.ShouldBe(HttpOutcomeKind.MalformedBody);
        outcome.StatusCode.ShouldBe(HttpStatusCode.OK);
        responseBody.ReadCount.ShouldBe(0);
        responseBody.IsDisposed.ShouldBeTrue();
    }

    [Fact]
    public async Task Send_preserves_request_content_read_failures_for_upload_callers()
    {
        // Arrange
        var requestContent = new TrackingStringContent("request", "application/octet-stream");
        using var handler = new HttpOutcomeMapperTestHandler((request, _) =>
        {
            request.Content.ShouldNotBeNull();
            throw new IOException("The local upload stream could not be read.");
        });
        using var client = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("https://catalog.example.test") };
        using var request = new HttpRequestMessage(HttpMethod.Post, "/images") { Content = requestContent };

        // Act
        Func<Task> action = async () => await HttpOutcomeMapper.Send(
                client,
                request,
                HttpStatusCode.Created,
                Json.HttpOutcomeMapperTestPayload,
                Json.ContractValidationProblemDto,
                TestContext.Current.CancellationToken)
            .ConfigureAwait(false);

        // Assert
        await action.ShouldThrow<IOException>();
        requestContent.IsDisposed.ShouldBeTrue();
    }

    [Fact]
    [Trait(TestTraitNames.CategoryName, TestTraitValues.SecurityCategory)]
    public async Task Send_returns_redacted_transport_failure()
    {
        // Arrange
        using var handler = new HttpOutcomeMapperTestHandler((_, _) => throw new HttpRequestException(
            "Bearer secret-token failed for alice@example.test"));
        using var client = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("https://catalog.example.test") };

        // Act
        var outcome = await HttpOutcomeMapper.Send(
            client,
            new HttpRequestMessage(HttpMethod.Get, "/content?token=secret-token"),
            HttpStatusCode.OK,
            Json.HttpOutcomeMapperTestPayload,
            Json.ContractValidationProblemDto,
            TestContext.Current.CancellationToken);

        // Assert
        outcome.Kind.ShouldBe(HttpOutcomeKind.TransportFailure);
        outcome.StatusCode.ShouldBeNull();
        outcome.Message.ShouldBe("HTTP transport failed.");
        outcome.Message.ShouldNotContain("secret-token", StringComparison.Ordinal);
        outcome.Message.ShouldNotContain("alice@example.test", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Send_returns_transport_failure_when_the_circuit_is_open()
    {
        // Arrange
        using var handler = new HttpOutcomeMapperTestHandler((_, _) => throw new BrokenCircuitException("secret circuit details"));
        using var client = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("https://catalog.example.test") };

        // Act
        var outcome = await HttpOutcomeMapper.Send(
            client,
            new HttpRequestMessage(HttpMethod.Get, "/content"),
            HttpStatusCode.OK,
            Json.HttpOutcomeMapperTestPayload,
            Json.ContractValidationProblemDto,
            TestContext.Current.CancellationToken);

        // Assert
        outcome.Kind.ShouldBe(HttpOutcomeKind.TransportFailure);
        outcome.StatusCode.ShouldBeNull();
        outcome.Message.ShouldBe("HTTP transport failed.");
    }

    [Fact]
    public async Task Send_returns_unexpected_status_for_an_unowned_response_status()
    {
        // Arrange
        using var handler = new HttpOutcomeMapperTestHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.Accepted)));
        using var client = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("https://catalog.example.test") };

        // Act
        var outcome = await HttpOutcomeMapper.Send(
            client,
            new HttpRequestMessage(HttpMethod.Get, "/content"),
            HttpStatusCode.OK,
            Json.HttpOutcomeMapperTestPayload,
            Json.ContractValidationProblemDto,
            TestContext.Current.CancellationToken);

        // Assert
        outcome.Kind.ShouldBe(HttpOutcomeKind.UnexpectedStatus);
        outcome.StatusCode.ShouldBe(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task Send_disposes_the_owned_request_response_and_content()
    {
        // Arrange
        var responseContent = new TrackingStringContent("{\"name\":\"Lisbon\"}", "application/json");
        var requestContent = new TrackingStringContent("request", "application/json");
        using var handler = new HttpOutcomeMapperTestHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = responseContent }));
        using var client = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("https://catalog.example.test") };
        var request = new HttpRequestMessage(HttpMethod.Post, "/content") { Content = requestContent };

        // Act
        var outcome = await HttpOutcomeMapper.Send(
            client,
            request,
            HttpStatusCode.OK,
            Json.HttpOutcomeMapperTestPayload,
            Json.ContractValidationProblemDto,
            TestContext.Current.CancellationToken);

        // Assert
        outcome.Kind.ShouldBe(HttpOutcomeKind.Succeeded);
        requestContent.IsDisposed.ShouldBeTrue();
        responseContent.IsDisposed.ShouldBeTrue();
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, """{"name":"Fixture"}""", "application/json", HttpOutcomeKind.NotFound)]
    [InlineData(HttpStatusCode.BadRequest, """{"errors":{"name":["Name is required."]}}""", "application/problem+json", HttpOutcomeKind.ValidationProblem)]
    [InlineData(HttpStatusCode.OK, " ", "application/json", HttpOutcomeKind.EmptyBody)]
    [InlineData(HttpStatusCode.OK, "null", "application/json", HttpOutcomeKind.MalformedBody)]
    [InlineData(HttpStatusCode.OK, "{", "application/json", HttpOutcomeKind.MalformedBody)]
    [InlineData(HttpStatusCode.OK, """{"name":"Fixture"}""", "text/plain", HttpOutcomeKind.UnsupportedContentType)]
    public async Task Send_does_not_validate_a_value_for_unsuccessful_or_unmaterialized_responses(
        HttpStatusCode statusCode, string content, string mediaType, HttpOutcomeKind expected)
    {
        // Arrange
        using var handler = new HttpOutcomeMapperTestHandler((_, _) => Task.FromResult(
            HttpOutcomeMapperTestResponses.Json(statusCode, content, mediaType)));
        using var client = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("https://catalog.example.test") };
        var validations = 0;

        // Act
        var outcome = await HttpOutcomeMapper.Send(
            client,
            new HttpRequestMessage(HttpMethod.Get, "/content"),
            HttpStatusCode.OK,
            Json.HttpOutcomeMapperTestPayload,
            Json.ContractValidationProblemDto,
            validateValue: _ =>
            {
                validations++;
                return false;
            },
            TestContext.Current.CancellationToken);

        // Assert
        outcome.Kind.ShouldBe(expected);
        outcome.StatusCode.ShouldBe(statusCode);
        outcome.Value.ShouldBeNull();
        validations.ShouldBe(0);
    }

    [Fact]
    public async Task Send_preserves_exceptions_thrown_by_the_value_predicate()
    {
        // Arrange
        var responseContent = new TrackingStringContent("""{"name":"Fixture"}""", "application/json");
        var requestContent = new TrackingStringContent("request", "application/json");
        using var handler = new HttpOutcomeMapperTestHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = responseContent }));
        using var client = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("https://catalog.example.test") };
        using var request = new HttpRequestMessage(HttpMethod.Post, "/content") { Content = requestContent };
        var failure = new HttpRequestException("Synthetic caller validation failure.");

        // Act
        Func<Task> send = async () => await HttpOutcomeMapper.Send(
                client,
                request,
                HttpStatusCode.OK,
                Json.HttpOutcomeMapperTestPayload,
                Json.ContractValidationProblemDto,
                validateValue: _ => throw failure,
                TestContext.Current.CancellationToken)
            .ConfigureAwait(false);
        var result = await send.ShouldThrow<HttpRequestException>();

        // Assert
        result.ShouldBe(failure);
        requestContent.IsDisposed.ShouldBeTrue();
        responseContent.IsDisposed.ShouldBeTrue();
    }

    [Fact]
    public async Task Send_rejects_a_null_value_predicate_before_taking_request_ownership()
    {
        // Arrange
        using var requestContent = new TrackingStringContent("request", "application/json");
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://catalog.example.test/content") { Content = requestContent };
        using var client = new HttpClient();

        // Act
        Func<Task> send = async () => await HttpOutcomeMapper.Send(client, request, HttpStatusCode.OK,
                Json.HttpOutcomeMapperTestPayload, Json.ContractValidationProblemDto, validateValue: null!, TestContext.Current.CancellationToken)
            .ConfigureAwait(false);
        var failure = await send.ShouldThrow<ArgumentNullException>();

        // Assert
        failure.ParamName.ShouldBe("validateValue");
        requestContent.IsDisposed.ShouldBeFalse();
    }

    [Theory]
    [InlineData(null, HttpOutcomeKind.Succeeded, "succeeded")]
    [InlineData(true, HttpOutcomeKind.Succeeded, "succeeded")]
    [InlineData(false, HttpOutcomeKind.MalformedBody, "malformed_body")]
    [Trait(TestTraitNames.CategoryName, TestTraitValues.SecurityCategory)]
    public async Task Send_records_bounded_telemetry_for_valid_and_rejected_values(
        bool? acceptValue, HttpOutcomeKind expected, string expectedTelemetry)
    {
        // Arrange
        var stoppedActivities = new ConcurrentQueue<Activity>();
        using var parentSource = new ActivitySource($"HttpOutcomeMapperTests.{Guid.NewGuid():N}");
        var outcomeSource = HttpOutcomeMapper.OutcomeActivitySource;
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => ReferenceEquals(source, parentSource)
                || ReferenceEquals(source, outcomeSource),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => stoppedActivities.Enqueue(activity)
        };
        ActivitySource.AddActivityListener(listener);
        using var parent = parentSource.StartActivity("test-parent");
        parent.ShouldNotBeNull();

        var location = new Uri("https://catalog.example.test/item/42");
        var responseContent = new TrackingStringContent("""{"name":"alice@example.test"}""", "application/json");
        var requestContent = new TrackingStringContent("request", "application/json");
        using var handler = new HttpOutcomeMapperTestHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = responseContent,
                Headers = { Location = location, ETag = new EntityTagHeaderValue("\"v1\"") }
            }));
        using var client = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("https://catalog.example.test") };
        var request = new HttpRequestMessage(HttpMethod.Post, "/content?token=secret-token") { Content = requestContent };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "secret-token");
        var validations = 0;

        // Act
        var outcome = acceptValue is { } accept
            ? await HttpOutcomeMapper.Send(client, request, HttpStatusCode.Created, Json.HttpOutcomeMapperTestPayload, Json.ContractValidationProblemDto,
                validateValue: value =>
                {
                    validations++;
                    return accept && value.Name == "alice@example.test";
                }, TestContext.Current.CancellationToken)
            : await HttpOutcomeMapper.Send(client, request, HttpStatusCode.Created, Json.HttpOutcomeMapperTestPayload, Json.ContractValidationProblemDto,
                TestContext.Current.CancellationToken);

        // Assert
        outcome.Kind.ShouldBe(expected);
        outcome.StatusCode.ShouldBe(HttpStatusCode.Created);
        validations.ShouldBe(acceptValue.HasValue ? 1 : 0);
        requestContent.IsDisposed.ShouldBeTrue();
        responseContent.IsDisposed.ShouldBeTrue();
        if (expected == HttpOutcomeKind.Succeeded)
        {
            outcome.Value.ShouldNotBeNull().Name.ShouldBe("alice@example.test");
            outcome.Location.ShouldBe(location);
            outcome.ETag.ShouldBe("\"v1\"");
        }
        else
        {
            outcome.Value.ShouldBeNull();
            outcome.Location.ShouldBeNull();
            outcome.ETag.ShouldBeNull();
            outcome.Message.ShouldBe("HTTP response body was malformed.");
        }
        handler.ObservedAuthorizationScheme.ShouldBe("Bearer");
        var activity = stoppedActivities
            .Where(candidate => candidate.Source.Name == HttpOutcomeMapper.ActivitySourceName
                && candidate.ParentSpanId == parent.SpanId)
            .ShouldHaveSingleItem();
        var tags = activity.TagObjects.OrderBy(tag => tag.Key, StringComparer.Ordinal).ToArray();
        activity.Source.Name.ShouldBe("SharedKernel.HttpClients");
        activity.OperationName.ShouldBe("http_client.outcome");
        tags.Select(tag => tag.Key).ShouldBeEquivalentTo(
            "http.request.method",
            "http.response.status_code",
            "sharedkernel.http_client.outcome");
        tags.ShouldHaveSingleItem(tag => tag.Key == "http.request.method").Value.ShouldBe("POST");
        tags.ShouldHaveSingleItem(tag => tag.Key == "http.response.status_code").Value.ShouldBe(201);
        tags.ShouldHaveSingleItem(tag => tag.Key == "sharedkernel.http_client.outcome").Value.ShouldBe(expectedTelemetry);
        var telemetryText = string.Join('|', tags.Select(tag => $"{tag.Key}={tag.Value}"));
        telemetryText.ShouldNotContain("secret-token", StringComparison.Ordinal);
        telemetryText.ShouldNotContain("alice@example.test", StringComparison.Ordinal);
        activity.Events.ShouldBeEmpty();
    }
}
