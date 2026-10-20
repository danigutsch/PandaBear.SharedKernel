using System.Diagnostics;
using OpenTelemetry;

namespace SharedKernel.Observability;

/// <summary>Removes sensitive telemetry attributes before export.</summary>
/// <param name="additionalSensitiveAttribute">Additional application attributes to remove.</param>
public sealed class ActivityPrivacyProcessor(Func<string, bool>? additionalSensitiveAttribute = null) : BaseProcessor<Activity>
{
    private readonly Func<string, bool> isSensitive = name => TelemetryPrivacyAttributeClassifier.IsSensitive(name)
        || additionalSensitiveAttribute?.Invoke(name) == true;

    private const int MaximumErrorTypeLength = 256;

    /// <inheritdoc />
    public override void OnEnd(Activity data)
    {
        if (data is null)
        {
            return;
        }

        var sensitiveAttributeNames = data.TagObjects
            .Where(attribute => isSensitive(attribute.Key)
                || attribute.Value is Exception)
            .Select(static attribute => attribute.Key)
            .ToArray();

        foreach (var attributeName in sensitiveAttributeNames)
        {
            data.SetTag(attributeName, null);
        }

        if (data.StatusDescription is not null)
        {
            data.SetStatus(data.Status);
        }

        LimitErrorType(data, "error.type");
        LimitErrorType(data, "exception.type");
    }

    private static void LimitErrorType(Activity activity, string attributeName)
    {
        (string Name, string Value)? limitedAttribute = null;

        foreach (var attribute in activity.TagObjects)
        {
            if (string.Equals(attribute.Key, attributeName, StringComparison.OrdinalIgnoreCase)
                && attribute.Value is string value
                && value.Length > MaximumErrorTypeLength)
            {
                limitedAttribute = (attribute.Key, value[..MaximumErrorTypeLength]);
                break;
            }
        }

        if (limitedAttribute is { } attributeToUpdate)
        {
            activity.SetTag(attributeToUpdate.Name, attributeToUpdate.Value);
        }
    }
}
