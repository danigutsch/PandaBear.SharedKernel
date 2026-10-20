using OpenTelemetry;
using OpenTelemetry.Logs;

namespace PandaBear.Observability;

/// <summary>Removes sensitive telemetry attributes before export.</summary>
/// <param name="additionalSensitiveAttribute">Additional application attributes to remove.</param>
public sealed class LogRecordPrivacyProcessor(Func<string, bool>? additionalSensitiveAttribute = null) : BaseProcessor<LogRecord>
{
    private readonly Func<string, bool> isSensitive = name => TelemetryPrivacyAttributeClassifier.IsSensitive(name)
        || additionalSensitiveAttribute?.Invoke(name) == true;

    private const int MaximumErrorTypeLength = 256;
    private const string ExceptionTypeAttribute = "exception.type";
    private const string OriginalFormatAttribute = "{OriginalFormat}";

    /// <inheritdoc />
    public override void OnEnd(LogRecord data)
    {
        if (data is null)
        {
            return;
        }

        var attributes = data.Attributes;
        var attributesRequireSanitization = AnalyzeAttributes(
            attributes,
            out var originalFormat,
            out var structuredException);

        data.Body = originalFormat;
        data.FormattedMessage = null;

        if (data.Exception is null && !attributesRequireSanitization)
        {
            return;
        }

        var exceptionType = GetExceptionType(data.Exception ?? structuredException);
        data.Exception = null;
        data.Attributes = CreateSanitizedAttributes(attributes, exceptionType);
    }

    private bool AnalyzeAttributes(
        IReadOnlyList<KeyValuePair<string, object?>>? attributes,
        out string? originalFormat,
        out Exception? structuredException)
    {
        originalFormat = null;
        structuredException = null;
        var requiresSanitization = false;

        if (attributes is null)
        {
            return requiresSanitization;
        }

        foreach (var attribute in attributes)
        {
            if (string.Equals(attribute.Key, OriginalFormatAttribute, StringComparison.Ordinal))
            {
                originalFormat = attribute.Value as string;
            }

            requiresSanitization |= isSensitive(attribute.Key)
                || IsOversizedErrorType(attribute)
                || attribute.Value is Exception;
            if (structuredException is null && attribute.Value is Exception exception)
            {
                structuredException = exception;
            }
        }

        return requiresSanitization;
    }

    private List<KeyValuePair<string, object?>> CreateSanitizedAttributes(
        IReadOnlyList<KeyValuePair<string, object?>>? attributes,
        string? exceptionType)
    {
        var capacity = (attributes?.Count ?? 0) + (exceptionType is null ? 0 : 1);
        var sanitizedAttributes = new List<KeyValuePair<string, object?>>(capacity);

        if (attributes is not null)
        {
            foreach (var attribute in attributes)
            {
                if (ShouldRemoveAttribute(attribute, exceptionType))
                {
                    continue;
                }

                sanitizedAttributes.Add(LimitErrorType(attribute));
            }
        }

        if (exceptionType is not null)
        {
            sanitizedAttributes.Add(new KeyValuePair<string, object?>(ExceptionTypeAttribute, exceptionType));
        }

        return sanitizedAttributes;
    }

    private bool ShouldRemoveAttribute(
        KeyValuePair<string, object?> attribute,
        string? exceptionType)
    {
        return isSensitive(attribute.Key)
            || attribute.Value is Exception
            || (exceptionType is not null
                && string.Equals(attribute.Key, ExceptionTypeAttribute, StringComparison.OrdinalIgnoreCase));
    }

    private static string? GetExceptionType(Exception? exception)
    {
        if (exception is null)
        {
            return null;
        }

        var value = exception.GetType().FullName ?? exception.GetType().Name;
        return value.Length <= MaximumErrorTypeLength ? value : value[..MaximumErrorTypeLength];
    }

    private static bool IsOversizedErrorType(KeyValuePair<string, object?> attribute)
    {
        return IsErrorType(attribute.Key)
            && attribute.Value is string value
            && value.Length > MaximumErrorTypeLength;
    }

    private static KeyValuePair<string, object?> LimitErrorType(KeyValuePair<string, object?> attribute)
    {
        if (!IsOversizedErrorType(attribute))
        {
            return attribute;
        }

        return attribute.Value is string value
            ? new KeyValuePair<string, object?>(attribute.Key, value[..MaximumErrorTypeLength])
            : attribute;
    }

    private static bool IsErrorType(string name)
    {
        return string.Equals(name, "error.type", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, ExceptionTypeAttribute, StringComparison.OrdinalIgnoreCase);
    }

}
