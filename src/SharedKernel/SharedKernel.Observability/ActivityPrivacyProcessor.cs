using System.Diagnostics;
using OpenTelemetry;

namespace PandaBear.Observability;

/// <summary>Removes sensitive telemetry attributes before export.</summary>
/// <param name="additionalSensitiveAttribute">Additional application attributes to remove.</param>
public sealed class ActivityPrivacyProcessor(Func<string, bool>? additionalSensitiveAttribute = null) : BaseProcessor<Activity>
{
    private const string PostgreSqlSystemName = "postgresql";

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

        var isPostgreSql = data.Kind == ActivityKind.Client
            && (Equals(data.GetTagItem("db.system.name"), PostgreSqlSystemName)
                || Equals(data.GetTagItem("db.system"), PostgreSqlSystemName));
        var sensitiveAttributeNames = data.TagObjects
            .Where(attribute => isSensitive(attribute.Key)
                || (isPostgreSql && TelemetryPrivacyAttributeClassifier.IsPostgreSqlIdentity(attribute.Key))
                || attribute.Value is Exception)
            .Select(static attribute => attribute.Key)
            .ToArray();

        foreach (var attributeName in sensitiveAttributeNames)
        {
            data.SetTag(attributeName, null);
        }

        if (isPostgreSql)
        {
            data.DisplayName = data.DisplayName.StartsWith("CONNECT ", StringComparison.Ordinal) ? "CONNECT" : PostgreSqlSystemName;
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
