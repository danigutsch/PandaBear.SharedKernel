namespace PandaBear.Observability;

internal static class TelemetryPrivacyAttributeClassifier
{
    /// <summary>Telemetry attribute names whose values may carry personal data, credentials, or storage identifiers and must be redacted.</summary>
    private static HashSet<string> SensitiveAttributeNames
    {
        get;
    } = new(StringComparer.OrdinalIgnoreCase)
    {
        "authorization",
        "commandText",
        "cookie",
        "email",
        "enduser.id",
        "error.message",
        "exception.message",
        "exception.stacktrace",
        "http.request.body",
        "http.response.body",
        "http.target",
        "http.url",
        "http.user_agent",
        "db.connection_string",
        "db.npgsql.connection_id",
        "db.npgsql.data_source",
        "db.parameters",
        "db.query.parameter",
        "db.query.parameters",
        "db.query.text",
        "db.statement",
        "media.object.key",
        "media.object_key",
        "object.key",
        "ObjectKey",
        "parameters",
        "Path",
        "PathBase",
        "QueryString",
        "RequestBody",
        "RequestPath",
        "RequestUri",
        "ResponseBody",
        "set-cookie",
        "Uri",
        "url.full",
        "url.path",
        "url.query",
        "user_agent.original",
        "user.id",
        "aws.extended_request_id",
        "aws.request_id",
        "aws.s3.bucket",
        "aws.s3.bucket.name",
        "aws.s3.copy_source",
        "aws.s3.delete",
        "aws.s3.key",
        "aws.s3.part_number",
        "aws.s3.upload_id",
        "aws.sns.topic.arn",
        "aws.sns.topic.name",
        "aws.sqs.queue.name",
        "aws.sqs.queue.url",
        "messaging.destination.name",
        "messaging.destination.subscription.name",
        "messaging.message.id"
    };

    public static bool IsSensitive(string name)
    {
        return SensitiveAttributeNames.Contains(name)
            || name.StartsWith("db.query.parameter.", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("http.request.header.", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("http.response.header.", StringComparison.OrdinalIgnoreCase);
    }
}
