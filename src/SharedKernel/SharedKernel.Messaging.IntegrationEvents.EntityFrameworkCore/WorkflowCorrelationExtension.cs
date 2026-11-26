using System.Text.Json;

namespace SharedKernel.Messaging.IntegrationEvents.EntityFrameworkCore;

/// <summary>Reads the optional workflow identity carried by a durable integration event envelope.</summary>
internal static class WorkflowCorrelationExtension
{
    /// <summary>The reserved CloudEvents string attribute for the workflow identifier.</summary>
    public const string AttributeName = "workflowid";

    /// <summary>Gets one valid workflow identifier from the reserved attribute, when present.</summary>
    public static Guid? Read(string? extensionAttributesJson)
    {
        if (extensionAttributesJson is null)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(extensionAttributesJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            Guid? workflowCorrelationId = null;
            foreach (var attribute in document.RootElement.EnumerateObject())
            {
                if (!attribute.NameEquals(AttributeName))
                {
                    continue;
                }

                if (workflowCorrelationId is not null
                    || attribute.Value.ValueKind != JsonValueKind.String
                    || attribute.Value.GetString() is not { Length: 36 } value
                    || !Guid.TryParseExact(value, "D", out var id)
                    || id == Guid.Empty)
                {
                    return null;
                }

                workflowCorrelationId = id;
            }

            return workflowCorrelationId;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
