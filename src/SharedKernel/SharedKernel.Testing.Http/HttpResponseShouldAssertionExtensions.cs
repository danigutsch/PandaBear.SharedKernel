using System.Net;
using System.Text.Json;
using PandaBear.Testing.Assertions;

namespace PandaBear.Testing.Http;

/// <summary>Provides assertions for HTTP problem responses without reflection-based deserialization.</summary>
public static class HttpResponseShouldAssertionExtensions
{
    /// <summary>Verifies HTTP and body statuses, problem media type, and any supplied detail or code.</summary>
    /// <param name="actual">The response, which remains owned by the caller.</param>
    /// <param name="expectedStatus">The expected HTTP status.</param>
    /// <param name="expectedDetail">The exact detail, or null to leave it unchecked.</param>
    /// <param name="expectedCode">The exact top-level code extension, or null to leave it unchecked.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A detached JSON object for additional assertions.</returns>
    public static async Task<JsonElement> ShouldBeProblem(this HttpResponseMessage actual, HttpStatusCode expectedStatus,
        string? expectedDetail = null, string? expectedCode = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actual);
        cancellationToken.ThrowIfCancellationRequested();
        actual.StatusCode.ShouldBe(expectedStatus);
        actual.Content.Headers.ContentType.ShouldNotBeNull().MediaType.ShouldBe("application/problem+json", StringComparer.OrdinalIgnoreCase);
        var bytes = await actual.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        using var document = JsonDocument.Parse(bytes);
        var problem = document.RootElement;
        problem.ValueKind.ShouldBe(JsonValueKind.Object);
        var status = GetRequiredProperty(problem, "status");
        status.ValueKind.ShouldBe(JsonValueKind.Number);
        status.TryGetInt32(out var bodyStatus).ShouldBeTrue();
        bodyStatus.ShouldBe((int)expectedStatus);
        if (expectedDetail is not null)
        {
            GetRequiredProperty(problem, "detail").GetString().ShouldBe(expectedDetail);
        }
        if (expectedCode is not null)
        {
            GetRequiredProperty(problem, "code").GetString().ShouldBe(expectedCode);
        }
        return problem.Clone();
    }

    /// <summary>Verifies a 400 problem response with a nonempty validation errors object.</summary>
    /// <param name="actual">The response, which remains owned by the caller.</param>
    /// <param name="expectedDetail">The exact detail, or null to leave it unchecked.</param>
    /// <param name="expectedCode">The exact top-level code extension, or null to leave it unchecked.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A detached JSON object for additional assertions.</returns>
    public static async Task<JsonElement> ShouldBeValidationProblem(this HttpResponseMessage actual,
        string? expectedDetail = null, string? expectedCode = null, CancellationToken cancellationToken = default)
    {
        var problem = await actual.ShouldBeProblem(HttpStatusCode.BadRequest, expectedDetail, expectedCode, cancellationToken).ConfigureAwait(false);
        var errors = GetRequiredProperty(problem, "errors");
        errors.ValueKind.ShouldBe(JsonValueKind.Object);
        errors.EnumerateObject().ShouldNotBeEmpty();
        foreach (var field in errors.EnumerateObject())
        {
            field.Value.ValueKind.ShouldBe(JsonValueKind.Array);
            field.Value.EnumerateArray().ShouldNotBeEmpty();
            foreach (var message in field.Value.EnumerateArray())
            {
                message.ValueKind.ShouldBe(JsonValueKind.String);
                string.IsNullOrWhiteSpace(message.GetString()).ShouldBeFalse();
            }
        }
        return problem;
    }

    /// <summary>Verifies that a case-sensitive validation field exists and returns its messages.</summary>
    /// <param name="actual">The parsed validation problem.</param>
    /// <param name="field">The case-sensitive field name.</param>
    /// <returns>The field message array.</returns>
    public static JsonElement ShouldHaveValidationField(this JsonElement actual, string field)
    {
        var errors = GetRequiredProperty(actual, "errors");
        errors.TryGetProperty(field, out var messages).ShouldBeTrue($"Expected validation field '{field}'.");
        messages.ValueKind.ShouldBe(JsonValueKind.Array);
        messages.EnumerateArray().ShouldNotBeEmpty();
        return messages;
    }

    /// <summary>Verifies the exact ordered messages for one field, allowing other validation fields.</summary>
    /// <param name="actual">The parsed validation problem.</param>
    /// <param name="field">The case-sensitive field name.</param>
    /// <param name="expectedMessages">The expected messages in order.</param>
    public static void ShouldHaveValidationError(this JsonElement actual, string field, params string[] expectedMessages)
    {
        var messages = actual.ShouldHaveValidationField(field);
        messages.EnumerateArray().Select(static message => message.GetString()).ShouldBe(expectedMessages);
    }

    /// <summary>Verifies every validation field and message, rejecting additional fields.</summary>
    /// <param name="actual">The parsed validation problem.</param>
    /// <param name="expected">All expected fields and ordered messages.</param>
    public static void ShouldHaveValidationErrors(this JsonElement actual, IReadOnlyDictionary<string, string[]> expected)
    {
        ArgumentNullException.ThrowIfNull(expected);
        GetRequiredProperty(actual, "errors").EnumerateObject().Count().ShouldBe(expected.Count);
        foreach (var (field, messages) in expected)
        {
            actual.ShouldHaveValidationError(field, messages);
        }
    }

    private static JsonElement GetRequiredProperty(JsonElement actual, string name)
    {
        actual.ValueKind.ShouldBe(JsonValueKind.Object);
        actual.TryGetProperty(name, out var value).ShouldBeTrue($"Expected problem property '{name}'.");
        return value;
    }
}
