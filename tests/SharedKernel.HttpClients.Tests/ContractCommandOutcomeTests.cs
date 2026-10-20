using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace SharedKernel.HttpClients.Tests;

[Trait(Testing.SharedKernelTestTraitNames.ScopeName, Testing.SharedKernelTestTraitNames.UnitScope)]
public sealed class ContractCommandOutcomeTests
{
    private static readonly ContractCommandOutcomeTestsJsonContext Json = ContractCommandOutcomeTestsJsonContext.Default;

    [Fact]
    public async Task Send_returns_success_with_materialized_body_and_propagates_the_exact_token()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        using var handler = new ContractCommandOutcomeTestHandler((_, _) => Task.FromResult(
            ContractCommandOutcomeTestResponses.Json(HttpStatusCode.OK, "{\"name\":\"Lisbon\"}")));
        using var client = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("https://catalog.example.test") };
        var request = new HttpRequestMessage(HttpMethod.Get, "/tours");

        // Act
        var outcome = await ContractCommandOutcome.Send(
            client,
            request,
            HttpStatusCode.OK,
            Json.ContractCommandOutcomeTestPayload,
            Json.ContractValidationProblemDto,
            cts.Token);

        // Assert
        outcome.Kind.ShouldBe(ContractCommandOutcomeKind.Succeeded);
        outcome.StatusCode.ShouldBe(HttpStatusCode.OK);
        var value = outcome.Value.ShouldNotBeNull();
        value.Name.ShouldBe("Lisbon");
        handler.ObservedCancellationToken.CanBeCanceled.ShouldBeTrue();
        handler.ObservedMethod.ShouldBe(HttpMethod.Get);
        handler.ObservedRequestUri.ShouldBe(new Uri("https://catalog.example.test/tours"));
    }

    [Fact]
    public async Task Send_returns_not_found_without_reading_a_body()
    {
        // Arrange
        using var responseBody = new TrackingReadStream("sensitive missing resource"u8.ToArray());
        using var handler = new ContractCommandOutcomeTestHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StreamContent(responseBody)
            }));
        using var client = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("https://catalog.example.test") };

        // Act
        var outcome = await ContractCommandOutcome.Send(
            client,
            new HttpRequestMessage(HttpMethod.Get, "/tours/missing"),
            HttpStatusCode.OK,
            Json.ContractCommandOutcomeTestPayload,
            Json.ContractValidationProblemDto,
            TestContext.Current.CancellationToken);

        // Assert
        outcome.Kind.ShouldBe(ContractCommandOutcomeKind.NotFound);
        outcome.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        outcome.Value.ShouldBeNull();
        responseBody.ReadCount.ShouldBe(0);
        responseBody.IsDisposed.ShouldBeTrue();
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, ContractCommandOutcomeKind.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden, ContractCommandOutcomeKind.Forbidden)]
    [InlineData(HttpStatusCode.Conflict, ContractCommandOutcomeKind.Conflict)]
    public async Task Send_returns_bounded_kind_for_known_non_success_status(
        HttpStatusCode statusCode,
        ContractCommandOutcomeKind expectedKind)
    {
        // Arrange
        using var handler = new ContractCommandOutcomeTestHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(statusCode)));
        using var client = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("https://catalog.example.test") };

        // Act
        var outcome = await ContractCommandOutcome.Send(
            client,
            new HttpRequestMessage(HttpMethod.Get, "/content"),
            HttpStatusCode.OK,
            Json.ContractCommandOutcomeTestPayload,
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
    public async Task Send_returns_validation_problem_from_problem_json(string fieldName)
    {
        // Arrange
        using var handler = new ContractCommandOutcomeTestHandler((_, _) => Task.FromResult(
            ContractCommandOutcomeTestResponses.Json(
                HttpStatusCode.BadRequest,
                $$$"""{"errors":{"{{{fieldName}}}":["Name is required."]}}""",
                "application/problem+json")));
        using var client = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("https://admin.example.test") };

        // Act
        var outcome = await ContractCommandOutcome.Send(
            client,
            new HttpRequestMessage(HttpMethod.Post, "/customers"),
            HttpStatusCode.Created,
            Json.ContractCommandOutcomeTestPayload,
            Json.ContractValidationProblemDto,
            TestContext.Current.CancellationToken);

        // Assert
        outcome.Kind.ShouldBe(ContractCommandOutcomeKind.ValidationProblem);
        outcome.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var errors = outcome.ValidationErrors.ShouldNotBeNull();
        errors[fieldName].ShouldBeEquivalentTo("Name is required.");
    }

    [Fact]
    public async Task Send_returns_empty_body_for_whitespace_success_content()
    {
        // Arrange
        using var handler = new ContractCommandOutcomeTestHandler((_, _) => Task.FromResult(
            ContractCommandOutcomeTestResponses.Json(HttpStatusCode.OK, "   ")));
        using var client = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("https://catalog.example.test") };

        // Act
        var outcome = await ContractCommandOutcome.Send(
            client,
            new HttpRequestMessage(HttpMethod.Get, "/content"),
            HttpStatusCode.OK,
            Json.ContractCommandOutcomeTestPayload,
            Json.ContractValidationProblemDto,
            TestContext.Current.CancellationToken);

        // Assert
        outcome.Kind.ShouldBe(ContractCommandOutcomeKind.EmptyBody);
        outcome.Message.ShouldBe("HTTP response body was empty.");
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("null")]
    public async Task Send_returns_malformed_body_for_invalid_or_null_json(string content)
    {
        // Arrange
        using var handler = new ContractCommandOutcomeTestHandler((_, _) => Task.FromResult(
            ContractCommandOutcomeTestResponses.Json(HttpStatusCode.OK, content)));
        using var client = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("https://catalog.example.test") };

        // Act
        var outcome = await ContractCommandOutcome.Send(
            client,
            new HttpRequestMessage(HttpMethod.Get, "/content"),
            HttpStatusCode.OK,
            Json.ContractCommandOutcomeTestPayload,
            Json.ContractValidationProblemDto,
            TestContext.Current.CancellationToken);

        // Assert
        outcome.Kind.ShouldBe(ContractCommandOutcomeKind.MalformedBody);
        outcome.Value.ShouldBeNull();
        outcome.Message.ShouldNotContain(content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Send_returns_unsupported_content_type_without_exposing_the_body()
    {
        // Arrange
        const string sensitiveBody = "{\"name\":\"alice@example.test\"}";
        using var responseBody = new TrackingReadStream(Encoding.UTF8.GetBytes(sensitiveBody));
        using var handler = new ContractCommandOutcomeTestHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(responseBody)
                {
                    Headers = { ContentType = new MediaTypeHeaderValue("text/plain") }
                }
            }));
        using var client = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("https://catalog.example.test") };

        // Act
        var outcome = await ContractCommandOutcome.Send(
            client,
            new HttpRequestMessage(HttpMethod.Get, "/content"),
            HttpStatusCode.OK,
            Json.ContractCommandOutcomeTestPayload,
            Json.ContractValidationProblemDto,
            TestContext.Current.CancellationToken);

        // Assert
        outcome.Kind.ShouldBe(ContractCommandOutcomeKind.UnsupportedContentType);
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
        using var handler = new ContractCommandOutcomeTestHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StreamContent(responseBody)
            }));
        using var client = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("https://admin.example.test") };

        // Act
        var outcome = await ContractCommandOutcome.Send(
            client,
            new HttpRequestMessage(HttpMethod.Post, "/customers"),
            HttpStatusCode.Created,
            Json.ContractValidationProblemDto,
            TestContext.Current.CancellationToken);

        // Assert
        outcome.Kind.ShouldBe(ContractCommandOutcomeKind.Succeeded);
        responseBody.ReadCount.ShouldBe(0);
        responseBody.IsDisposed.ShouldBeTrue();
    }

    [Fact]
    [Trait(Testing.TestTraitNames.CategoryName, Testing.TestTraitValues.SecurityCategory)]
    public async Task Send_returns_malformed_body_after_reading_only_the_bounded_oversized_json_prefix()
    {
        // Arrange
        const int responseBodyLimit = 64;
        var oversizedJson = Encoding.UTF8.GetBytes($"{{\"name\":\"{new string('x', responseBodyLimit * 2)}\"}}");
        using var responseBody = new TrackingReadStream(oversizedJson);
        using var handler = new ContractCommandOutcomeTestHandler((_, _) => Task.FromResult(
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
        var outcome = await ContractCommandOutcome.Send(
            client,
            new HttpRequestMessage(HttpMethod.Get, "/content"),
            HttpStatusCode.OK,
            Json.ContractCommandOutcomeTestPayload,
            Json.ContractValidationProblemDto,
            TestContext.Current.CancellationToken);

        // Assert
        outcome.Kind.ShouldBe(ContractCommandOutcomeKind.MalformedBody);
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
        using var handler = new ContractCommandOutcomeTestHandler((_, _) => Task.FromResult(
            ContractCommandOutcomeTestResponses.Json(
                HttpStatusCode.BadRequest,
                content,
                "application/problem+json")));
        using var client = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("https://admin.example.test") };

        // Act
        var outcome = await ContractCommandOutcome.Send(
            client,
            new HttpRequestMessage(HttpMethod.Post, "/customers"),
            HttpStatusCode.Created,
            Json.ContractCommandOutcomeTestPayload,
            Json.ContractValidationProblemDto,
            TestContext.Current.CancellationToken);

        // Assert
        outcome.Kind.ShouldBe(ContractCommandOutcomeKind.MalformedBody);
        outcome.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        outcome.ValidationErrors.ShouldBeNull();
        outcome.Message.ShouldBe("Validation problem response body was malformed.");
    }

    [Fact]
    public async Task Send_returns_timed_out_when_transport_cancels_without_caller_cancellation()
    {
        // Arrange
        using var handler = new ContractCommandOutcomeTestHandler((_, _) => throw new TaskCanceledException("secret timeout details"));
        using var client = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("https://catalog.example.test") };

        // Act
        var outcome = await ContractCommandOutcome.Send(
            client,
            new HttpRequestMessage(HttpMethod.Get, "/content"),
            HttpStatusCode.OK,
            Json.ContractCommandOutcomeTestPayload,
            Json.ContractValidationProblemDto,
            TestContext.Current.CancellationToken);

        // Assert
        outcome.Kind.ShouldBe(ContractCommandOutcomeKind.TimedOut);
        outcome.StatusCode.ShouldBeNull();
        outcome.Message.ShouldBe("HTTP request timed out.");
    }

    [Fact]
    public async Task Send_returns_timed_out_when_the_resilience_pipeline_times_out()
    {
        // Arrange
        using var handler = new ContractCommandOutcomeTestHandler((_, _) => throw new TimeoutRejectedException("secret timeout details"));
        using var client = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("https://catalog.example.test") };

        // Act
        var outcome = await ContractCommandOutcome.Send(
            client,
            new HttpRequestMessage(HttpMethod.Get, "/content"),
            HttpStatusCode.OK,
            Json.ContractCommandOutcomeTestPayload,
            Json.ContractValidationProblemDto,
            TestContext.Current.CancellationToken);

        // Assert
        outcome.Kind.ShouldBe(ContractCommandOutcomeKind.TimedOut);
        outcome.StatusCode.ShouldBeNull();
        outcome.Message.ShouldBe("HTTP request timed out.");
    }

    [Fact]
    public async Task Send_returns_timed_out_when_the_response_body_stalls_after_headers()
    {
        // Arrange
        using var responseBody = new BlockingReadStream();
        using var handler = new ContractCommandOutcomeTestHandler((_, _) => Task.FromResult(
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
        var outcome = await ContractCommandOutcome.Send(
            client,
            new HttpRequestMessage(HttpMethod.Get, "/content"),
            HttpStatusCode.OK,
            Json.ContractCommandOutcomeTestPayload,
            Json.ContractValidationProblemDto,
            TestContext.Current.CancellationToken);

        // Assert
        responseBody.ReadStarted.IsCompletedSuccessfully.ShouldBeTrue();
        outcome.Kind.ShouldBe(ContractCommandOutcomeKind.TimedOut);
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
        using var handler = new ContractCommandOutcomeTestHandler((_, token) => throw new OperationCanceledException(token));
        using var client = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("https://catalog.example.test") };

        // Act
        Func<Task> action = async () => await ContractCommandOutcome.Send(
                client,
                new HttpRequestMessage(HttpMethod.Get, "/content"),
                HttpStatusCode.OK,
                Json.ContractCommandOutcomeTestPayload,
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
        using var handler = new ContractCommandOutcomeTestHandler((_, _) => Task.FromResult(
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
        // Act
        Func<Task> action = async () => await ContractCommandOutcome.Send(
                client,
                new HttpRequestMessage(HttpMethod.Get, "/content"),
                HttpStatusCode.OK,
                Json.ContractCommandOutcomeTestPayload,
                Json.ContractValidationProblemDto,
                cts.Token)
            .ConfigureAwait(false);
        var exception = await action
            .ShouldThrowAssignableTo<OperationCanceledException>();

        // Assert
        responseBody.ReadStarted.IsCompletedSuccessfully.ShouldBeTrue();
        exception.CancellationToken.ShouldBe(cts.Token);
        responseBody.IsDisposed.ShouldBeTrue();
    }

    [Fact]
    public async Task Send_without_value_preserves_caller_cancellation_with_a_finite_timeout()
    {
        // Arrange
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        using var responseBody = new BlockingReadStream(cts.Cancel);
        using var handler = new ContractCommandOutcomeTestHandler((_, _) => Task.FromResult(
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
        Func<Task> action = async () => await ContractCommandOutcome.Send(
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
    public async Task Send_rejects_a_known_oversized_json_body_without_reading_it()
    {
        // Arrange
        const int responseBodyLimit = 64;
        using var responseBody = new TrackingReadStream("{}"u8.ToArray());
        var content = new StreamContent(responseBody);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        content.Headers.ContentLength = responseBodyLimit + 1;
        using var handler = new ContractCommandOutcomeTestHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = content }));
        using var client = new HttpClient(handler, disposeHandler: false)
        {
            BaseAddress = new Uri("https://catalog.example.test"),
            MaxResponseContentBufferSize = responseBodyLimit
        };

        // Act
        var outcome = await ContractCommandOutcome.Send(
            client,
            new HttpRequestMessage(HttpMethod.Get, "/content"),
            HttpStatusCode.OK,
            Json.ContractCommandOutcomeTestPayload,
            Json.ContractValidationProblemDto,
            TestContext.Current.CancellationToken);

        // Assert
        outcome.Kind.ShouldBe(ContractCommandOutcomeKind.MalformedBody);
        outcome.StatusCode.ShouldBe(HttpStatusCode.OK);
        responseBody.ReadCount.ShouldBe(0);
        responseBody.IsDisposed.ShouldBeTrue();
    }

    [Fact]
    public async Task Send_preserves_request_content_read_failures_for_upload_callers()
    {
        // Arrange
        var requestContent = new TrackingStringContent("request", "application/octet-stream");
        using var handler = new ContractCommandOutcomeTestHandler((request, _) =>
        {
            request.Content.ShouldNotBeNull();
            throw new IOException("The local upload stream could not be read.");
        });
        using var client = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("https://catalog.example.test") };
        using var request = new HttpRequestMessage(HttpMethod.Post, "/images") { Content = requestContent };

        // Act
        Func<Task> action = async () => await ContractCommandOutcome.Send(
                client,
                request,
                HttpStatusCode.Created,
                Json.ContractCommandOutcomeTestPayload,
                Json.ContractValidationProblemDto,
                TestContext.Current.CancellationToken)
            .ConfigureAwait(false);

        // Assert
        await action.ShouldThrow<IOException>();
        requestContent.IsDisposed.ShouldBeTrue();
    }

    [Fact]
    [Trait(Testing.TestTraitNames.CategoryName, Testing.TestTraitValues.SecurityCategory)]
    public async Task Send_returns_redacted_transport_failure()
    {
        // Arrange
        using var handler = new ContractCommandOutcomeTestHandler((_, _) => throw new HttpRequestException(
            "Bearer secret-token failed for alice@example.test"));
        using var client = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("https://catalog.example.test") };

        // Act
        var outcome = await ContractCommandOutcome.Send(
            client,
            new HttpRequestMessage(HttpMethod.Get, "/content?token=secret-token"),
            HttpStatusCode.OK,
            Json.ContractCommandOutcomeTestPayload,
            Json.ContractValidationProblemDto,
            TestContext.Current.CancellationToken);

        // Assert
        outcome.Kind.ShouldBe(ContractCommandOutcomeKind.TransportFailure);
        outcome.StatusCode.ShouldBeNull();
        outcome.Message.ShouldBe("HTTP transport failed.");
        outcome.Message.ShouldNotContain("secret-token", StringComparison.Ordinal);
        outcome.Message.ShouldNotContain("alice@example.test", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Send_returns_transport_failure_when_the_circuit_is_open()
    {
        // Arrange
        using var handler = new ContractCommandOutcomeTestHandler((_, _) => throw new BrokenCircuitException("secret circuit details"));
        using var client = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("https://catalog.example.test") };

        // Act
        var outcome = await ContractCommandOutcome.Send(
            client,
            new HttpRequestMessage(HttpMethod.Get, "/content"),
            HttpStatusCode.OK,
            Json.ContractCommandOutcomeTestPayload,
            Json.ContractValidationProblemDto,
            TestContext.Current.CancellationToken);

        // Assert
        outcome.Kind.ShouldBe(ContractCommandOutcomeKind.TransportFailure);
        outcome.StatusCode.ShouldBeNull();
        outcome.Message.ShouldBe("HTTP transport failed.");
    }

    [Fact]
    public async Task Send_returns_unexpected_status_for_an_unowned_response_status()
    {
        // Arrange
        using var handler = new ContractCommandOutcomeTestHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.Accepted)));
        using var client = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("https://catalog.example.test") };

        // Act
        var outcome = await ContractCommandOutcome.Send(
            client,
            new HttpRequestMessage(HttpMethod.Get, "/content"),
            HttpStatusCode.OK,
            Json.ContractCommandOutcomeTestPayload,
            Json.ContractValidationProblemDto,
            TestContext.Current.CancellationToken);

        // Assert
        outcome.Kind.ShouldBe(ContractCommandOutcomeKind.UnexpectedStatus);
        outcome.StatusCode.ShouldBe(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task Send_disposes_the_owned_request_response_and_content()
    {
        // Arrange
        var responseContent = new TrackingStringContent("{\"name\":\"Lisbon\"}", "application/json");
        var requestContent = new TrackingStringContent("request", "application/json");
        using var handler = new ContractCommandOutcomeTestHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = responseContent }));
        using var client = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("https://catalog.example.test") };
        var request = new HttpRequestMessage(HttpMethod.Post, "/content") { Content = requestContent };

        // Act
        var outcome = await ContractCommandOutcome.Send(
            client,
            request,
            HttpStatusCode.OK,
            Json.ContractCommandOutcomeTestPayload,
            Json.ContractValidationProblemDto,
            TestContext.Current.CancellationToken);

        // Assert
        outcome.Kind.ShouldBe(ContractCommandOutcomeKind.Succeeded);
        requestContent.IsDisposed.ShouldBeTrue();
        responseContent.IsDisposed.ShouldBeTrue();
    }

    [Fact]
    [Trait(Testing.TestTraitNames.CategoryName, Testing.TestTraitValues.SecurityCategory)]
    public async Task Send_telemetry_contains_only_method_status_and_outcome_kind()
    {
        // Arrange
        var stoppedActivities = new ConcurrentQueue<Activity>();
        using var parentSource = new ActivitySource($"ContractCommandOutcomeTests.{Guid.NewGuid():N}");
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => ReferenceEquals(source, parentSource)
                || source.Name == ContractCommandOutcome.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => stoppedActivities.Enqueue(activity)
        };
        ActivitySource.AddActivityListener(listener);
        using var parent = parentSource.StartActivity("test-parent");
        parent.ShouldNotBeNull();

        using var handler = new ContractCommandOutcomeTestHandler((_, _) => Task.FromResult(
            ContractCommandOutcomeTestResponses.Json(HttpStatusCode.OK, "{\"name\":\"Lisbon\"}")));
        using var client = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("https://catalog.example.test") };
        var request = new HttpRequestMessage(HttpMethod.Get, "/content?token=secret-token");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "secret-token");

        // Act
        var outcome = await ContractCommandOutcome.Send(
            client,
            request,
            HttpStatusCode.OK,
            Json.ContractCommandOutcomeTestPayload,
            Json.ContractValidationProblemDto,
            TestContext.Current.CancellationToken);

        // Assert
        outcome.Kind.ShouldBe(ContractCommandOutcomeKind.Succeeded);
        handler.ObservedAuthorizationScheme.ShouldBe("Bearer");
        var activity = stoppedActivities
            .Where(candidate => candidate.Source.Name == ContractCommandOutcome.ActivitySourceName
                && candidate.ParentSpanId == parent.SpanId)
            .ShouldHaveSingleItem();
        var tags = activity.TagObjects.OrderBy(tag => tag.Key, StringComparer.Ordinal).ToArray();
        tags.Select(tag => tag.Key).ShouldBeEquivalentTo(
            "http.outcome.kind",
            "http.request.method",
            "http.response.status_code");
        tags.Single(tag => tag.Key == "http.request.method").Value.ShouldBe("GET");
        tags.Single(tag => tag.Key == "http.response.status_code").Value.ShouldBe(200);
        tags.Single(tag => tag.Key == "http.outcome.kind").Value.ShouldBe(nameof(ContractCommandOutcomeKind.Succeeded));
        var telemetryText = string.Join('|', tags.Select(tag => $"{tag.Key}={tag.Value}"));
        telemetryText.ShouldNotContain("secret-token", StringComparison.Ordinal);
        telemetryText.ShouldNotContain("alice@example.test", StringComparison.Ordinal);
        activity.Events.ShouldBeEmpty();
    }
}
