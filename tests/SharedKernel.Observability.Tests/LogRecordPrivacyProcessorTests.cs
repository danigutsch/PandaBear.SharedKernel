using Microsoft.Extensions.Logging;

namespace PandaBear.Observability.Tests;

/// <summary>Tests that exported log records are stripped of sensitive attributes, scopes and exception details by the log privacy processor.</summary>
[Trait(Testing.TestTraitNames.CategoryName, Testing.TestTraitValues.SecurityCategory)]
public sealed class LogRecordPrivacyProcessorTests
{
    [Fact]
    public void Log_export_removes_exceptions_sensitive_attributes_and_scopes_while_preserving_template_and_outcome()
    {
        // Arrange
        using var scope = new PrivacyLogTestScope();
        using var logScope = scope.Logger.BeginScope(new Dictionary<string, object?> { ["email"] = "private@example.test" });
        var log = LoggerMessage.Define<string, string, string>(LogLevel.Error, new EventId(1),
            "Operation for {Email} object {ObjectKey} ended with {Outcome}");

        // Act
        log(scope.Logger, "private@example.test", "private/object.txt", "failed",
            new InvalidOperationException("private@example.test"));

        // Assert
        var result = scope.Records.ShouldHaveSingleItem();
        result.Body.ShouldBe("Operation for {Email} object {ObjectKey} ended with {Outcome}");
        result.FormattedMessage.ShouldBeNull();
        result.Exception.ShouldBeNull();
        result.ScopeCount.ShouldBe(0);
        result.Attributes.ShouldNotContain(attribute =>
            string.Equals(attribute.Key, "Email", StringComparison.OrdinalIgnoreCase)
            || string.Equals(attribute.Key, "ObjectKey", StringComparison.OrdinalIgnoreCase));
        result.Attributes.ShouldContain(attribute => attribute.Key == "Outcome" && Equals(attribute.Value, "failed"));
        result.Attributes.ShouldContain(attribute => attribute.Key == "exception.type"
            && Equals(attribute.Value, typeof(InvalidOperationException).FullName));
    }

    [Fact]
    public void Log_export_scrubs_exception_values_from_otherwise_safe_structured_attributes()
    {
        // Arrange
        using var scope = new PrivacyLogTestScope();
        var log = LoggerMessage.Define<Exception, string>(LogLevel.Error, new EventId(1),
            "Operation failed with {DiagnosticFailure} and {Outcome}");

        // Act
        log(scope.Logger, new InvalidOperationException("private@example.test"), "retryable", null);

        // Assert
        var result = scope.Records.ShouldHaveSingleItem();
        result.Exception.ShouldBeNull();
        result.Attributes.ShouldNotContain(attribute => attribute.Value is Exception);
        result.Attributes.ShouldContain(attribute => attribute.Key == "exception.type"
            && Equals(attribute.Value, typeof(InvalidOperationException).FullName));
        result.Attributes.ShouldContain(attribute => attribute.Key == "Outcome" && Equals(attribute.Value, "retryable"));
    }

    [Fact]
    public void Log_export_removes_database_commands_and_parameters()
    {
        // Arrange
        using var scope = new PrivacyLogTestScope();
        var log = LoggerMessage.Define<string, string, string>(LogLevel.Information, new EventId(1),
            "Executed {CommandText} with {Parameters}; {Outcome}");

        // Act
        log(scope.Logger, "SELECT * FROM records WHERE email = @email", "@email='private@example.test'", "executed", null);

        // Assert
        var result = scope.Records.ShouldHaveSingleItem();
        result.Attributes.ShouldNotContain(attribute =>
            string.Equals(attribute.Key, "CommandText", StringComparison.OrdinalIgnoreCase)
            || string.Equals(attribute.Key, "Parameters", StringComparison.OrdinalIgnoreCase));
        result.Attributes.ShouldContain(attribute => attribute.Key == "Outcome" && Equals(attribute.Value, "executed"));
    }

    [Fact]
    public void Log_export_replaces_existing_exception_type_case_insensitively()
    {
        // Arrange
        using var scope = new PrivacyLogTestScope();
        var log = LoggerMessage.Define<string>(LogLevel.Error, new EventId(1), "Operation failed with {Exception.Type}");

        // Act
        log(scope.Logger, "Untrusted.Exception.Type", new InvalidOperationException("private@example.test"));

        // Assert
        var result = scope.Records.ShouldHaveSingleItem();
        var attribute = result.Attributes.ShouldHaveSingleItem(item =>
            string.Equals(item.Key, "exception.type", StringComparison.OrdinalIgnoreCase));
        attribute.Key.ShouldBe("exception.type");
        attribute.Value.ShouldBe(typeof(InvalidOperationException).FullName);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Log_export_bounds_error_type_values_case_insensitively(bool exceptionType)
    {
        // Arrange
        using var scope = new PrivacyLogTestScope();
        var log = LoggerMessage.Define<string>(LogLevel.Error, new EventId(1),
            exceptionType ? "Operation failed with {Exception.Type}" : "Operation failed with {Error.Type}");

        // Act
        log(scope.Logger, new string('x', 257), null);

        // Assert
        var result = scope.Records.ShouldHaveSingleItem();
        var attribute = result.Attributes.ShouldHaveSingleItem(item =>
            string.Equals(item.Key, exceptionType ? "exception.type" : "error.type", StringComparison.OrdinalIgnoreCase));
        attribute.Value.ShouldBe(new string('x', 256));
    }

    [Fact]
    public void Additional_log_policy_extends_baseline_redaction_without_removing_safe_outcomes()
    {
        // Arrange
        using var scope = new PrivacyLogTestScope(name => string.Equals(name, "PrivateIdentifier", StringComparison.Ordinal));
        var log = LoggerMessage.Define<string, string, string>(LogLevel.Information, new EventId(1),
            "Authorization {Authorization} identifier {PrivateIdentifier} outcome {Outcome}");

        // Act
        log(scope.Logger, "private-token", "private-record", "completed", null);

        // Assert
        var result = scope.Records.ShouldHaveSingleItem();
        result.Attributes.ShouldNotContain(attribute => attribute.Key is "Authorization" or "PrivateIdentifier");
        result.Attributes.ShouldContain(attribute => attribute.Key == "Outcome" && Equals(attribute.Value, "completed"));
    }

    [Fact]
    public void Additional_log_policy_cannot_disable_baseline_redaction()
    {
        // Arrange
        using var scope = new PrivacyLogTestScope(_ => false);
        var log = LoggerMessage.Define<string, string>(LogLevel.Information, new EventId(1),
            "Authorization {Authorization} outcome {Outcome}");

        // Act
        log(scope.Logger, "private-token", "completed", null);

        // Assert
        var result = scope.Records.ShouldHaveSingleItem();
        result.Attributes.ShouldNotContain(attribute => attribute.Key == "Authorization");
        result.Attributes.ShouldContain(attribute => attribute.Key == "Outcome" && Equals(attribute.Value, "completed"));
    }
}
